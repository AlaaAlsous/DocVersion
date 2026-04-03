class Program
{
    public static async Task<int> Main(string[] args)
    {
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