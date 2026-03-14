namespace VideoAgent;

/// <summary>
/// Token usage statistics tracker.
/// </summary>
public sealed class UsageStats
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int Requests { get; set; }
}

public sealed class GeminiStats
{
    public int Uploads { get; set; }
    public int Processes { get; set; }
}

public static class StatsTracker
{
    private static readonly UsageStats _openAi = new();
    private static readonly GeminiStats _gemini = new();

    public static void RecordUsage(UsageStats? usage)
    {
        if (usage == null) return;
        _openAi.InputTokens += usage.InputTokens;
        _openAi.OutputTokens += usage.OutputTokens;
        _openAi.Requests++;
    }

    public static void RecordGemini(string type)
    {
        if (type == "upload") _gemini.Uploads++;
        else if (type == "process") _gemini.Processes++;
    }

    public static void LogStats()
    {
        Console.WriteLine($"\n📊 OpenAI Stats: {_openAi.Requests} requests, {_openAi.InputTokens} input tokens, {_openAi.OutputTokens} output tokens");
        Console.WriteLine($"🎨 Gemini Stats: {_gemini.Uploads} uploads, {_gemini.Processes} processes\n");
    }

    public static void Reset()
    {
        _openAi.InputTokens = 0;
        _openAi.OutputTokens = 0;
        _openAi.Requests = 0;
        _gemini.Uploads = 0;
        _gemini.Processes = 0;
    }
}
