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

    public static void Start(string message) =>
        Console.WriteLine($"[{Timestamp()}] \u001b[36m→\u001b[0m {message}");

    public static void Box(string text)
    {
        var lines = text.Split('\n');
        var width = lines.Max(l => l.Length) + 4;
        Console.WriteLine($"\n\u001b[36m{new string('─', width)}\u001b[0m");
        foreach (var line in lines)
        {
            Console.WriteLine($"\u001b[36m│\u001b[0m \u001b[1m{line.PadRight(width - 3)}\u001b[0m\u001b[36m│\u001b[0m");
        }
        Console.WriteLine($"\u001b[36m{new string('─', width)}\u001b[0m\n");
    }

    public static void Query(string q) =>
        Console.WriteLine($"\n\u001b[44m\u001b[37m QUERY \u001b[0m {q}\n");

    public static void Api(string step, int messageCount) =>
        Console.WriteLine($"[{Timestamp()}] \u001b[35m◆\u001b[0m {step} ({messageCount} messages)");

    public static void ApiDone(UsageStats? usage)
    {
        if (usage != null)
        {
            Console.WriteLine($"         tokens: {usage.InputTokens} in / {usage.OutputTokens} out");
        }
    }

    public static void Tool(string name, object args)
    {
        var argStr = System.Text.Json.JsonSerializer.Serialize(args);
        var truncated = argStr.Length > 100 ? argStr[..100] + "..." : argStr;
        Console.WriteLine($"[{Timestamp()}] \u001b[33m⚡\u001b[0m {name} \u001b[2m{truncated}\u001b[0m");
    }

    public static void ToolResult(string name, bool success, string output)
    {
        var icon = success ? "\u001b[32m✓\u001b[0m" : "\u001b[31m✗\u001b[0m";
        var truncated = output.Length > 150 ? output[..150] + "..." : output;
        Console.WriteLine($"         {icon} {truncated}");
    }

    public static void Gemini(string action, string? detail = null)
    {
        Console.WriteLine($"[{Timestamp()}] \u001b[45m\u001b[37m GEMINI \u001b[0m {action}");
        if (detail != null)
            Console.WriteLine($"         {detail}");
    }

    public static void GeminiResult(bool success, string message)
    {
        var icon = success ? "\u001b[32m✓\u001b[0m" : "\u001b[31m✗\u001b[0m";
        Console.WriteLine($"         {icon} {message}");
    }
}
