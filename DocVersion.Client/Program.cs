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
}