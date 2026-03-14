using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoAgent;

/// <summary>
/// Native tool definitions for video processing.
/// </summary>
public static class NativeTools
{
    private const int InlineSizeLimit = 20 * 1024 * 1024; // 20MB

    public static List<ToolDefinition> Definitions => [
        new ToolDefinition
        {
            Name = "analyze_video",
            Description = "Analyze video content - visual elements, audio, actions, and overall composition. Supports local files and YouTube URLs. Returns structured analysis with timestamps.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new
                    {
                        type = "string",
                        description = "Path to video file relative to project root (e.g., workspace/input/video.mp4) OR a YouTube URL"
                    },
                    analysis_type = new
                    {
                        type = "string",
                        @enum = new[] { "general", "visual", "audio", "action" },
                        description = "Type of analysis: 'general' (comprehensive), 'visual' (cinematography), 'audio' (speech, music), 'action' (events). Default: general"
                    },
                    custom_prompt = new { type = "string", description = "Optional custom analysis prompt" },
                    start_time = new { type = "string", description = "Optional start time for clipping (e.g., '30s' or '1m30s')" },
                    end_time = new { type = "string", description = "Optional end time for clipping" },
                    fps = new { type = "number", description = "Frames per second to sample (default: 1)" },
                    output_name = new { type = "string", description = "Optional base name for saving analysis JSON to workspace/output/" }
                },
                required = new[] { "video_path" }
            }
        },
        new ToolDefinition
        {
            Name = "transcribe_video",
            Description = "Transcribe speech from video with timestamps and speaker detection. Supports local files and YouTube URLs.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file OR YouTube URL" },
                    include_timestamps = new { type = "boolean", description = "Include timestamps. Default: true" },
                    detect_speakers = new { type = "boolean", description = "Detect different speakers. Default: true" },
                    translate_to = new { type = "string", description = "Target language for translation" },
                    start_time = new { type = "string", description = "Optional start time for clipping" },
                    end_time = new { type = "string", description = "Optional end time for clipping" },
                    output_name = new { type = "string", description = "Optional base name for saving JSON" }
                },
                required = new[] { "video_path" }
            }
        },
        new ToolDefinition
        {
            Name = "extract_video",
            Description = "Extract specific elements from video: scenes, keyframes, objects, or text. Returns structured data with timestamps.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file OR YouTube URL" },
                    extraction_type = new
                    {
                        type = "string",
                        @enum = new[] { "scenes", "keyframes", "objects", "text" },
                        description = "What to extract. Default: scenes"
                    },
                    start_time = new { type = "string", description = "Optional start time for clipping" },
                    end_time = new { type = "string", description = "Optional end time for clipping" },
                    fps = new { type = "number", description = "Frames per second to sample" },
                    output_name = new { type = "string", description = "Optional base name for saving JSON" }
                },
                required = new[] { "video_path" }
            }
        },
        new ToolDefinition
        {
            Name = "query_video",
            Description = "Ask any question about a video. Use for custom queries that don't fit analyze/transcribe/extract patterns.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file OR YouTube URL" },
                    question = new { type = "string", description = "Question or prompt about the video content" },
                    start_time = new { type = "string", description = "Optional start time to focus on" },
                    end_time = new { type = "string", description = "Optional end time to focus on" }
                },
                required = new[] { "video_path", "question" }
            }
        }
    ];

    public static bool IsNativeTool(string name) =>
        name is "analyze_video" or "transcribe_video" or "extract_video" or "query_video";

    public static async Task<object> ExecuteAsync(
        string name,
        JsonElement args,
        GeminiVideoService gemini,
        string workspaceRoot,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return name switch
            {
                "analyze_video" => await AnalyzeVideoAsync(args, gemini, workspaceRoot, cancellationToken),
                "transcribe_video" => await TranscribeVideoAsync(args, gemini, workspaceRoot, cancellationToken),
                "extract_video" => await ExtractVideoAsync(args, gemini, workspaceRoot, cancellationToken),
                "query_video" => await QueryVideoAsync(args, gemini, cancellationToken),
                _ => new { success = false, error = $"Unknown native tool: {name}" }
            };
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error(name, ex.Message);
            return new { success = false, error = ex.Message };
        }
    }

    private static async Task<object> AnalyzeVideoAsync(
        JsonElement args, GeminiVideoService gemini, string workspaceRoot, CancellationToken ct)
    {
        var videoPath = args.GetProperty("video_path").GetString()!;
        var analysisType = args.TryGetProperty("analysis_type", out var at) ? at.GetString() ?? "general" : "general";
        var customPrompt = args.TryGetProperty("custom_prompt", out var cp) ? cp.GetString() : null;
        var outputName = args.TryGetProperty("output_name", out var on) ? on.GetString() : null;
        var metadata = BuildMetadata(args);

        ConsoleLogger.Tool("analyze_video", new { video_path = videoPath[..Math.Min(50, videoPath.Length)], analysis_type });

        var (fileUri, videoBase64, mimeType) = await LoadVideoAsync(videoPath, workspaceRoot);
        var result = await gemini.AnalyzeVideoAsync(fileUri, videoBase64, mimeType, analysisType, customPrompt, metadata, ct);

        if (outputName != null)
        {
            var outputPath = await SaveOutputAsync(workspaceRoot, outputName, result, ct);
            ConsoleLogger.Success($"Analysis saved: {outputPath}");
            return new { success = true, video_path = videoPath, analysis_type = analysisType, output_path = outputPath, analysis = result };
        }

        ConsoleLogger.Success($"Analyzed: {result.VideoType}");
        return new { success = true, video_path = videoPath, analysis_type = analysisType, analysis = result };
    }

    private static async Task<object> TranscribeVideoAsync(
        JsonElement args, GeminiVideoService gemini, string workspaceRoot, CancellationToken ct)
    {
        var videoPath = args.GetProperty("video_path").GetString()!;
        var includeTimestamps = args.TryGetProperty("include_timestamps", out var it) && it.GetBoolean();
        var detectSpeakers = !args.TryGetProperty("detect_speakers", out var ds) || ds.GetBoolean();
        var translateTo = args.TryGetProperty("translate_to", out var t) ? t.GetString() : null;
        var outputName = args.TryGetProperty("output_name", out var on) ? on.GetString() : null;
        var metadata = BuildMetadata(args);

        ConsoleLogger.Tool("transcribe_video", new { video_path = videoPath[..Math.Min(50, videoPath.Length)], timestamps = includeTimestamps, speakers = detectSpeakers });

        var (fileUri, videoBase64, mimeType) = await LoadVideoAsync(videoPath, workspaceRoot);
        var result = await gemini.TranscribeVideoAsync(fileUri, videoBase64, mimeType, includeTimestamps, detectSpeakers, translateTo, metadata, ct);

        if (outputName != null)
        {
            var outputPath = await SaveOutputAsync(workspaceRoot, outputName, result, ct);
            ConsoleLogger.Success($"Transcription saved: {outputPath}");
            return new { success = true, video_path = videoPath, output_path = outputPath, transcription = result };
        }

        ConsoleLogger.Success($"Transcribed: {result.Segments?.Count ?? 0} segments");
        return new { success = true, video_path = videoPath, transcription = result };
    }

    private static async Task<object> ExtractVideoAsync(
        JsonElement args, GeminiVideoService gemini, string workspaceRoot, CancellationToken ct)
    {
        var videoPath = args.GetProperty("video_path").GetString()!;
        var extractionType = args.TryGetProperty("extraction_type", out var et) ? et.GetString() ?? "scenes" : "scenes";
        var outputName = args.TryGetProperty("output_name", out var on) ? on.GetString() : null;
        var metadata = BuildMetadata(args);

        ConsoleLogger.Tool("extract_video", new { video_path = videoPath[..Math.Min(50, videoPath.Length)], extraction_type = extractionType });

        var (fileUri, videoBase64, mimeType) = await LoadVideoAsync(videoPath, workspaceRoot);
        var result = await gemini.ExtractFromVideoAsync(fileUri, videoBase64, mimeType, extractionType, metadata, ct);

        if (outputName != null)
        {
            var outputPath = await SaveOutputAsync(workspaceRoot, outputName, result, ct);
            ConsoleLogger.Success($"Extraction saved: {outputPath}");
            return new { success = true, video_path = videoPath, extraction_type = extractionType, output_path = outputPath, extraction = result };
        }

        ConsoleLogger.Success($"Extracted {extractionType}");
        return new { success = true, video_path = videoPath, extraction_type = extractionType, extraction = result };
    }

    private static async Task<object> QueryVideoAsync(
        JsonElement args, GeminiVideoService gemini, CancellationToken ct)
    {
        var videoPath = args.GetProperty("video_path").GetString()!;
        var question = args.GetProperty("question").GetString()!;
        var metadata = BuildMetadata(args);

        ConsoleLogger.Tool("query_video", new { video_path = videoPath[..Math.Min(50, videoPath.Length)], question = question[..Math.Min(50, question.Length)] + "..." });

        // For query_video, we pass the YouTube URL or file directly
        string? fileUri = IsYouTubeUrl(videoPath) ? videoPath : null;
        string? videoBase64 = null;
        string mimeType = "video/mp4";

        if (fileUri == null)
        {
            // Load local file
            var fullPath = Path.GetFullPath(videoPath);
            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            videoBase64 = Convert.ToBase64String(bytes);
            mimeType = GetMimeType(videoPath);
        }

        var result = await gemini.ProcessVideoAsync(new VideoProcessingRequest
        {
            FileUri = fileUri,
            VideoBase64 = videoBase64,
            MimeType = mimeType,
            Prompt = question,
            VideoMetadata = metadata
        }, ct);

        ConsoleLogger.Success($"Query answered ({result.Length} chars)");
        return new { success = true, video_path = videoPath, question, answer = result };
    }

    private static async Task<(string? fileUri, string? videoBase64, string mimeType)> LoadVideoAsync(string videoPath, string workspaceRoot)
    {
        if (IsYouTubeUrl(videoPath))
        {
            return (videoPath, null, "video/mp4");
        }

        var fullPath = Path.IsPathRooted(videoPath) ? videoPath : Path.Combine(workspaceRoot, videoPath);
        var bytes = await File.ReadAllBytesAsync(fullPath);
        var mimeType = GetMimeType(videoPath);

        if (bytes.Length > InlineSizeLimit)
        {
            // For large files, we'd need to implement upload
            // For now, just use inline (will work for <20MB)
            ConsoleLogger.Warn($"Video file is large ({bytes.Length / 1024 / 1024}MB). Consider using files < 20MB.");
        }

        return (null, Convert.ToBase64String(bytes), mimeType);
    }

    private static VideoMetadata? BuildMetadata(JsonElement args)
    {
        string? startTime = args.TryGetProperty("start_time", out var st) ? st.GetString() : null;
        string? endTime = args.TryGetProperty("end_time", out var et) ? et.GetString() : null;
        double? fps = args.TryGetProperty("fps", out var f) ? f.GetDouble() : null;

        if (startTime == null && endTime == null && fps == null)
            return null;

        return new VideoMetadata
        {
            StartOffset = startTime,
            EndOffset = endTime,
            Fps = fps
        };
    }

    private static async Task<string> SaveOutputAsync<T>(string workspaceRoot, string outputName, T data, CancellationToken ct)
    {
        var outputDir = Path.Combine(workspaceRoot, "workspace", "output");
        Directory.CreateDirectory(outputDir);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fileName = $"{outputName}_{timestamp}.json";
        var outputPath = Path.Combine(outputDir, fileName);

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputPath, json, ct);

        return $"workspace/output/{fileName}";
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp4" => "video/mp4",
            ".mpeg" => "video/mpeg",
            ".mpg" => "video/mpg",
            ".mov" => "video/mov",
            ".avi" => "video/avi",
            ".flv" => "video/x-flv",
            ".webm" => "video/webm",
            ".wmv" => "video/wmv",
            ".3gp" => "video/3gpp",
            _ => "video/mp4"
        };
    }

    private static bool IsYouTubeUrl(string url) =>
        url.Contains("youtube.com/watch") || url.Contains("youtu.be/");
}
