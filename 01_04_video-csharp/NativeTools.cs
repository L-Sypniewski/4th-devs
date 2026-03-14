using System.Text.Json;

namespace VideoAgent;

public static class NativeTools
{
    private static GeminiVideoService? _videoService;

    public static void Initialize(GeminiVideoService videoService) =>
        _videoService = videoService;

    public static bool IsNativeTool(string name) =>
        name is "analyze_video" or "transcribe_video" or "extract_video" or "query_video";

    public static List<ToolDefinition> Definitions => [
        new ToolDefinition
        {
            Name = "analyze_video",
            Description = "Analyze video content. Supports local files and YouTube URLs.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file or YouTube URL" },
                    analysis_type = new
                    {
                        type = "string",
                        @enum = new[] { "general", "visual", "audio", "action" },
                        description = "Type of analysis. Default: general"
                    }
                },
                required = new[] { "video_path" }
            }
        },
        new ToolDefinition
        {
            Name = "transcribe_video",
            Description = "Transcribe speech from video with timestamps and speaker detection.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file or YouTube URL" },
                    include_timestamps = new { type = "boolean", description = "Include timestamps. Default: true" },
                    detect_speakers = new { type = "boolean", description = "Detect speakers. Default: true" }
                },
                required = new[] { "video_path" }
            }
        },
        new ToolDefinition
        {
            Name = "extract_video",
            Description = "Extract elements from video: scenes, keyframes, objects, text.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file or YouTube URL" },
                    extraction_type = new
                    {
                        type = "string",
                        @enum = new[] { "scenes", "keyframes", "objects", "text" },
                        description = "What to extract. Default: scenes"
                    }
                },
                required = new[] { "video_path" }
            }
        },
        new ToolDefinition
        {
            Name = "query_video",
            Description = "Ask any question about video content.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file or YouTube URL" },
                    question = new { type = "string", description = "Question about the video" }
                },
                required = new[] { "video_path", "question" }
            }
        }
    ];

    public static async Task<object> ExecuteAsync(
        string toolName,
        Dictionary<string, object?> args,
        CancellationToken cancellationToken = default)
    {
        if (_videoService == null)
            throw new InvalidOperationException("Video service not initialized");

        var videoPath = GetRequiredString(args, "video_path");

        ConsoleLogger.Tool(toolName, new { video_path = videoPath });

        return toolName switch
        {
            "analyze_video" => await ExecuteAnalyzeVideoAsync(videoPath, args, cancellationToken),
            "transcribe_video" => await ExecuteTranscribeVideoAsync(videoPath, args, cancellationToken),
            "extract_video" => await ExecuteExtractVideoAsync(videoPath, args, cancellationToken),
            "query_video" => await ExecuteQueryVideoAsync(videoPath, args, cancellationToken),
            _ => throw new Exception($"Unknown tool: {toolName}")
        };
    }

    private static async Task<object> ExecuteAnalyzeVideoAsync(
        string videoPath,
        Dictionary<string, object?> args,
        CancellationToken ct)
    {
        var analysisType = GetString(args, "analysis_type") ?? "general";

        var result = await _videoService!.AnalyzeVideoAsync(videoPath, analysisType, ct);
        ConsoleLogger.Success($"Analyzed video ({result.Length} chars)");

        return new { success = true, video_path = videoPath, analysis_type = analysisType, analysis = result };
    }

    private static async Task<object> ExecuteTranscribeVideoAsync(
        string videoPath,
        Dictionary<string, object?> args,
        CancellationToken ct)
    {
        var includeTimestamps = GetBool(args, "include_timestamps") ?? true;
        var detectSpeakers = GetBool(args, "detect_speakers") ?? true;

        var result = await _videoService!.TranscribeVideoAsync(videoPath, includeTimestamps, detectSpeakers, ct);
        ConsoleLogger.Success($"Transcribed video ({result.Length} chars)");

        return new { success = true, video_path = videoPath, transcription = result };
    }

    private static async Task<object> ExecuteExtractVideoAsync(
        string videoPath,
        Dictionary<string, object?> args,
        CancellationToken ct)
    {
        var extractionType = GetString(args, "extraction_type") ?? "scenes";

        var result = await _videoService!.ExtractFromVideoAsync(videoPath, extractionType, ct);
        ConsoleLogger.Success($"Extracted {extractionType} ({result.Length} chars)");

        return new { success = true, video_path = videoPath, extraction_type = extractionType, extraction = result };
    }

    private static async Task<object> ExecuteQueryVideoAsync(
        string videoPath,
        Dictionary<string, object?> args,
        CancellationToken ct)
    {
        var question = GetRequiredString(args, "question");

        var result = await _videoService!.QueryVideoAsync(videoPath, question, ct);
        ConsoleLogger.Success($"Query answered ({result.Length} chars)");

        return new { success = true, video_path = videoPath, question, answer = result };
    }

    // Helper methods

    private static string GetRequiredString(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
            throw new Exception($"Missing required parameter: {key}");
        return value.ToString()!;
    }

    private static string? GetString(Dictionary<string, object?> args, string key) =>
        args.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static bool? GetBool(Dictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
            return null;
        if (value is bool b)
            return b;
        return bool.TryParse(value.ToString(), out var result) ? result : null;
    }
}
