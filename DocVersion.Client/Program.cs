using System.Net.Http.Json;
using DocVersion.Core.Helpers;
using DocVersion.Core.Models;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR.Client;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length < 2)
        {
            MessageColor("Usage: DocVersion.Client [pull|push|sync] <serverUrl> [username] [password]", ConsoleColor.Red);
            return 1;
        }
        var command = args[0].ToLower();
        var serverUrl = NormalizeServerUrl(args[1]);
        var username = args.Length > 2 ? args[2] : null;
        var password = args.Length > 3 ? args[3] : null;
        var cwd = Directory.GetCurrentDirectory();

        MessageColor("Working directory: " + cwd, ConsoleColor.Cyan);

        try
        {
            using var client = new HttpClient();

            if (username != null || password != null)
            {
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    MessageColor("Both username and password must be provided.", ConsoleColor.Red);
                    return 1;
                }

                var loginResponse = await client.PostAsJsonAsync(
                    $"{serverUrl}/api/login",
                    new { User = username, Password = password });

                if (!loginResponse.IsSuccessStatusCode)
                {
                    MessageColor("Login failed: " + loginResponse.StatusCode, ConsoleColor.Red);
                    return 1;
                }
                var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
                var token = loginResult?.Token;
                if (string.IsNullOrEmpty(token))
                {
                    MessageColor("Login failed: No token received", ConsoleColor.Red);
                    return 1;
                }
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            if (command == "pull")
            {
                await Pull(client, serverUrl, cwd);
            }
            else if (command == "push")
            {
                await Push(client, serverUrl, cwd);
            }
            else if (command == "sync")
            {
                await Sync(client, serverUrl, cwd);
            }
            else
            {
                MessageColor("Unknown command: " + command, ConsoleColor.Red);
                return 1;
            }
        }
        catch (Exception ex)
        {
            MessageColor("Error: " + ex.Message, ConsoleColor.Red);
            return 1;
        }
        return 0;
    }

    private static async Task Pull(HttpClient client, string serverUrl, string cwd)
    {
        MessageColor("Pulling files from server...", ConsoleColor.Blue);
        var response = await client.GetAsync($"{serverUrl}/api/files");
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to pull files: " + response.StatusCode);
        }
        var files = await response.Content.ReadFromJsonAsync<Dictionary<string, FileMetadata>>();
        if (files == null)
        {
            files = new Dictionary<string, FileMetadata>();
        }

        var flatServerFiles = ToFlatList(files)
            .ToDictionary(entry => entry.Path, entry => entry.Metadata);

        foreach (var file in flatServerFiles.OrderBy(entry => entry.Value.IsFile))
        {
            var filename = file.Key;
            var metadata = file.Value;
            var localPath = Path.Combine(cwd, filename);
            if (metadata.IsFile)
            {
                MessageColor($"Pulling file: {filename} ({metadata.Bytes} bytes)", ConsoleColor.Gray);
                var fileResponse = await client.GetAsync($"{serverUrl}/api/files/{EncodePathForApi(filename)}");
                if (!fileResponse.IsSuccessStatusCode)
                {
                    MessageColor($"Failed to pull file {filename}: " + fileResponse.StatusCode, ConsoleColor.Red);
                    continue;
                }
                MessageColor($"Successfully pulled file: {filename}", ConsoleColor.Green);
                var content = await fileResponse.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? "");
                await File.WriteAllBytesAsync(localPath, content);
            }
            else
            {
                if (!Directory.Exists(localPath))
                {
                    MessageColor($"Creating folder: {filename}", ConsoleColor.DarkYellow);
                    Directory.CreateDirectory(localPath);
                }
            }
        }

        var localFiles = Directory.GetFiles(cwd, "*", SearchOption.AllDirectories);
        foreach (var localFile in localFiles)
        {
            var relativePath = Path.GetRelativePath(cwd, localFile).Replace("\\", "/");
            if (!flatServerFiles.ContainsKey(relativePath))
            {
                try
                {
                    if (File.Exists(localFile))
                    {
                        File.Delete(localFile);
                        MessageColor($"Deleted file: {localFile}", ConsoleColor.DarkRed);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    MessageColor($"Error: You do not have permission to delete {localFile}", ConsoleColor.Red);
                }
                catch (IOException ex)
                {
                    MessageColor($"Error: Could not delete {localFile}. It may be in use. {ex.Message}", ConsoleColor.Red);
                }
                catch (Exception ex)
                {
                    MessageColor($"Unexpected error deleting {localFile}: {ex.Message}", ConsoleColor.Red);
                }
            }
        }

        var localDirs = Directory.GetDirectories(cwd, "*", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length);

        foreach (var localDir in localDirs)
        {
            var relativeDirPath = Path.GetRelativePath(cwd, localDir).Replace("\\", "/");
            var existsOnServerAsFolder = flatServerFiles.TryGetValue(relativeDirPath, out var metadata) && !metadata.IsFile;
            if (existsOnServerAsFolder)
                continue;

            try
            {
                FileHelper.PrepareDirectoryForDelete(localDir);
                Directory.Delete(localDir, true);
                MessageColor($"Deleted folder: {localDir}", ConsoleColor.DarkRed);
            }
            catch (Exception ex)
            {
                MessageColor($"Could not delete folder {localDir}: {ex.Message}", ConsoleColor.DarkRed);
            }
        }
    }

    private static async Task Push(HttpClient client, string serverUrl, string cwd)
    {
        MessageColor("Pushing files to server...", ConsoleColor.Blue);
        var response = await client.GetAsync($"{serverUrl}/api/files");
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Failed to get file list from server: " + response.StatusCode);
        }
        var serverFiles = await response.Content.ReadFromJsonAsync<Dictionary<string, FileMetadata>>();
        if (serverFiles == null) serverFiles = new Dictionary<string, FileMetadata>();

        var flatServerFiles = ToFlatList(serverFiles)
            .ToDictionary(entry => entry.Path, entry => entry.Metadata);

        var localTree = FileHelper.GetFolderContent(cwd);
        var flatLocalEntries = ToFlatList(localTree).ToList();

        foreach (var localFolder in flatLocalEntries.Where(entry => !entry.Metadata.IsFile).OrderBy(entry => entry.Path))
        {
            if (flatServerFiles.TryGetValue(localFolder.Path, out var existing) && !existing.IsFile)
                continue;

            MessageColor($"Creating folder on server: {localFolder.Path}", ConsoleColor.DarkYellow);
            using var request = new HttpRequestMessage(HttpMethod.Put, $"{serverUrl}/api/files/{EncodePathForApi(localFolder.Path)}");
            request.Headers.Add("X-Type", "folder");
            request.Content = new ByteArrayContent(Array.Empty<byte>());
            var folderResponse = await client.SendAsync(request);
            if (!folderResponse.IsSuccessStatusCode)
            {
                MessageColor($"Failed to create folder {localFolder.Path} on server: {folderResponse.StatusCode}", ConsoleColor.Red);
            }
            else
            {
                MessageColor($"Successfully created folder on server: {localFolder.Path}", ConsoleColor.Green);
            }
        }

        foreach (var localFile in flatLocalEntries.Where(entry => entry.Metadata.IsFile))
        {
            MessageColor($"Pushing file: {localFile.Path}", ConsoleColor.Gray);
            using var fileStream = File.OpenRead(Path.Combine(cwd, localFile.Path));
            var content = new StreamContent(fileStream);
            var putResponse = await client.PutAsync($"{serverUrl}/api/files/{EncodePathForApi(localFile.Path)}", content);
            if (!putResponse.IsSuccessStatusCode)
            {
                MessageColor($"Failed to push file {localFile.Path}: " + putResponse.StatusCode, ConsoleColor.Red);
            }
            else
                MessageColor($"Successfully pushed file: {localFile.Path}", ConsoleColor.Green);
        }

        var localPaths = flatLocalEntries.Select(entry => entry.Path).ToHashSet();

        foreach (var serverFile in flatServerFiles.Where(entry => entry.Value.IsFile))
        {
            var filename = serverFile.Key;
            if (!localPaths.Contains(filename))
            {
                var deleteResponse = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(filename)}");
                if (!deleteResponse.IsSuccessStatusCode)
                {
                    MessageColor($"Failed to delete file {filename} from server: " + deleteResponse.StatusCode, ConsoleColor.Red);
                }
                else
                    MessageColor($"Deleted file from server: {filename}", ConsoleColor.DarkRed);
            }
        }

        foreach (var serverFile in flatServerFiles.Where(entry => !entry.Value.IsFile)
        .OrderByDescending(entry => entry.Key.Count(ch => ch == '/')))
        {
            var foldername = serverFile.Key;
            if (!localPaths.Contains(foldername))
            {
                var deleteResponse = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(foldername)}");
                if (!deleteResponse.IsSuccessStatusCode)
                {
                    MessageColor($"Failed to delete folder {foldername} from server: " + deleteResponse.StatusCode, ConsoleColor.Red);
                }
                else
                    MessageColor($"Deleted folder from server: {foldername}", ConsoleColor.DarkRed);
            }
        }
    }

    private static async Task Sync(HttpClient client, string serverUrl, string cwd)
    {
        MessageColor("Syncing: doing initial pull...", ConsoleColor.Blue);
        await Pull(client, serverUrl, cwd);

        var token = client.DefaultRequestHeaders.Authorization?.Parameter;

        var connection = new HubConnectionBuilder()
            .WithUrl($"{serverUrl}/api/events/signalr", options =>
            {
                if (!string.IsNullOrEmpty(token))
                    options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .WithAutomaticReconnect()
            .Build();

        int ignoringLocalChanges = 0;

        async Task Retry(Func<Task> action)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await action();
                    return;
                }
                catch (Exception ex)
                {
                    if (i == 2) throw;
                    MessageColor($"[Retry] Attempt {i + 1} failed: {ex.Message}", ConsoleColor.Yellow);
                    await Task.Delay(500);
                }
            }
        }

        var recentlyPushed = new Dictionary<string, DateTime>();
        var echoCooldownMs = 3000;

        void MarkAsPushed(string path)
        {
            lock (recentlyPushed) { recentlyPushed[path] = DateTime.UtcNow; }
        }

        bool IsEcho(string path)
        {
            lock (recentlyPushed)
            {
                if (recentlyPushed.TryGetValue(path, out var time)
                    && (DateTime.UtcNow - time).TotalMilliseconds < echoCooldownMs)
                    return true;
                recentlyPushed.Remove(path);
                return false;
            }
        }

        connection.On<int, string>("Event", async (eventType, filePath) =>
        {
            if (IsEcho(filePath)) return;

            var type = (EventsType)eventType;
            var localPath = Path.Combine(cwd, filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            Interlocked.Exchange(ref ignoringLocalChanges, 1);

            try
            {
                switch (type)
                {
                    case EventsType.FileCreated:
                    case EventsType.FileUpdated:
                        MessageColor($"[Server] File updated: {filePath}", ConsoleColor.DarkCyan);

                        await Retry(async () =>
                        {
                            var response = await client.GetAsync($"{serverUrl}/api/files/{EncodePathForApi(filePath)}");
                            response.EnsureSuccessStatusCode();

                            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                            var content = await response.Content.ReadAsByteArrayAsync();
                            await File.WriteAllBytesAsync(localPath, content);
                        });
                        break;

                    case EventsType.FileDeleted:
                        if (File.Exists(localPath))
                            File.Delete(localPath);
                        MessageColor($"[Server] File deleted: {filePath}", ConsoleColor.DarkRed);
                        break;

                    case EventsType.FolderCreated:
                        Directory.CreateDirectory(localPath);
                        MessageColor($"[Server] Folder created: {filePath}", ConsoleColor.DarkCyan);
                        break;

                    case EventsType.FolderDeleted:
                        if (Directory.Exists(localPath))
                        {
                            FileHelper.PrepareDirectoryForDelete(localPath);
                            Directory.Delete(localPath, true);
                        }
                        MessageColor($"[Server] Folder deleted: {filePath}", ConsoleColor.DarkRed);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageColor($"[Server] Error: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                await Task.Delay(500);
                Interlocked.Exchange(ref ignoringLocalChanges, 0);
            }
        });

        await connection.StartAsync();
        MessageColor("Connected to server.", ConsoleColor.Green);

        using var watcher = new FileSystemWatcher(cwd)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };

        var lastEvent = new Dictionary<string, DateTime>();
        int debounceMs = 1000;
        var changeSemaphore = new SemaphoreSlim(1, 1);

        var ignoredFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "desktop.ini", "Thumbs.db", ".DS_Store"
        };

        async Task HandleChange(string fullPath, WatcherChangeTypes type)
        {
            if (Volatile.Read(ref ignoringLocalChanges) == 1)
                return;

            await changeSemaphore.WaitAsync();
            try
            {
                var fileName = Path.GetFileName(fullPath);
                if (ignoredFiles.Contains(fileName))
                    return;

                var relative = Path.GetRelativePath(cwd, fullPath).Replace("\\", "/");
                var key = relative + type;
                var now = DateTime.UtcNow;

                lock (lastEvent)
                {
                    if (lastEvent.TryGetValue(key, out var last) &&
                        (now - last).TotalMilliseconds < debounceMs)
                        return;

                    lastEvent[key] = now;
                }

                try
                {
                    if (type == WatcherChangeTypes.Deleted)
                    {
                        MessageColor($"[Local] Deleted: {relative}", ConsoleColor.DarkRed);
                        MarkAsPushed(relative);
                        await Retry(() => client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(relative)}"));
                    }
                    else if (Directory.Exists(fullPath))
                    {
                        var folderAction = type == WatcherChangeTypes.Created ? "New folder" : "Updated folder";
                        var folderColor = type == WatcherChangeTypes.Created ? ConsoleColor.Green : ConsoleColor.Magenta;
                        MessageColor($"[Local] {folderAction}: {relative}", folderColor);
                        MarkAsPushed(relative);

                        await Retry(async () =>
                        {
                            using var request = new HttpRequestMessage(HttpMethod.Put,
                                $"{serverUrl}/api/files/{EncodePathForApi(relative)}");
                            request.Headers.Add("X-Type", "folder");
                            request.Content = new ByteArrayContent(Array.Empty<byte>());
                            await client.SendAsync(request);
                        });
                    }
                    else if (File.Exists(fullPath))
                    {
                        await Task.Delay(500);

                        if (!File.Exists(fullPath)) return;

                        var fileAction = type == WatcherChangeTypes.Created ? "New file" : "Updated file";
                        var fileColor = type == WatcherChangeTypes.Created ? ConsoleColor.Green : ConsoleColor.Magenta;
                        MessageColor($"[Local] {fileAction}: {relative}", fileColor);
                        MarkAsPushed(relative);

                        await Retry(async () =>
                        {
                            using var stream = File.OpenRead(fullPath);
                            using var request = new HttpRequestMessage(HttpMethod.Put,
                                $"{serverUrl}/api/files/{EncodePathForApi(relative)}");
                            request.Headers.Add("X-Type", "file");
                            request.Content = new StreamContent(stream);
                            await client.SendAsync(request);
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageColor($"[Local] Error: {ex.Message}", ConsoleColor.Red);
                }
            }
            finally
            {
                changeSemaphore.Release();
            }
        }

        watcher.Created += (_, e) => _ = HandleChange(e.FullPath, e.ChangeType);
        watcher.Changed += (_, e) => _ = HandleChange(e.FullPath, e.ChangeType);
        watcher.Deleted += (_, e) => _ = HandleChange(e.FullPath, e.ChangeType);

        watcher.Renamed += (_, e) =>
        {
            _ = Task.Run(async () =>
            {
                var oldPath = Path.GetRelativePath(cwd, e.OldFullPath).Replace("\\", "/");
                var newPath = Path.GetRelativePath(cwd, e.FullPath).Replace("\\", "/");

                Interlocked.Exchange(ref ignoringLocalChanges, 1);

                try
                {
                    MessageColor($"[Local] Rename: {oldPath} → {newPath}", ConsoleColor.White);

                    MarkAsPushed(oldPath);
                    MarkAsPushed(newPath);

                    await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(oldPath)}");

                    if (Directory.Exists(e.FullPath))
                    {
                        using var folderRequest = new HttpRequestMessage(HttpMethod.Put,
                            $"{serverUrl}/api/files/{EncodePathForApi(newPath)}");
                        folderRequest.Headers.Add("X-Type", "folder");
                        folderRequest.Content = new ByteArrayContent(Array.Empty<byte>());
                        await client.SendAsync(folderRequest);

                        foreach (var file in Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories))
                        {
                            var relFile = Path.GetRelativePath(cwd, file).Replace("\\", "/");
                            MarkAsPushed(relFile);
                            using var stream = File.OpenRead(file);
                            using var fileRequest = new HttpRequestMessage(HttpMethod.Put,
                                $"{serverUrl}/api/files/{EncodePathForApi(relFile)}");
                            fileRequest.Headers.Add("X-Type", "file");
                            fileRequest.Content = new StreamContent(stream);
                            await client.SendAsync(fileRequest);
                        }
                    }
                    else if (File.Exists(e.FullPath))
                    {
                        using var stream = File.OpenRead(e.FullPath);
                        using var fileRequest = new HttpRequestMessage(HttpMethod.Put,
                            $"{serverUrl}/api/files/{EncodePathForApi(newPath)}");
                        fileRequest.Headers.Add("X-Type", "file");
                        fileRequest.Content = new StreamContent(stream);
                        await client.SendAsync(fileRequest);
                    }
                }
                catch (Exception ex)
                {
                    MessageColor($"[Local] Rename error: {ex.Message}", ConsoleColor.Red);
                }
                finally
                {
                    await Task.Delay(500);
                    Interlocked.Exchange(ref ignoringLocalChanges, 0);
                }
            });
        };

        watcher.EnableRaisingEvents = true;

        MessageColor("Sync running... Ctrl+C to stop", ConsoleColor.Cyan);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (TaskCanceledException) { }

        MessageColor("Stopping sync...", ConsoleColor.Yellow);
        await connection.StopAsync();
    }

    private static IEnumerable<(string Path, FileMetadata Metadata)> ToFlatList(Dictionary<string, FileMetadata> source, string currentFolder = "")
    {
        foreach (var entry in source)
        {
            var currentPath = string.IsNullOrEmpty(currentFolder) ? entry.Key : $"{currentFolder}/{entry.Key}";
            var normalizedPath = currentPath.Replace("\\", "/");
            yield return (normalizedPath, entry.Value);

            if (entry.Value.Content != null)
            {
                foreach (var nested in ToFlatList(entry.Value.Content, normalizedPath))
                    yield return nested;
            }
        }
    }

    private static string NormalizeServerUrl(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = (url.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("127.0.0.1"))
                  ? $"http://{url}" : $"https://{url}";
        }
        return url.TrimEnd('/');
    }

    private static string EncodePathForApi(string relativePath)
    {
        return string.Join("/", relativePath
            .Replace("\\", "/")
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(Uri.EscapeDataString));
    }

    private static void MessageColor(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    class LoginResponse
    {
        public string Token { get; set; } = "";
    }
}