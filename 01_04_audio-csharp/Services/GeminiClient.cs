using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AudioAgent.Helpers;
using AudioAgent.Models;

namespace AudioAgent.Services;

/// <summary>
/// Google Gemini API wrapper for audio processing.
/// Supports audio understanding and text-to-speech.
/// </summary>
public sealed class GeminiClient(Configuration config, HttpClient httpClient, UsageTracker usageTracker)
{
    private const string UploadEndpoint = "https://generativelanguage.googleapis.com/upload/v1beta/files";
    private const string AudioModel = "gemini-2.5-flash";
    private const string TtsModel = "gemini-2.5-flash-preview-tts";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Available TTS voices with their characteristics.
    /// </summary>
    public static readonly Dictionary<string, string> TtsVoices = new()
    {
        ["Zephyr"] = "Bright",
        ["Puck"] = "Upbeat",
        ["Charon"] = "Informative",
        ["Kore"] = "Firm",
        ["Fenrir"] = "Excitable",
        ["Leda"] = "Youthful",
        ["Orus"] = "Firm",
        ["Aoede"] = "Breezy",
        ["Callirrhoe"] = "Easy-going",
        ["Autonoe"] = "Bright",
        ["Enceladus"] = "Breathy",
        ["Iapetus"] = "Clear",
        ["Umbriel"] = "Easy-going",
        ["Algieba"] = "Smooth",
        ["Despina"] = "Smooth",
        ["Erinome"] = "Clear",
        ["Algenib"] = "Gravelly",
        ["Rasalgethi"] = "Informative",
        ["Laomedeia"] = "Upbeat",
        ["Achernar"] = "Soft",
        ["Alnilam"] = "Firm",
        ["Schedar"] = "Even",
        ["Gacrux"] = "Mature",
        ["Pulcherrima"] = "Forward",
        ["Achird"] = "Friendly",
        ["Zubenelgenubi"] = "Casual",
        ["Vindemiatrix"] = "Gentle",
        ["Sadachbia"] = "Lively",
        ["Sadaltager"] = "Knowledgeable",
        ["Sulafat"] = "Warm"
    };

    public async Task<UploadedFile> UploadAudioFileAsync(
        byte[] audioBuffer,
        string mimeType,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Gemini("Uploading audio file", displayName);

        // Step 1: Initialize resumable upload
        using var initRequest = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint);
        initRequest.Headers.Add("x-goog-api-key", config.GeminiApiKey);
        initRequest.Headers.Add("X-Goog-Upload-Protocol", "resumable");
        initRequest.Headers.Add("X-Goog-Upload-Command", "start");
        initRequest.Headers.Add("X-Goog-Upload-Header-Content-Length", audioBuffer.Length.ToString());
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

        var uploadUrl = initResponse.Headers.GetValues("x-goog-upload-url").FirstOrDefault();
        if (string.IsNullOrEmpty(uploadUrl))
            throw new Exception("No upload URL received from Gemini");

        // Step 2: Upload the actual bytes
        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
        uploadRequest.Headers.Add("X-Goog-Upload-Offset", "0");
        uploadRequest.Headers.Add("X-Goog-Upload-Command", "upload, finalize");
        uploadRequest.Content = new ByteArrayContent(audioBuffer);
        uploadRequest.Content.Headers.ContentLength = audioBuffer.Length;

        using var uploadResponse = await httpClient.SendAsync(uploadRequest, cancellationToken);
        if (!uploadResponse.IsSuccessStatusCode)
        {
            var error = await uploadResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Upload failed: {error}");
        }

        var fileInfo = await uploadResponse.Content.ReadFromJsonAsync<GeminiUploadResponse>(JsonOptions, cancellationToken);
        if (fileInfo?.File?.Uri == null)
            throw new Exception("No file URI in upload response");

        ConsoleLogger.GeminiResult(true, $"Uploaded: {fileInfo.File.Name}");
        usageTracker.RecordGemini("upload");

        return new UploadedFile(fileInfo.File.Uri, fileInfo.File.Name, fileInfo.File.MimeType ?? mimeType);
    }

    public async Task<string> ProcessAudioAsync(
        string? fileUri,
        string? audioBase64,
        string mimeType,
        string prompt,
        object? responseSchema = null,
        CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Gemini("Processing audio", prompt[..Math.Min(80, prompt.Length)]);

        var parts = new List<GeminiPart> { new() { Text = prompt } };

        if (!string.IsNullOrEmpty(fileUri))
        {
            parts.Add(new GeminiPart
            {
                FileData = new GeminiFileData { MimeType = mimeType, FileUri = fileUri }
            });
        }
        else if (!string.IsNullOrEmpty(audioBase64))
        {
            parts.Add(new GeminiPart
            {
                InlineData = new GeminiInlineData { MimeType = mimeType, Data = audioBase64 }
            });
        }
        else
        {
            throw new Exception("Either fileUri or audioBase64 must be provided");
        }

        var body = new GeminiRequest
        {
            Contents = [new GeminiContent { Parts = parts }],
            GenerationConfig = responseSchema != null
                ? new GeminiGenerationConfig
                {
                    ResponseMimeType = "application/json",
                    ResponseSchema = responseSchema
                }
                : null
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{AudioModel}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", config.GeminiApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var data = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);

        if (data?.Error != null)
            throw new Exception(data.Error.Message ?? "Unknown Gemini error");

        usageTracker.RecordGemini("process");

        var text = data?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrEmpty(text))
            throw new Exception("No text response from Gemini");

        ConsoleLogger.GeminiResult(true, $"Processed audio ({text.Length} chars)");

        return text;
    }

    public async Task<TranscriptionResult> TranscribeAudioAsync(
        string? fileUri,
        string? audioBase64,
        string mimeType,
        bool includeTimestamps = true,
        bool detectSpeakers = true,
        bool detectEmotions = false,
        string? targetLanguage = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildTranscriptionPrompt(includeTimestamps, detectSpeakers, detectEmotions, targetLanguage);
        var schema = BuildTranscriptionSchema(includeTimestamps, detectSpeakers, detectEmotions, targetLanguage);

        var result = await ProcessAudioAsync(fileUri, audioBase64, mimeType, prompt, schema, cancellationToken);
        return JsonSerializer.Deserialize<TranscriptionResult>(result, JsonOptions)
            ?? throw new Exception("Failed to parse transcription result");
    }

    public async Task<AnalysisResult> AnalyzeAudioAsync(
        string? fileUri,
        string? audioBase64,
        string mimeType,
        string analysisType = "general",
        string? customPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var prompt = customPrompt ?? GetAnalysisPrompt(analysisType);
        var schema = BuildAnalysisSchema();

        var result = await ProcessAudioAsync(fileUri, audioBase64, mimeType, prompt, schema, cancellationToken);
        return JsonSerializer.Deserialize<AnalysisResult>(result, JsonOptions)
            ?? throw new Exception("Failed to parse analysis result");
    }

    public async Task<TtsResult> GenerateSpeechAsync(
        string text,
        string voice = "Kore",
        CancellationToken cancellationToken = default)
    {
        ConsoleLogger.Gemini("Generating speech", $"{voice}: {text[..Math.Min(60, text.Length)]}...");

        var body = new GeminiRequest
        {
            Contents = [new GeminiContent { Parts = [new GeminiPart { Text = text }] }],
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseModalities = ["AUDIO"],
                SpeechConfig = new GeminiSpeechConfig
                {
                    VoiceConfig = new GeminiVoiceConfig
                    {
                        PrebuiltVoiceConfig = new GeminiPrebuiltVoiceConfig { VoiceName = voice }
                    }
                }
            }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{TtsModel}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", config.GeminiApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var data = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);

        if (data?.Error != null)
            throw new Exception(data.Error.Message ?? "Unknown Gemini error");

        var audioData = data?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.InlineData?.Data;
        if (string.IsNullOrEmpty(audioData))
            throw new Exception("No audio data in TTS response");

        usageTracker.RecordGemini("tts");
        ConsoleLogger.GeminiResult(true, $"Generated speech with voice: {voice}");

        return new TtsResult
        {
            Data = Convert.FromBase64String(audioData),
            MimeType = "audio/wav"
        };
    }

    public async Task<TtsResult> GenerateMultiSpeakerSpeechAsync(
        string text,
        List<(string Speaker, string Voice)> speakers,
        CancellationToken cancellationToken = default)
    {
        var speakerNames = string.Join(", ", speakers.Select(s => s.Speaker));
        ConsoleLogger.Gemini("Generating multi-speaker speech", speakerNames);

        var speakerConfigs = speakers.Select(s => new GeminiSpeakerVoiceConfig
        {
            Speaker = s.Speaker,
            VoiceConfig = new GeminiVoiceConfig
            {
                PrebuiltVoiceConfig = new GeminiPrebuiltVoiceConfig { VoiceName = s.Voice }
            }
        }).ToList();

        var body = new GeminiRequest
        {
            Contents = [new GeminiContent { Parts = [new GeminiPart { Text = text }] }],
            GenerationConfig = new GeminiGenerationConfig
            {
                ResponseModalities = ["AUDIO"],
                SpeechConfig = new GeminiSpeechConfig
                {
                    MultiSpeakerVoiceConfig = new GeminiMultiSpeakerConfig
                    {
                        SpeakerVoiceConfigs = speakerConfigs
                    }
                }
            }
        };

        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{TtsModel}:generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Add("x-goog-api-key", config.GeminiApiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var data = await response.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken);

        if (data?.Error != null)
            throw new Exception(data.Error.Message ?? "Unknown Gemini error");

        var audioData = data?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.InlineData?.Data;
        if (string.IsNullOrEmpty(audioData))
            throw new Exception("No audio data in TTS response");

        usageTracker.RecordGemini("tts");
        ConsoleLogger.GeminiResult(true, $"Generated multi-speaker speech: {speakerNames}");

        return new TtsResult
        {
            Data = Convert.FromBase64String(audioData),
            MimeType = "audio/wav"
        };
    }

    private static string BuildTranscriptionPrompt(
        bool includeTimestamps, bool detectSpeakers, bool detectEmotions, string? targetLanguage)
    {
        var prompt = "Process this audio file and generate a detailed transcription.\n\nRequirements:\n";

        if (detectSpeakers)
            prompt += "- Identify distinct speakers (e.g., Speaker 1, Speaker 2, or names if context allows).\n";
        if (includeTimestamps)
            prompt += "- Provide accurate timestamps for each segment (Format: MM:SS).\n";
        prompt += "- Detect the primary language of each segment.\n";
        if (targetLanguage != null)
            prompt += $"- Translate all segments to {targetLanguage}.\n";
        if (detectEmotions)
            prompt += "- Identify the primary emotion of the speaker. Choose exactly one: happy, sad, angry, neutral.\n";
        prompt += "- Provide a brief summary of the entire audio at the beginning.";

        return prompt;
    }

    private static object BuildTranscriptionSchema(
        bool includeTimestamps, bool detectSpeakers, bool detectEmotions, string? targetLanguage)
    {
        return new
        {
            type = "OBJECT",
            properties = new
            {
                summary = new { type = "STRING", description = "A concise summary of the audio content." },
                duration_estimate = new { type = "STRING", description = "Estimated duration of the audio." },
                primary_language = new { type = "STRING", description = "Primary language detected in the audio." },
                segments = new
                {
                    type = "ARRAY",
                    description = "List of transcribed segments.",
                    items = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            speaker = new { type = "STRING" },
                            timestamp = new { type = "STRING" },
                            content = new { type = "STRING" },
                            language = new { type = "STRING" },
                            translation = targetLanguage != null ? new { type = "STRING" } : null,
                            emotion = detectEmotions ? new { type = "STRING", @enum = new[] { "happy", "sad", "angry", "neutral" } } : null
                        }
                    }
                }
            },
            required = new[] { "summary", "segments" }
        };
    }

    private static string GetAnalysisPrompt(string analysisType) => analysisType switch
    {
        "music" => @"Analyze this music audio. Describe:
- Genre and style
- Tempo (BPM estimate) and time signature
- Key and mood
- Instruments identified
- Song structure (verse, chorus, bridge, etc.)
- Vocals (if any): gender, style, language
- Production quality assessment",

        "speech" => @"Analyze the speech in this audio. Describe:
- Number of speakers and their characteristics
- Speaking style (formal, casual, emotional)
- Speech clarity and pace
- Background noise assessment
- Language and accent identification
- Key topics and themes discussed",

        "sounds" => @"Analyze the sounds in this audio. Identify:
- All distinct sound sources
- Environmental context (indoor, outdoor, etc.)
- Temporal patterns (continuous, intermittent)
- Sound quality and recording conditions
- Any notable or unusual sounds",

        _ => @"Analyze this audio file comprehensively. Describe:
- Type of audio (speech, music, ambient sounds, mixed)
- Main content and topics discussed (if speech)
- Notable sounds or instruments (if music/sounds)
- Audio quality assessment
- Any notable characteristics or anomalies"
    };

    private static object BuildAnalysisSchema()
    {
        return new
        {
            type = "OBJECT",
            properties = new
            {
                audio_type = new { type = "STRING", description = "Primary type of audio content" },
                summary = new { type = "STRING", description = "Brief summary of the audio" },
                details = new { type = "STRING", description = "Detailed analysis results as text" },
                quality_assessment = new { type = "STRING", description = "Audio quality assessment" },
                notable_elements = new { type = "ARRAY", items = new { type = "STRING" }, description = "Notable elements or characteristics" }
            },
            required = new[] { "audio_type", "summary" }
        };
    }
}

public sealed record UploadedFile(string FileUri, string Name, string MimeType);

internal sealed record GeminiUploadResponse(GeminiUploadFile? File);

internal sealed record GeminiUploadFile(string Uri, string Name, string? MimeType);
