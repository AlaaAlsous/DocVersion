using System.Net.Http.Json;
using DocVersion.Core.Helpers;
using DocVersion.Core.Models;
using System.Net.Http.Headers;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 4)
        {
            MessageColor("Usage: DocVersion.Client [pull|push] <serverUrl> <username> <password>", ConsoleColor.Red);
            return 1;
        }
        var command = args[0].ToLower();
        var serverUrl = NormalizeServerUrl(args[1]);
        var username = args[2];
        var password = args[3];
        var cwd = Directory.GetCurrentDirectory();

        MessageColor("Working directory: " + cwd, ConsoleColor.Cyan);

        try
        {
            using var client = new HttpClient();


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
                Console.ResetColor();
                return 1;
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (command == "pull")
            {
                await Pull(client, serverUrl, cwd);
            }
            else if (command == "push")
            {
                await Push(client, serverUrl, cwd);
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
        MessageColor("Pulling files from server...", ConsoleColor.DarkGreen);
        var response = await client.GetAsync($"{serverUrl}/api/files");
        if (!response.IsSuccessStatusCode)
        {
            MessageColor("Failed to pull files: " + response.StatusCode, ConsoleColor.Red);
            return;
        }
        var files = await response.Content.ReadFromJsonAsync<Dictionary<string, FileMetadata>>();
        if (files == null)
        {
            MessageColor("No files found on server.", ConsoleColor.Yellow);
            return;
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
                MessageColor($"Pulling file: {filename} ({metadata.Bytes} bytes)", ConsoleColor.White);
                var fileResponse = await client.GetAsync($"{serverUrl}/api/files/{EncodePathForApi(filename)}");
                if (!fileResponse.IsSuccessStatusCode)
                {
                    MessageColor($"Failed to pull file {filename}: " + fileResponse.StatusCode, ConsoleColor.Red);
                    continue;
                }
                var content = await fileResponse.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(Path.GetDirectoryName(localPath) ?? "");
                await File.WriteAllBytesAsync(localPath, content);
            }
            else
            {
                if (!Directory.Exists(localPath))
                {
                    MessageColor($"Creating folder: {filename}", ConsoleColor.DarkGray);
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

            if (!Directory.EnumerateFileSystemEntries(localDir).Any())
            {
                try
                {
                    Directory.Delete(localDir);
                    MessageColor($"Deleted folder: {localDir}", ConsoleColor.DarkRed);
                }
                catch (Exception ex)
                {
                    MessageColor($"Could not delete folder {localDir}: {ex.Message}", ConsoleColor.Red);
                }
            }
        }
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
            || url.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase)
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

    private static string MessageColor(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
        return message;
    }

    class LoginResponse
    {
        public string Token { get; set; } = "";
    }
}