using System.Text.Json.Serialization;

namespace AudioAgent.Models;

/// <summary>
/// Models for Gemini API audio processing.
/// </summary>
public sealed record GeminiRequest
{
    [JsonPropertyName("contents")]
    public required List<GeminiContent> Contents { get; init; }

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; init; }
}

public sealed record GeminiContent
{
    [JsonPropertyName("parts")]
    public required List<GeminiPart> Parts { get; init; }
}

public sealed record GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("inline_data")]
    public GeminiInlineData? InlineData { get; init; }

    [JsonPropertyName("file_data")]
    public GeminiFileData? FileData { get; init; }
}

public sealed record GeminiInlineData
{
    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }

    [JsonPropertyName("data")]
    public required string Data { get; init; }
}

public sealed record GeminiFileData
{
    [JsonPropertyName("mime_type")]
    public required string MimeType { get; init; }

    [JsonPropertyName("file_uri")]
    public required string FileUri { get; init; }
}

public sealed record GeminiGenerationConfig
{
    [JsonPropertyName("response_mime_type")]
    public string? ResponseMimeType { get; init; }

    [JsonPropertyName("response_schema")]
    public object? ResponseSchema { get; init; }

    [JsonPropertyName("responseModalities")]
    public List<string>? ResponseModalities { get; init; }

    [JsonPropertyName("speechConfig")]
    public GeminiSpeechConfig? SpeechConfig { get; init; }
}

public sealed record GeminiSpeechConfig
{
    [JsonPropertyName("voiceConfig")]
    public GeminiVoiceConfig? VoiceConfig { get; init; }

    [JsonPropertyName("multiSpeakerVoiceConfig")]
    public GeminiMultiSpeakerConfig? MultiSpeakerVoiceConfig { get; init; }
}

public sealed record GeminiVoiceConfig
{
    [JsonPropertyName("prebuiltVoiceConfig")]
    public GeminiPrebuiltVoiceConfig? PrebuiltVoiceConfig { get; init; }
}

public sealed record GeminiPrebuiltVoiceConfig
{
    [JsonPropertyName("voiceName")]
    public required string VoiceName { get; init; }
}

public sealed record GeminiMultiSpeakerConfig
{
    [JsonPropertyName("speakerVoiceConfigs")]
    public required List<GeminiSpeakerVoiceConfig> SpeakerVoiceConfigs { get; init; }
}

public sealed record GeminiSpeakerVoiceConfig
{
    [JsonPropertyName("speaker")]
    public required string Speaker { get; init; }

    [JsonPropertyName("voiceConfig")]
    public required GeminiVoiceConfig VoiceConfig { get; init; }
}

public sealed record GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; init; }

    [JsonPropertyName("error")]
    public GeminiError? Error { get; init; }
}

public sealed record GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; init; }
}

public sealed record GeminiError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

// Audio transcription result models
public sealed record TranscriptionResult
{
    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("duration_estimate")]
    public string? DurationEstimate { get; init; }

    [JsonPropertyName("primary_language")]
    public string? PrimaryLanguage { get; init; }

    [JsonPropertyName("segments")]
    public required List<TranscriptionSegment> Segments { get; init; }
}

public sealed record TranscriptionSegment
{
    [JsonPropertyName("speaker")]
    public string? Speaker { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("translation")]
    public string? Translation { get; init; }

    [JsonPropertyName("emotion")]
    public string? Emotion { get; init; }
}

// Audio analysis result models
public sealed record AnalysisResult
{
    [JsonPropertyName("audio_type")]
    public required string AudioType { get; init; }

    [JsonPropertyName("summary")]
    public required string Summary { get; init; }

    [JsonPropertyName("details")]
    public string? Details { get; init; }

    [JsonPropertyName("quality_assessment")]
    public string? QualityAssessment { get; init; }

    [JsonPropertyName("notable_elements")]
    public List<string>? NotableElements { get; init; }
}

// TTS result
public sealed record TtsResult
{
    public required byte[] Data { get; init; }
    public required string MimeType { get; init; }
}
