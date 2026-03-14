namespace VideoAgent;

/// <summary>
/// Token usage statistics.
/// </summary>
public sealed class UsageStats
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int Requests { get; set; }
}

/// <summary>
/// Global stats tracker.
/// </summary>
public static class StatsTracker
{
    private static readonly UsageStats Stats = new();

    public static void Record(UsageStats? usage)
    {
        if (usage == null) return;
        Stats.InputTokens += usage.InputTokens;
        Stats.OutputTokens += usage.OutputTokens;
        Stats.Requests++;
    }

    public static void Log()
    {
        Console.WriteLine($"\n📊 Stats: {Stats.Requests} requests, {Stats.InputTokens} input tokens, {Stats.OutputTokens} output tokens\n");
    }

    public static void Reset()
    {
        Stats.InputTokens = 0;
        Stats.OutputTokens = 0;
        Stats.Requests = 0;
    }
}
