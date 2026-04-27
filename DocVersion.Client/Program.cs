using System.Net.Http.Json;
using System.Text.Json;
using DocVersion.Core.Helpers;
using System.Collections.Concurrent;
using DocVersion.Core.Models;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.SignalR.Client;

class Program
{
    private static readonly Dictionary<string, DateTime> echoCache = new();
    private static readonly object echoLock = new();

    private static DateTime lastLocalDelete = DateTime.MinValue;
    private static readonly HashSet<string> processedDeletes = new();
    private static readonly object deleteLock = new();

    private static readonly ConcurrentQueue<string> pendingDeletes = new();
    private static readonly ConcurrentDictionary<string, byte> pendingSet = new();

    private static readonly Queue<(Func<Task> Op, int Attempts, string? Description)> failedOps = new();
    private static readonly object failedOpsLock = new();

    private const int BATCH_SIZE = 200;
    private const int BATCH_INTERVAL_MS = 500;
    private const int MAX_PARALLEL_DELETES = 8;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        if (args.Length < 2)
        {
            MessageColor("Usage: DocVersion.Client [pull|push|sync] <serverUrl> [username] [password]", ConsoleColor.Red);
            return 1;
        }

        var command = args[0].ToLowerInvariant();
        var serverUrl = NormalizeServerUrl(args[1]);
        var username = args.Length > 2 ? args[2] : null;
        var password = args.Length > 3 ? args[3] : null;
        var cwd = Directory.GetCurrentDirectory();

        MessageColor("Working directory: " + cwd, ConsoleColor.Cyan);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

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

            switch (command)
            {
                case "pull":
                    await Pull(client, serverUrl, cwd);
                    break;

                case "push":
                    await Push(client, serverUrl, cwd);
                    break;

                case "sync":
                    await Sync(client, serverUrl, cwd);
                    break;

                default:
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
            throw new Exception("Failed to pull files: " + response.StatusCode);

        var files = await response.Content.ReadFromJsonAsync<Dictionary<string, FileMetadata>>()
                   ?? new Dictionary<string, FileMetadata>();

        var flatServerFiles = ToFlatList(files).ToDictionary(entry => entry.Path, entry => entry.Metadata);

        foreach (var file in flatServerFiles.OrderBy(entry => entry.Value.IsFile))
        {
            var filename = file.Key;
            var metadata = file.Value;
            var localPath = Path.Combine(cwd, filename.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (metadata.IsFile)
            {
                MessageColor($"Pulling file: {filename} ({metadata.Bytes} bytes)", ConsoleColor.Gray);
                var fileResponse = await client.GetAsync($"{serverUrl}/api/files/{EncodePathForApi(filename)}");
                if (!fileResponse.IsSuccessStatusCode)
                {
                    MessageColor($"Failed to pull file {filename}: " + fileResponse.StatusCode, ConsoleColor.Red);
                    continue;
                }

                var content = await fileResponse.Content.ReadAsByteArrayAsync();
                EnsureDirectoryExists(localPath, cwd);
                await File.WriteAllBytesAsync(localPath, content);
                MessageColor($"Successfully pulled file: {filename}", ConsoleColor.Green);
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
            if (flatServerFiles.ContainsKey(relativePath))
                continue;

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
            throw new Exception("Failed to get file list from server: " + response.StatusCode);

        var serverFiles = await response.Content.ReadFromJsonAsync<Dictionary<string, FileMetadata>>()
                         ?? new Dictionary<string, FileMetadata>();

        var flatServerFiles = ToFlatList(serverFiles).ToDictionary(entry => entry.Path, entry => entry.Metadata);

        var localTree = FileHelper.GetFolderContent(cwd);
        var flatLocalEntries = ToFlatList(localTree).ToList();

        foreach (var localFolder in flatLocalEntries.Where(entry => !entry.Metadata.IsFile).OrderBy(entry => entry.Path))
        {
            if (flatServerFiles.TryGetValue(localFolder.Path, out var existing) && !existing.IsFile)
                continue;

            MessageColor($"Creating folder on server: {localFolder.Path}", ConsoleColor.DarkYellow);

            using var request = new HttpRequestMessage(HttpMethod.Put, $"{serverUrl}/api/files/{EncodePathForApi(localFolder.Path)}");
            request.Headers.Add("X-Type", "folder");
            var content = new ByteArrayContent(Array.Empty<byte>());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content = content;

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
            var fullLocalPath = Path.Combine(cwd, localFile.Path.Replace("/", Path.DirectorySeparatorChar.ToString()));

            try
            {
                using var fileStream = File.OpenRead(fullLocalPath);
                using var request = new HttpRequestMessage(HttpMethod.Put, $"{serverUrl}/api/files/{EncodePathForApi(localFile.Path)}");
                request.Headers.Add("X-Type", "file");
                var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                request.Content = streamContent;

                var putResponse = await client.SendAsync(request);
                if (!putResponse.IsSuccessStatusCode)
                {
                    MessageColor($"Failed to push file {localFile.Path}: " + putResponse.StatusCode, ConsoleColor.Red);
                }
                else
                {
                    MessageColor($"Successfully pushed file: {localFile.Path}", ConsoleColor.Green);
                }
            }
            catch (Exception ex)
            {
                MessageColor($"Failed to push file {localFile.Path}: {ex.Message}", ConsoleColor.Red);
            }
        }

        var localPaths = flatLocalEntries.Select(entry => entry.Path).ToHashSet();

        foreach (var serverFile in flatServerFiles.Where(entry => entry.Value.IsFile))
        {
            var filename = serverFile.Key;
            if (localPaths.Contains(filename))
                continue;

            MarkEcho(filename);
            var deleteResponse = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(filename)}");
            if (!deleteResponse.IsSuccessStatusCode)
            {
                MessageColor($"Failed to delete file {filename} from server: " + deleteResponse.StatusCode, ConsoleColor.Red);
            }
            else
            {
                MessageColor($"Deleted file from server: {filename}", ConsoleColor.DarkRed);
            }
        }

        foreach (var serverFile in flatServerFiles.Where(entry => !entry.Value.IsFile)
                     .OrderByDescending(entry => entry.Key.Count(ch => ch == '/')))
        {
            var foldername = serverFile.Key;
            if (localPaths.Contains(foldername))
                continue;

            var deleteResponse = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(foldername)}");
            if (!deleteResponse.IsSuccessStatusCode)
            {
                MessageColor($"Failed to delete folder {foldername} from server: " + deleteResponse.StatusCode, ConsoleColor.Red);
            }
            else
            {
                MessageColor($"Deleted folder from server: {foldername}", ConsoleColor.DarkRed);
            }
        }
    }

    private static async Task Sync(HttpClient client, string serverUrl, string cwd)
    {
        var cts = new CancellationTokenSource();

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

        var processFailedOpsTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                (Func<Task> Op, int Attempts, string? Description)? item = null;

                lock (failedOpsLock)
                {
                    if (failedOps.Count > 0)
                        item = failedOps.Dequeue();
                }

                if (item is null)
                {
                    try
                    {
                        await Task.Delay(1000, cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }

                    continue;
                }

                var (op, attempts, description) = item.Value;

                try
                {
                    await op();
                    MessageColor($"[Retry] Success{(description != null ? $" for {description}" : "")}", ConsoleColor.Green);
                }
                catch (Exception ex)
                {
                    if (attempts >= 10)
                    {
                        MessageColor($"[Retry] Failed after 10 attempts{(description != null ? $" for {description}" : "")}: {ex.Message}", ConsoleColor.Red);
                        continue;
                    }

                    var delay = Math.Min((int)Math.Pow(2, attempts) * 1000, 30000);
                    MessageColor($"[Retry] Attempt {attempts + 1} failed{(description != null ? $" for {description}" : "")}. Retrying in {delay} ms", ConsoleColor.Yellow);

                    try
                    {
                        await Task.Delay(delay, cts.Token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }

                    lock (failedOpsLock)
                    {
                        failedOps.Enqueue((op, attempts + 1, description));
                    }
                }
            }
        }, cts.Token);

        void EnqueueFailed(Func<Task> op, string? description = null)
        {
            lock (failedOpsLock)
            {
                failedOps.Enqueue((op, 0, description));
            }
        }

        var deleteLimiter = new SemaphoreSlim(MAX_PARALLEL_DELETES, MAX_PARALLEL_DELETES);

        var batchProcessorTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(BATCH_INTERVAL_MS, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }

                var batch = new List<string>();
                while (batch.Count < BATCH_SIZE && pendingDeletes.TryDequeue(out var item))
                {
                    if (pendingSet.TryRemove(item, out _))
                        batch.Add(item);
                }

                if (batch.Count == 0)
                    continue;

                var tasks = new List<Task>();
                foreach (var path in batch)
                {
                    await deleteLimiter.WaitAsync(cts.Token);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            try
                            {
                                var resp = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(path)}", cts.Token);
                                if (resp.IsSuccessStatusCode || resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    MessageColor($"[Local->Server] Deleted: {path} (Status: {resp.StatusCode})", ConsoleColor.DarkRed);
                                }
                                else
                                {
                                    var body = await resp.Content.ReadAsStringAsync();
                                    MessageColor($"[Local->Server] Delete failed {path}: {resp.StatusCode} - {body}", ConsoleColor.Yellow);

                                    EnqueueFailed(async () =>
                                    {
                                        var r = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(path)}", cts.Token);
                                        if (!r.IsSuccessStatusCode && r.StatusCode != System.Net.HttpStatusCode.NotFound)
                                            throw new Exception($"Delete failed: {r.StatusCode}");
                                    }, path);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageColor($"[Local->Server] Delete exception {path}: {ex.Message}", ConsoleColor.Yellow);

                                EnqueueFailed(async () =>
                                {
                                    var r = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(path)}", cts.Token);
                                    if (!r.IsSuccessStatusCode && r.StatusCode != System.Net.HttpStatusCode.NotFound)
                                        throw new Exception($"Delete failed: {r.StatusCode}");
                                }, path);
                            }
                        }
                        finally
                        {
                            deleteLimiter.Release();
                        }
                    }, cts.Token));
                }

                try
                {
                    await Task.WhenAll(tasks);
                }
                catch { }
            }
        }, cts.Token);

        var deleteVerifierTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(3000, cts.Token);

                    if (!pendingDeletes.IsEmpty)
                        continue;

                    var response = await client.GetAsync($"{serverUrl}/api/files", cts.Token);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    var serverFiles = await response.Content.ReadFromJsonAsync<Dictionary<string, FileMetadata>>(cancellationToken: cts.Token);
                    if (serverFiles == null)
                        continue;

                    var flatServer = ToFlatList(serverFiles).Select(x => x.Path).ToHashSet();

                    lock (deleteLock)
                    {
                        foreach (var deleted in processedDeletes.ToList())
                        {
                            if (flatServer.Contains(deleted))
                            {
                                if (pendingSet.TryAdd(deleted, 0))
                                    pendingDeletes.Enqueue(deleted);

                                MessageColor($"[Verifier] Server missed delete → retry: {deleted}", ConsoleColor.Yellow);
                            }
                            else
                            {
                                processedDeletes.Remove(deleted);
                            }
                        }
                    }
                }
                catch { }
            }
        }, cts.Token);

        var reconcileTask = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, cts.Token);
                    await ReconcileServerWithLocalAsync(client, serverUrl, cwd, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    MessageColor($"[Reconcile] Error: {ex.Message}", ConsoleColor.Yellow);
                }
            }
        }, cts.Token);

        connection.On<int, object>("Event", async (eventType, payload) =>
        {
            string? filePath = null;
            string? oldName = null;
            string? newName = null;
            var type = (EventsType)eventType;

            Interlocked.Exchange(ref ignoringLocalChanges, 1);

            try
            {
                switch (type)
                {
                    case EventsType.FileCreated:
                    case EventsType.FileUpdated:
                        {
                            filePath = GetString(payload);

                            if (string.IsNullOrWhiteSpace(filePath))
                                break;

                            if (IsEcho(filePath))
                                break;

                            var localPath = Path.Combine(Directory.GetCurrentDirectory(), filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                            MessageColor($"[Server] File updated: {filePath}", ConsoleColor.DarkCyan);

                            try
                            {
                                var response = await client.GetAsync($"{serverUrl}/api/files/{EncodePathForApi(filePath)}");
                                response.EnsureSuccessStatusCode();
                                EnsureDirectoryExists(localPath, Directory.GetCurrentDirectory());
                                var content = await response.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(localPath, content);
                                MarkEcho(filePath);
                            }
                            catch (Exception ex)
                            {
                                MessageColor($"[Server] File download failed, adding to queue: {ex.Message}", ConsoleColor.Yellow);

                                var capturedPath = filePath;
                                var capturedLocal = localPath;
                                EnqueueFailed(async () =>
                                {
                                    var response = await client.GetAsync($"{serverUrl}/api/files/{EncodePathForApi(capturedPath)}");
                                    response.EnsureSuccessStatusCode();
                                    EnsureDirectoryExists(capturedLocal, Directory.GetCurrentDirectory());
                                    var content = await response.Content.ReadAsByteArrayAsync();
                                    await File.WriteAllBytesAsync(capturedLocal, content);
                                    MarkEcho(capturedPath);
                                }, capturedPath);
                            }

                            break;
                        }

                    case EventsType.FileDeleted:
                        {
                            filePath = GetString(payload);
                            if (string.IsNullOrEmpty(filePath))
                                break;

                            bool alreadyProcessed;
                            lock (deleteLock)
                            {
                                alreadyProcessed = processedDeletes.Contains(filePath);
                                if (!alreadyProcessed)
                                    processedDeletes.Add(filePath);
                            }

                            if (alreadyProcessed)
                            {
                                MessageColor($"[Server] Already processed delete: {filePath}", ConsoleColor.DarkGray);
                                break;
                            }

                            if (IsEcho(filePath))
                                break;

                            if ((DateTime.UtcNow - lastLocalDelete).TotalMilliseconds < 1500)
                                break;

                            var localPath = Path.Combine(Directory.GetCurrentDirectory(), filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

                            try
                            {
                                if (File.Exists(localPath))
                                {
                                    File.SetAttributes(localPath, FileAttributes.Normal);
                                    File.Delete(localPath);
                                    MessageColor($"[Server] File deleted: {filePath}", ConsoleColor.DarkRed);
                                }
                                else
                                {
                                    MessageColor($"[Server] File already deleted: {filePath}", ConsoleColor.DarkGray);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageColor($"[Server] Delete failed, queued: {filePath}: {ex.Message}", ConsoleColor.Yellow);

                                var capturedPath = filePath;
                                var capturedLocal = localPath;
                                EnqueueFailed(async () =>
                                {
                                    if (File.Exists(capturedLocal))
                                    {
                                        File.SetAttributes(capturedLocal, FileAttributes.Normal);
                                        File.Delete(capturedLocal);
                                        MessageColor($"[Retry] File deleted: {capturedPath}", ConsoleColor.Green);
                                    }

                                    await Task.CompletedTask;
                                }, capturedPath);
                            }

                            break;
                        }

                    case EventsType.FolderCreated:
                        {
                            var data = GetString(payload);
                            if (string.IsNullOrEmpty(data))
                                break;

                            filePath = data;
                            if (IsEcho(filePath))
                                break;

                            var localPath = Path.Combine(Directory.GetCurrentDirectory(), filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                            Directory.CreateDirectory(localPath);
                            MessageColor($"[Server] Folder created: {filePath}", ConsoleColor.DarkCyan);
                            MarkEcho(filePath);
                            break;
                        }

                    case EventsType.FolderDeleted:
                        {
                            filePath = GetString(payload);
                            if (string.IsNullOrEmpty(filePath))
                                break;

                            if ((DateTime.UtcNow - lastLocalDelete).TotalSeconds < 1)
                                break;

                            if (IsEcho(filePath))
                                break;

                            var localPath = Path.Combine(Directory.GetCurrentDirectory(), filePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

                            try
                            {
                                if (Directory.Exists(localPath))
                                {
                                    FileHelper.PrepareDirectoryForDelete(localPath);
                                    Directory.Delete(localPath, true);
                                    MessageColor($"[Server] Folder deleted: {filePath}", ConsoleColor.DarkRed);
                                }
                                else
                                {
                                    MessageColor($"[Server] Folder already deleted: {filePath}", ConsoleColor.DarkGray);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageColor($"[Server] Folder delete failed, queued: {filePath}: {ex.Message}", ConsoleColor.Yellow);

                                var capturedPath = filePath;
                                var capturedLocal = localPath;
                                EnqueueFailed(async () =>
                                {
                                    if (Directory.Exists(capturedLocal))
                                    {
                                        FileHelper.PrepareDirectoryForDelete(capturedLocal);
                                        Directory.Delete(capturedLocal, true);
                                        MessageColor($"[Retry] Folder deleted: {capturedPath}", ConsoleColor.Green);
                                    }

                                    await Task.CompletedTask;
                                }, capturedPath);
                            }

                            break;
                        }

                    case EventsType.FolderRenamed:
                    case EventsType.FileRenamed:
                        {
                            var data = GetString(payload);
                            if (string.IsNullOrWhiteSpace(data))
                            {
                                MessageColor("[Server] Rename event missing data", ConsoleColor.Red);
                                break;
                            }

                            var parts = data.Split('|', 2);
                            if (parts.Length != 2)
                            {
                                MessageColor("[Server] Invalid rename format", ConsoleColor.Red);
                                break;
                            }

                            oldName = parts[0];
                            newName = parts[1];

                            if (IsEcho(oldName) || IsEcho(newName))
                                break;

                            var oldPath = Path.Combine(Directory.GetCurrentDirectory(), oldName.Replace("/", Path.DirectorySeparatorChar.ToString()));
                            var newPath = Path.Combine(Directory.GetCurrentDirectory(), newName.Replace("/", Path.DirectorySeparatorChar.ToString()));

                            async Task RenameOperation()
                            {
                                if (!File.Exists(oldPath) && !Directory.Exists(oldPath))
                                {
                                    MessageColor($"[Server] Rename source not found: {oldName}", ConsoleColor.Yellow);
                                    return;
                                }

                                EnsureDirectoryExists(newPath, Directory.GetCurrentDirectory());

                                if (File.Exists(oldPath))
                                {
                                    SafeMoveFile(oldPath, newPath);
                                    MessageColor($"[Server] File renamed: {oldName} → {newName}", ConsoleColor.Yellow);
                                }
                                else if (Directory.Exists(oldPath))
                                {
                                    Directory.Move(oldPath, newPath);
                                    MessageColor($"[Server] Folder renamed: {oldName} → {newName}", ConsoleColor.Yellow);
                                }

                                MarkEcho(oldName);
                                MarkEcho(newName);
                            }

                            try
                            {
                                await RenameOperation();
                            }
                            catch (Exception ex)
                            {
                                MessageColor($"[Server] Rename failed, queued: {ex.Message}", ConsoleColor.Red);

                                EnqueueFailed(async () => { await RenameOperation(); }, newName);
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                MessageColor($"[Server] Error: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                try { await Task.Delay(500); } catch { }
                Interlocked.Exchange(ref ignoringLocalChanges, 0);
            }
        });

        try
        {
            try
            {
                await connection.StartAsync(cts.Token);
                MessageColor("Connected to server.", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                MessageColor($"Could not start SignalR connection: {ex.Message}", ConsoleColor.Red);
                EnqueueFailed(async () => await connection.StartAsync(cts.Token), "SignalR start");
            }

            using var watcher = new FileSystemWatcher(Directory.GetCurrentDirectory())
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
            };

            var lastEvent = new Dictionary<string, DateTime>();
            const int debounceMs = 1000;
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

                    var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace("\\", "/");
                    var key = relative + type + (File.Exists(fullPath) ? File.GetLastWriteTimeUtc(fullPath).Ticks : 0);
                    var now = DateTime.UtcNow;

                    lock (lastEvent)
                    {
                        if (type != WatcherChangeTypes.Deleted &&
                            lastEvent.TryGetValue(key, out var last) &&
                            (now - last).TotalMilliseconds < debounceMs)
                        {
                            return;
                        }

                        lastEvent[key] = now;
                    }

                    try
                    {
                        if (type == WatcherChangeTypes.Deleted)
                        {
                            if (pendingSet.TryAdd(relative, 0))
                                pendingDeletes.Enqueue(relative);

                            lock (deleteLock)
                            {
                                processedDeletes.Add(relative);
                            }

                            MessageColor($"[Local] Deleted (queued): {relative}", ConsoleColor.DarkRed);

                            lastLocalDelete = DateTime.UtcNow;
                        }
                        else if (Directory.Exists(fullPath))
                        {
                            var folderAction = type == WatcherChangeTypes.Created ? "New folder" : "Updated folder";
                            var folderColor = type == WatcherChangeTypes.Created ? ConsoleColor.Green : ConsoleColor.Magenta;
                            MessageColor($"[Local] {folderAction}: {relative}", folderColor);

                            try
                            {
                                using var request = new HttpRequestMessage(HttpMethod.Put,
                                    $"{serverUrl}/api/files/{EncodePathForApi(relative)}");
                                request.Headers.Add("X-Type", "folder");
                                var content = new ByteArrayContent(Array.Empty<byte>());
                                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                request.Content = content;
                                await client.SendAsync(request);
                            }
                            catch (Exception ex)
                            {
                                MessageColor($"[Local] Folder operation failed, adding to queue: {ex.Message}", ConsoleColor.Yellow);

                                var capturedRelative = relative;
                                EnqueueFailed(async () =>
                                {
                                    using var request = new HttpRequestMessage(HttpMethod.Put,
                                        $"{serverUrl}/api/files/{EncodePathForApi(capturedRelative)}");
                                    request.Headers.Add("X-Type", "folder");
                                    var content = new ByteArrayContent(Array.Empty<byte>());
                                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                    request.Content = content;
                                    await client.SendAsync(request);
                                }, capturedRelative);
                            }
                        }
                        else if (File.Exists(fullPath))
                        {
                            await Task.Delay(500);
                            if (!File.Exists(fullPath))
                                return;

                            var fileAction = type == WatcherChangeTypes.Created ? "New file" : "Updated file";
                            var fileColor = type == WatcherChangeTypes.Created ? ConsoleColor.Green : ConsoleColor.Magenta;
                            MessageColor($"[Local] {fileAction}: {relative}", fileColor);

                            try
                            {
                                using var stream = File.OpenRead(fullPath);
                                using var request = new HttpRequestMessage(HttpMethod.Put,
                                    $"{serverUrl}/api/files/{EncodePathForApi(relative)}");
                                request.Headers.Add("X-Type", "file");
                                var streamContent = new StreamContent(stream);
                                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                request.Content = streamContent;
                                await client.SendAsync(request);
                            }
                            catch (Exception ex)
                            {
                                MessageColor($"[Local] File operation failed, adding to queue: {ex.Message}", ConsoleColor.Yellow);

                                var capturedRelative = relative;
                                var capturedPath = fullPath;
                                EnqueueFailed(async () =>
                                {
                                    using var stream = File.OpenRead(capturedPath);
                                    using var request = new HttpRequestMessage(HttpMethod.Put,
                                        $"{serverUrl}/api/files/{EncodePathForApi(capturedRelative)}");
                                    request.Headers.Add("X-Type", "file");
                                    var streamContent = new StreamContent(stream);
                                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                    request.Content = streamContent;
                                    await client.SendAsync(request);
                                }, capturedRelative);
                            }
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
                    var oldPathRel = Path.GetRelativePath(Directory.GetCurrentDirectory(), e.OldFullPath).Replace("\\", "/");
                    var newPathRel = Path.GetRelativePath(Directory.GetCurrentDirectory(), e.FullPath).Replace("\\", "/");

                    Interlocked.Exchange(ref ignoringLocalChanges, 1);

                    try
                    {
                        MessageColor($"[Local] Rename: {oldPathRel} → {newPathRel}", ConsoleColor.White);

                        await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(oldPathRel)}");

                        if (Directory.Exists(e.FullPath))
                        {
                            using var folderRequest = new HttpRequestMessage(HttpMethod.Put,
                                $"{serverUrl}/api/files/{EncodePathForApi(newPathRel)}");
                            folderRequest.Headers.Add("X-Type", "folder");
                            var content = new ByteArrayContent(Array.Empty<byte>());
                            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                            folderRequest.Content = content;
                            await client.SendAsync(folderRequest);

                            foreach (var file in Directory.GetFiles(e.FullPath, "*", SearchOption.AllDirectories))
                            {
                                var relFile = Path.GetRelativePath(Directory.GetCurrentDirectory(), file).Replace("\\", "/");

                                using var stream = File.OpenRead(file);
                                using var fileRequest = new HttpRequestMessage(HttpMethod.Put,
                                    $"{serverUrl}/api/files/{EncodePathForApi(relFile)}");
                                fileRequest.Headers.Add("X-Type", "file");
                                var streamContent = new StreamContent(stream);
                                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                                fileRequest.Content = streamContent;
                                await client.SendAsync(fileRequest);
                            }
                        }
                        else if (File.Exists(e.FullPath))
                        {
                            using var stream = File.OpenRead(e.FullPath);
                            using var fileRequest = new HttpRequestMessage(HttpMethod.Put,
                                $"{serverUrl}/api/files/{EncodePathForApi(newPathRel)}");
                            fileRequest.Headers.Add("X-Type", "file");
                            var streamContent = new StreamContent(stream);
                            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                            fileRequest.Content = streamContent;
                            await client.SendAsync(fileRequest);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageColor($"[Local] Rename error: {ex.Message}", ConsoleColor.Red);
                    }
                    finally
                    {
                        try { await Task.Delay(500); } catch { }
                        Interlocked.Exchange(ref ignoringLocalChanges, 0);
                    }
                });
            };

        watcher.EnableRaisingEvents = true;

        MessageColor("Sync running... Ctrl+C to stop", ConsoleColor.Cyan);


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
            try { await connection.DisposeAsync(); } catch { }

            try
            {
                await Task.WhenAll(batchProcessorTask, processFailedOpsTask);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static string? GetString(object? payload)
    {
        if (payload is string s)
            return s;

        if (payload is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                JsonValueKind.Object => el.GetRawText(),
                JsonValueKind.Array => el.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => el.GetRawText()
            };
        }

        return payload?.ToString();
    }

    private static IEnumerable<(string Path, FileMetadata Metadata)> ToFlatList(
        Dictionary<string, FileMetadata> source,
        string currentFolder = "")
    {
        foreach (var entry in source)
        {
            var currentPath = string.IsNullOrEmpty(currentFolder) ? entry.Key : $"{currentFolder}/{entry.Key}";
            var normalizedPath = currentPath.Replace("\\", "/");
            yield return (normalizedPath, entry.Value);

            if (entry.Value.Content is null)
                continue;

            foreach (var nested in ToFlatList(entry.Value.Content, normalizedPath))
                yield return nested;
        }
    }

    private static string NormalizeServerUrl(string url)
    {
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = (url.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                ? $"http://{url}"
                : $"https://{url}";
        }
        return url.TrimEnd('/');
    }

    private static string EncodePathForApi(string relativePath)
    {
        return string.Join("/",
            relativePath
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

    private sealed class LoginResponse
    {
        public string Token { get; set; } = "";
    }

    private static void EnsureDirectoryExists(string filePath, string cwd)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(dir))
            dir = cwd;

        Directory.CreateDirectory(dir);
    }

    private static void SafeMoveFile(string source, string destination)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException($"Source file not found: {source}");

        var destDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        try
        {
            File.Move(source, destination, overwrite: true);
        }
        catch (IOException ex)
        {
            if (File.Exists(destination))
            {
                try
                {
                    File.Delete(destination);
                    File.Move(source, destination);
                }
                catch (Exception retryEx)
                {
                    throw new IOException(
                        $"Failed to overwrite '{destination}' after retry: {retryEx.Message}",
                        retryEx);
                }
            }
            else
            {
                throw new IOException(
                    $"IO error moving '{source}' to '{destination}': {ex.Message}",
                    ex);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                $"Permission denied moving '{source}' to '{destination}': {ex.Message}",
                ex);
        }
    }

    private static async Task ReconcileServerWithLocalAsync(HttpClient client, string serverUrl, string cwd, CancellationToken token)
    {
        var resp = await client.GetAsync($"{serverUrl}/api/files", token);
        if (!resp.IsSuccessStatusCode)
            return;

        var serverTree = await resp.Content.ReadFromJsonAsync<Dictionary<string, FileMetadata>>(cancellationToken: token)
                        ?? new Dictionary<string, FileMetadata>();

        var flatServerFiles = ToFlatList(serverTree)
            .Where(x => x.Metadata.IsFile)
            .Select(x => x.Path)
            .ToHashSet();

        var localFiles = Directory.GetFiles(cwd, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(cwd, p).Replace("\\", "/"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var serverFile in flatServerFiles)
        {
            if (localFiles.Contains(serverFile))
                continue;

            try
            {
                var delResp = await client.DeleteAsync($"{serverUrl}/api/files/{EncodePathForApi(serverFile)}", token);
                if (delResp.IsSuccessStatusCode || delResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    MessageColor($"[Reconcile] Deleted extra server file: {serverFile}", ConsoleColor.DarkRed);
                }
                else
                {
                    var body = await delResp.Content.ReadAsStringAsync(token);
                    MessageColor($"[Reconcile] Failed delete {serverFile}: {delResp.StatusCode} - {body}", ConsoleColor.Yellow);
                }
            }
            catch (Exception ex)
            {
                MessageColor($"[Reconcile] Exception deleting {serverFile}: {ex.Message}", ConsoleColor.Yellow);
            }
        }
    }
}