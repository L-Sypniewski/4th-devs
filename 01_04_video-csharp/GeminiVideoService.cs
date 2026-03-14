using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoAgent;

/// <summary>
/// Google Gemini API wrapper for video processing.
/// Uses gemini-2.5-flash for video understanding.
/// </summary>
public sealed class GeminiVideoService(Configuration config, HttpClient httpClient)
{
    private const string UploadEndpoint = "https://generativelanguage.googleapis.com/upload/v1beta/files";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string GenerateEndpoint => $"https://generativelanguage.googleapis.com/v1beta/models/{config.GeminiModel}:generateContent";

    /// <summary>
    /// Upload a video file to Gemini Files API.
    /// Required for files > 20MB or for reuse across multiple requests.
    /// </summary>
    public async Task<UploadedFile> UploadVideoAsync(byte[] videoData, string mimeType, string displayName, CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Gemini("Uploading video file", displayName);

        // Step 1: Initialize resumable upload
        using var initRequest = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint);
        initRequest.Headers.Add("x-goog-api-key", config.GeminiApiKey);
        initRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        initRequest.Headers.Add("X-Goog-Upload-Command", "start");
        initRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", videoData.Length.ToString());
        initRequest.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
        initRequest.Content = new StringContent(
            JsonSerializer.Serialize(new { file = new { display_name = displayName } }),
            Encoding.UTF8,
            "application/json");

        using var initResponse = await httpClient.SendAsync(initRequest, cancellationToken);
        if (!initResponse.IsSuccessStatusCode)
        {
            var error = await initResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Upload init failed: {error}");
        }

        var uploadUrl = initResponse.Headers.GetValues("x-goog-upload-url").FirstOrDefault()
            ?? throw new Exception("No upload URL received from Gemini");

        // Step 2: Upload the actual bytes
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        uploadRequest.Content = new ByteArrayContent(videoData);
        uploadRequest.Content.Headers.ContentLength = videoData.Length;

        using var uploadResponse = await httpClient.SendAsync(uploadRequest, cancellationToken);
        if (!uploadResponse.IsSuccessStatusCode)
        {
            var error = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Upload failed: {error}");
        }

        var json = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
        var fileInfo = JsonSerializer.Deserialize<GeminiUploadResponse>(json, JsonOptions)
            ?? throw new Exception("Failed to parse upload response");

        if (fileInfo.File?.Uri is null)
            throw new Exception("No file URI in upload response");

        ConsoleLogger.GeminiResult(true, $"Uploaded: {fileInfo.File.Name}");
        StatsTracker.RecordGemini("upload");

        return new UploadedFile(fileInfo.File.Uri, fileInfo.File.Name, fileInfo.File.MimeType ?? mimeType);
    }

    /// <summary>
    /// Process video with Gemini (analysis, transcription, etc.)
    /// </summary>
    public async Task<string> ProcessVideoAsync(VideoProcessingRequest request, CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Gemini("Processing video", request.Prompt?[..Math.Min(80, request.Prompt.Length ?? 0)]);

        var parts = new List<object>();

        // Add video as file_data or inline_data
        if (!string.IsNullOrEmpty(request.FileUri))
        {
            var filePart = new Dictionary<string, object>
            {
                ["file_data"] = new Dictionary<string, object?>
                {
                    ["file_uri"] = request.FileUri,
                    ["mime_type"] = request.MimeType
                }
            };

            // Only add mime_type if not a YouTube URL
            if (IsYouTubeUrl(request.FileUri))
            {
                ((Dictionary<string, object?>)filePart["file_data"])["mime_type"] = null;
            }

            if (request.VideoMetadata != null)
                filePart["video_metadata"] = request.VideoMetadata;

            parts.Add(filePart);
        }
        else if (!string.IsNullOrEmpty(request.VideoBase64))
        {
            var inlinePart = new Dictionary<string, object>
            {
                ["inline_data"] = new Dictionary<string, object?>
                {
                    ["mime_type"] = request.MimeType,
                    ["data"] = request.VideoBase64
                }
            };

            if (request.VideoMetadata != null)
                inlinePart["video_metadata"] = request.VideoMetadata;

            parts.Add(inlinePart);
        }
        else
        {
            throw new ArgumentException("Either FileUri or VideoBase64 must be provided");
        }

        // Add text prompt after video (best practice per docs)
        parts.Add(new { text = request.Prompt });

        var body = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { parts } }
        };

        // Add structured output schema if provided
        if (request.ResponseSchema != null)
        {
            body["generation_config"] = new
            {
                response_mime_type = "application/json",
                response_schema = request.ResponseSchema
            };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, GenerateEndpoint);
        httpRequest.Headers.Add("x-goog-api-key", config.GeminiApiKey);
        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var data = JsonSerializer.Deserialize<GeminiGenerateResponse>(json, JsonOptions);
        if (data?.Error != null)
            throw new Exception(data.Error.Message ?? JsonSerializer.Serialize(data.Error));

        StatsTracker.RecordGemini("process");

        var text = data?.Candidates?[0]?.Content?.Parts?[0]?.Text;
        if (string.IsNullOrEmpty(text))
            throw new Exception("No text response from Gemini");

        ConsoleLogger.GeminiResult(true, $"Processed video ({text.Length} chars)");
        return text;
    }

    /// <summary>
    /// Analyze video content (visual and audio).
    /// </summary>
    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(
        string? fileUri, string? videoBase64, string mimeType,
        string analysisType = "general", string? customPrompt = null,
        VideoMetadata? videoMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var prompts = new Dictionary<string, string>
        {
            ["general"] = @"Analyze this video comprehensively. Describe:
- Type of video content (tutorial, vlog, presentation, movie clip, etc.)
- Main subject and topics covered
- Key visual elements and scenes
- Audio content (speech, music, sound effects)
- Overall quality and production value
- Notable moments with timestamps (MM:SS format)",

            ["visual"] = @"Analyze the visual elements of this video. Describe:
- Scene composition and cinematography
- Color palette and lighting
- Text overlays, graphics, or animations
- Objects and people visible
- Visual transitions and effects
- Key visual moments with timestamps",

            ["audio"] = @"Analyze the audio content of this video. Describe:
- Speech content and speakers
- Background music (genre, mood)
- Sound effects
- Audio quality
- Key audio moments with timestamps",

            ["action"] = @"Analyze the actions and events in this video. Describe:
- Sequence of events with timestamps
- Key actions performed
- Interactions between subjects
- Important transitions or changes
- Climactic or significant moments"
        };

        var prompt = customPrompt ?? prompts.GetValueOrDefault(analysisType, prompts["general"]);

        var schema = new
        {
            type = "OBJECT",
            properties = new
            {
                video_type = new { type = "STRING", description = "Type of video content" },
                summary = new { type = "STRING", description = "Brief summary of the video" },
                duration_estimate = new { type = "STRING", description = "Estimated duration" },
                key_moments = new
                {
                    type = "ARRAY",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            timestamp = new { type = "STRING", description = "Timestamp in MM:SS format" },
                            description = new { type = "STRING", description = "What happens" }
                        }
                    }
                },
                visual_elements = new { type = "ARRAY", items = new { type = "STRING" } },
                audio_elements = new { type = "ARRAY", items = new { type = "STRING" } },
                quality_assessment = new { type = "STRING" }
            },
            required = new[] { "video_type", "summary" }
        };

        var result = await ProcessVideoAsync(new VideoProcessingRequest
        {
            FileUri = fileUri,
            VideoBase64 = videoBase64,
            MimeType = mimeType,
            Prompt = prompt,
            ResponseSchema = schema,
            VideoMetadata = videoMetadata
        }, cancellationToken);

        return JsonSerializer.Deserialize<VideoAnalysisResult>(result, JsonOptions)
            ?? throw new Exception("Failed to parse analysis result");
    }

    /// <summary>
    /// Transcribe speech from video with timestamps.
    /// </summary>
    public async Task<TranscriptionResult> TranscribeVideoAsync(
        string? fileUri, string? videoBase64, string mimeType,
        bool includeTimestamps = true, bool detectSpeakers = true,
        string? targetLanguage = null, VideoMetadata? videoMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = "Transcribe all speech from this video.\n\nRequirements:\n";

        if (detectSpeakers)
            prompt += "- Identify distinct speakers (e.g., Speaker 1, Speaker 2, or names if visible/mentioned).\n";
        if (includeTimestamps)
            prompt += "- Provide accurate timestamps for each segment (Format: MM:SS).\n";
        prompt += "- Detect the primary language.\n";
        if (targetLanguage != null)
            prompt += $"- Translate all segments to {targetLanguage}.\n";
        prompt += "- Note any significant non-speech audio (music, sound effects) with timestamps.\n";
        prompt += "- Provide a brief summary at the beginning.";

        var result = await ProcessVideoAsync(new VideoProcessingRequest
        {
            FileUri = fileUri,
            VideoBase64 = videoBase64,
            MimeType = mimeType,
            Prompt = prompt,
            ResponseSchema = null, // Complex dynamic schema - let it return JSON text
            VideoMetadata = videoMetadata
        }, cancellationToken);

        return JsonSerializer.Deserialize<TranscriptionResult>(result, JsonOptions)
            ?? throw new Exception("Failed to parse transcription result");
    }

    /// <summary>
    /// Extract elements from video (scenes, keyframes, objects, text).
    /// </summary>
    public async Task<string> ExtractFromVideoAsync(
        string? fileUri, string? videoBase64, string mimeType,
        string extractionType = "scenes", VideoMetadata? videoMetadata = null,
        CancellationToken cancellationToken = default)
    {
        var prompts = new Dictionary<string, string>
        {
            ["scenes"] = @"Identify and describe all distinct scenes in this video.
For each scene provide:
- Start timestamp (MM:SS)
- End timestamp (MM:SS)
- Description of the scene
- Key visual elements
- Mood/tone",

            ["keyframes"] = @"Identify the key frames in this video - moments that best represent the content.
For each keyframe provide:
- Timestamp (MM:SS)
- Description of what's shown
- Why this frame is significant",

            ["objects"] = @"Identify all notable objects, people, and elements visible in this video.
For each item provide:
- What it is
- Timestamps when visible (MM:SS)
- Context/relevance to the video",

            ["text"] = @"Extract all text visible in this video (on-screen text, titles, captions, signs, etc.)
For each text element provide:
- The text content
- Timestamp when visible (MM:SS)
- Location on screen
- Purpose (title, caption, sign, etc.)"
        };

        var prompt = prompts.GetValueOrDefault(extractionType, prompts["scenes"]);

        return await ProcessVideoAsync(new VideoProcessingRequest
        {
            FileUri = fileUri,
            VideoBase64 = videoBase64,
            MimeType = mimeType,
            Prompt = prompt,
            VideoMetadata = videoMetadata
        }, cancellationToken);
    }

    private static bool IsYouTubeUrl(string url) =>
        url.Contains("youtube.com/watch") || url.Contains("youtu.be/");
}

#region Gemini Models

public sealed record UploadedFile(string FileUri, string Name, string MimeType);

public sealed record VideoProcessingRequest
{
    public string? FileUri { get; init; }
    public string? VideoBase64 { get; init; }
    public string MimeType { get; init; } = "video/mp4";
    public string? Prompt { get; init; }
    public object? ResponseSchema { get; init; }
    public VideoMetadata? VideoMetadata { get; init; }
}

public sealed record VideoMetadata
{
    [JsonPropertyName("start_offset")]
    public string? StartOffset { get; init; }

    [JsonPropertyName("end_offset")]
    public string? EndOffset { get; init; }

    [JsonPropertyName("fps")]
    public double? Fps { get; init; }
}

public sealed record VideoAnalysisResult
{
    [JsonPropertyName("video_type")]
    public string? VideoType { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("duration_estimate")]
    public string? DurationEstimate { get; init; }

    [JsonPropertyName("key_moments")]
    public List<KeyMoment>? KeyMoments { get; init; }

    [JsonPropertyName("visual_elements")]
    public List<string>? VisualElements { get; init; }

    [JsonPropertyName("audio_elements")]
    public List<string>? AudioElements { get; init; }

    [JsonPropertyName("quality_assessment")]
    public string? QualityAssessment { get; init; }
}

public sealed record KeyMoment
{
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

public sealed record TranscriptionResult
{
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("primary_language")]
    public string? PrimaryLanguage { get; init; }

    [JsonPropertyName("segments")]
    public List<TranscriptionSegment>? Segments { get; init; }
}

public sealed record TranscriptionSegment
{
    [JsonPropertyName("speaker")]
    public string? Speaker { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

// Internal response models
file sealed record GeminiUploadResponse(GeminiFile? File);
file sealed record GeminiFile(string? Uri, string? Name, string? MimeType);

file sealed record GeminiGenerateResponse
{
    public List<GeminiCandidate>? Candidates { get; init; }
    public GeminiError? Error { get; init; }
}

file sealed record GeminiCandidate
{
    public GeminiContent? Content { get; init; }
}

file sealed record GeminiContent
{
    public List<GeminiPart>? Parts { get; init; }
}

file sealed record GeminiPart
{
    public string? Text { get; init; }
}

file sealed record GeminiError
{
    public string? Message { get; init; }
}

#endregion
