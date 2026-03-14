using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoAgent;

public sealed class GeminiVideoService(Configuration config, HttpClient httpClient)
{
    private const string UploadEndpoint = "https://generativelanguage.googleapis.com/upload/v1beta/files";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string GenerateEndpoint => $"https://generativelanguage.googleapis.com/v1beta/models/{config.GeminiModel}:generateContent";

    public async Task<string> AnalyzeVideoAsync(
        string videoPath,
        string analysisType = "general",
        CancellationToken cancellationToken = default)
    {
        var prompts = new Dictionary<string, string>
        {
            ["general"] = "Analyze this video comprehensively. Describe: type of content, main subject, key elements, audio content, notable moments with timestamps (MM:SS).",
            ["visual"] = "Analyze visual elements. Describe: scene composition, colors, lighting, objects, key visual moments with timestamps.",
            ["audio"] = "Analyze audio content. Describe: speech, music, sound effects, key audio moments with timestamps.",
            ["action"] = "Analyze actions and events. Describe: sequence of events with timestamps, key actions, important transitions."
        };

        var prompt = prompts.TryGetValue(analysisType, out var p) ? p : prompts["general"];
        return await ProcessVideoAsync(videoPath, prompt, cancellationToken);
    }

    public async Task<string> TranscribeVideoAsync(
        string videoPath,
        bool includeTimestamps = true,
        bool detectSpeakers = true,
        CancellationToken cancellationToken = default)
    {
        var prompt = "Transcribe all speech from this video.\n\nRequirements:\n";
        if (detectSpeakers)
            prompt += "- Identify distinct speakers\n";
        if (includeTimestamps)
            prompt += "- Provide timestamps (MM:SS format)\n";
        prompt += "- Detect primary language\n- Summarize at the beginning";

        return await ProcessVideoAsync(videoPath, prompt, cancellationToken);
    }

    public async Task<string> ExtractFromVideoAsync(
        string videoPath,
        string extractionType = "scenes",
        CancellationToken cancellationToken = default)
    {
        var prompts = new Dictionary<string, string>
        {
            ["scenes"] = "Identify distinct scenes. For each scene: start/end timestamps (MM:SS) and description.",
            ["keyframes"] = "Identify key frames - representative moments with timestamps (MM:SS).",
            ["objects"] = "Identify notable objects and people with timestamps (MM:SS).",
            ["text"] = "Extract all on-screen text with timestamps (MM:SS)."
        };

        var prompt = prompts.TryGetValue(extractionType, out var p) ? p : prompts["scenes"];
        return await ProcessVideoAsync(videoPath, prompt, cancellationToken);
    }

    public async Task<string> QueryVideoAsync(
        string videoPath,
        string question,
        CancellationToken cancellationToken = default)
    {
        return await ProcessVideoAsync(videoPath, question, cancellationToken);
    }

    public async Task<string> ProcessVideoAsync(
        string videoPath,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Gemini("Processing video", videoPath);

        var parts = new List<object>();

        if (IsYouTubeUrl(videoPath))
        {
            parts.Add(new
            {
                file_data = new
                {
                    file_uri = videoPath
                }
            });
        }
        else
        {
            var (videoBase64, mimeType) = await LoadVideoAsync(videoPath);
            parts.Add(new
            {
                inline_data = new
                {
                    mime_type = mimeType,
                    data = videoBase64
                }
            });
        }

        parts.Add(new { text = prompt });

        var body = new Dictionary<string, object>
        {
            ["contents"] = new[] { new { parts } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, GenerateEndpoint);
        request.Headers.Add("x-goog-api-key", config.GeminiApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        var data = JsonSerializer.Deserialize<GeminiResponse>(json, JsonOptions);
        if (data?.Error != null)
            throw new Exception(data.Error.Message ?? "Gemini API error");

        var text = data?.Candidates?[0]?.Content?.Parts?[0]?.Text
            ?? throw new Exception("No text response from Gemini");

        ConsoleLogger.Success($"Processed video ({text.Length} chars)");
        return text;
    }

    private static async Task<(string base64, string mimeType)> LoadVideoAsync(string videoPath)
    {
        var fullPath = Path.GetFullPath(Path.Combine("workspace/input", videoPath));
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Video file not found: {fullPath}");

        var bytes = await File.ReadAllBytesAsync(fullPath);
        var mimeType = GetMimeType(videoPath);
        return (Convert.ToBase64String(bytes), mimeType);
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp4" => "video/mp4",
            ".mpeg" => "video/mpeg",
            ".mov" => "video/mov",
            ".avi" => "video/avi",
            ".webm" => "video/webm",
            ".flv" => "video/x-flv",
            ".wmv" => "video/wmv",
            ".3gp" => "video/3gpp",
            _ => "video/mp4"
        };
    }

    private static bool IsYouTubeUrl(string url) =>
        url.Contains("youtube.com/watch") || url.Contains("youtu.be/");
}

// Gemini response models

public sealed class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }
    [JsonPropertyName("error")]
    public GeminiError? Error { get; init; }
}

public sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }
}

public sealed class GeminiContent
{
    [JsonPropertyName("parts")]
    public List<GeminiPart>? Parts { get; init; }
}

public sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed class GeminiError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
