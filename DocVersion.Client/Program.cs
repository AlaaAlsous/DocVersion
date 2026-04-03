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