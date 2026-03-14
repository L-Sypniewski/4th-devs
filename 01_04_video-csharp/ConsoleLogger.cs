namespace VideoAgent;

/// <summary>
/// Simple colored logger for terminal output.
/// </summary>
public static class ConsoleLogger
{
    private static string Timestamp() => DateTime.Now.ToString("HH:mm:ss");

    public static void Info(string message) =>
        Console.WriteLine($"[{Timestamp()}] {message}");

    public static void Success(string message) =>
        Console.WriteLine($"[{Timestamp()}] \u001b[32m✓\u001b[0m {message}");

    public static void Error(string title, string? message = null) =>
        Console.WriteLine($"[{Timestamp()}] \u001b[31m✗ {title}\u001b[0m {message ?? ""}");

    public static void Warn(string message) =>
        Console.WriteLine($"[{Timestamp()}] \u001b[33m⚠\u001b[0m {message}");

    public static void Tool(string name, object args)
    {
        var argStr = System.Text.Json.JsonSerializer.Serialize(args);
        var truncated = argStr.Length > 80 ? argStr[..80] + "..." : argStr;
        Console.WriteLine($"[{Timestamp()}] \u001b[33m⚡\u001b[0m {name} \u001b[2m{truncated}\u001b[0m");
    }

    public static void Gemini(string action, string? detail = null)
    {
        Console.WriteLine($"[{Timestamp()}] \u001b[45m\u001b[37m GEMINI \u001b[0m {action}");
        if (detail != null)
            Console.WriteLine($"         {detail}");
    }

    public static void Box(string title)
    {
        var width = Math.Max(title.Length + 4, 40);
        Console.WriteLine($"\n\u001b[36m{new string('─', width)}\u001b[0m");
        Console.WriteLine($"\u001b[36m│\u001b[0m \u001b[1m{title.PadRight(width - 3)}\u001b[0m\u001b[36m│\u001b[0m");
        Console.WriteLine($"\u001b[36m{new string('─', width)}\u001b[0m\n");
    }
}
