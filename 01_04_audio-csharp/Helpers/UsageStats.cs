namespace AudioAgent.Helpers;

/// <summary>
/// Token usage statistics tracker.
/// </summary>
public sealed class UsageStats
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
}

public sealed class UsageTracker
{
    private int _inputTokens;
    private int _outputTokens;
    private int _requests;
    private int _geminiUploads;
    private int _geminiProcess;
    private int _geminiTts;

    public void RecordUsage(UsageStats? usage)
    {
        if (usage == null) return;
        _inputTokens += usage.InputTokens;
        _outputTokens += usage.OutputTokens;
        _requests++;
    }

    public void RecordGemini(string type)
    {
        switch (type)
        {
            case "upload": _geminiUploads++; break;
            case "process": _geminiProcess++; break;
            case "tts": _geminiTts++; break;
        }
    }

    public void LogStats()
    {
        Console.WriteLine($"\n📊 OpenAI Stats: {_requests} requests, {_inputTokens} input tokens, {_outputTokens} output tokens");
        Console.WriteLine($"🎨 Gemini Stats: {_geminiProcess} process, {_geminiTts} TTS, {_geminiUploads} uploads\n");
    }

    public void Reset()
    {
        _inputTokens = 0;
        _outputTokens = 0;
        _requests = 0;
        _geminiUploads = 0;
        _geminiProcess = 0;
        _geminiTts = 0;
    }
}
