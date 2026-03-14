namespace AudioAgent.Helpers;

/// <summary>
/// Simple colored console logger for terminal output.
/// </summary>
public static class ConsoleLogger
{
    private static string Timestamp() => DateTime.Now.ToString("HH:mm:ss");

    public static void Info(string message) =>
        Console.WriteLine($"[{Timestamp()}] \x1b[2m{message}\x1b[0m");

    public static void Success(string message) =>
        Console.WriteLine($"[{Timestamp()}] \x1b[32m✓\x1b[0m {message}");

    public static void Error(string title, string? message = null) =>
        Console.WriteLine($"[{Timestamp()}] \x1b[31m✗ {title}\x1b[0m {message ?? ""}");

    public static void Warn(string message) =>
        Console.WriteLine($"[{Timestamp()}] \x1b[33m⚠\x1b[0m {message}");

    public static void Start(string message) =>
        Console.WriteLine($"[{Timestamp()}] \x1b[36m→\x1b[0m {message}");

    public static void Box(string text)
    {
        var lines = text.Split('\n');
        var width = lines.Max(l => l.Length) + 4;
        Console.WriteLine($"\n\x1b[36m{new string('─', width)}\x1b[0m");
        foreach (var line in lines)
        {
            Console.WriteLine($"\x1b[36m│\x1b[0m \x1b[1m{line.PadRight(width - 3)}\x1b[0m\x1b[36m│\x1b[0m");
        }
        Console.WriteLine($"\x1b[36m{new string('─', width)}\x1b[0m\n");
    }

    public static void Heading(string title, string? description = null)
    {
        Console.WriteLine($"\n\x1b[1m═══ {title} ═══\x1b[0m");
        if (description != null)
            Console.WriteLine($"\x1b[2m{description}\x1b[0m");
    }

    public static void Example(string text) =>
        Console.WriteLine($"  \x1b[32m→\x1b[0m \x1b[1m{text}\x1b[0m");

    public static void Hint(string text) =>
        Console.WriteLine($"\n\x1b[2m{text}\x1b[0m\n");

    public static void Query(string q) =>
        Console.WriteLine($"\n\x1b[44m\x1b[37m QUERY \x1b[0m {q}\n");

    public static void Api(string step, int msgCount) =>
        Console.WriteLine($"[{Timestamp()}] \x1b[35m◆\x1b[0m {step} ({msgCount} messages)");

    public static void ApiDone(UsageStats? usage)
    {
        if (usage != null)
        {
            Console.WriteLine($"         \x1b[2mtokens: {usage.InputTokens} in / {usage.OutputTokens} out\x1b[0m");
        }
    }

    public static void Tool(string name, object args)
    {
        var argStr = System.Text.Json.JsonSerializer.Serialize(args);
        var truncated = argStr.Length > 100 ? argStr[..100] + "..." : argStr;
        Console.WriteLine($"[{Timestamp()}] \x1b[33m⚡\x1b[0m {name} \x1b[2m{truncated}\x1b[0m");
    }

    public static void ToolResult(string name, bool success, string output)
    {
        var icon = success ? "\x1b[32m✓\x1b[0m" : "\x1b[31m✗\x1b[0m";
        var truncated = output.Length > 150 ? output[..150] + "..." : output;
        Console.WriteLine($"         {icon} {truncated}\x1b[0m");
    }

    public static void Gemini(string action, string? detail = null)
    {
        Console.WriteLine($"[{Timestamp()}] \x1b[45m\x1b[37m GEMINI \x1b[0m {action}");
        if (detail != null)
            Console.WriteLine($"         \x1b[2m{detail}\x1b[0m");
    }

    public static void GeminiResult(bool success, string message)
    {
        var icon = success ? "\x1b[32m✓\x1b[0m" : "\x1b[31m✗\x1b[0m";
        Console.WriteLine($"         {icon} {message}\x1b[0m");
    }
}
