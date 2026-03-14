using System.Text;
using System.Text;
using System.Text.Json;
using AudioAgent.Helpers;
using AudioAgent.Models;

namespace AudioAgent.Services;

/// <summary>
/// Native audio processing tools for transcription, analysis, and TTS.
/// </summary>
public sealed class AudioTools(GeminiClient geminiClient, string projectRoot)
{
    private const int InlineSizeLimit = 20 * 1024 * 1024; // 20MB
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        [".mp3"] = "audio/mp3",
        [".wav"] = "audio/wav",
        [".aiff"] = "audio/aiff",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".flac"] = "audio/flac",
        [".m4a"] = "audio/mp4",
        [".webm"] = "audio/webm"
    };

    public static List<ApiTool> GetToolDefinitions()
    {
        var voices = string.Join(", ", GeminiClient.TtsVoices.Select(kv => $"{kv.Key} ({kv.Value})"));

        return
        [
            new ApiTool
            {
                Name = "transcribe_audio",
                Description = "Transcribe audio to text with timestamps and speaker detection. Supports local files (MP3, WAV, AIFF, AAC, OGG, FLAC) and YouTube URLs. Can detect speakers, emotions, and translate to other languages.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        audio_path = new { type = "string", description = "Path to audio file relative to project root (e.g., workspace/input/recording.mp3) OR a YouTube URL" },
                        include_timestamps = new { type = "boolean", description = "Include timestamps for each segment. Default: true" },
                        detect_speakers = new { type = "boolean", description = "Identify and label different speakers. Default: true" },
                        detect_emotions = new { type = "boolean", description = "Detect speaker emotions (happy, sad, angry, neutral). Default: false" },
                        translate_to = new { type = "string", description = "Target language for translation (e.g., 'English', 'Spanish'). If not provided, keeps original language." },
                        output_name = new { type = "string", description = "Optional base name for saving transcription JSON to workspace/output/" }
                    },
                    required = new[] { "audio_path" }
                }
            },
            new ApiTool
            {
                Name = "analyze_audio",
                Description = "Analyze audio content - identify sounds, music characteristics, speech patterns, or general audio analysis. Supports local files and YouTube URLs. Does NOT transcribe - use transcribe_audio for that.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        audio_path = new { type = "string", description = "Path to audio file relative to project root OR a YouTube URL" },
                        analysis_type = new { type = "string", @enum = new[] { "general", "music", "speech", "sounds" }, description = "Type of analysis: 'general' (comprehensive), 'music' (genre, tempo, instruments), 'speech' (speakers, style, clarity), 'sounds' (identify sound sources). Default: general" },
                        custom_prompt = new { type = "string", description = "Optional custom analysis prompt to override the default for the analysis type" },
                        output_name = new { type = "string", description = "Optional base name for saving analysis JSON to workspace/output/" }
                    },
                    required = new[] { "audio_path" }
                }
            },
            new ApiTool
            {
                Name = "query_audio",
                Description = "Ask any question about audio content. Supports local files and YouTube URLs. Use for custom queries that don't fit transcribe or analyze patterns.",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        audio_path = new { type = "string", description = "Path to audio file relative to project root OR a YouTube URL" },
                        question = new { type = "string", description = "Question or prompt about the audio content" }
                    },
                    required = new[] { "audio_path", "question" }
                }
            },
            new ApiTool
            {
                Name = "generate_audio",
                Description = $"Generate speech audio from text using Gemini TTS. Supports single-speaker and multi-speaker (up to 2) generation. Style, tone, pace, and accent are controllable via natural language in the text prompt.\n\nAvailable voices: {voices}\n\nFor style control, include directions in the text like: \"Say cheerfully: Hello!\" or \"In a whisper: The secret is...\"\nFor multi-speaker, format as dialogue: \"Speaker1: Hello! Speaker2: Hi there!\"",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string", description = "Text to convert to speech. Include style directions for tone/pace control. For multi-speaker, use 'SpeakerName: dialogue' format." },
                        voice = new { type = "string", description = "Voice name for single-speaker. Options: Kore (Firm), Puck (Upbeat), Charon (Informative), Aoede (Breezy), Fenrir (Excitable), etc. Default: Kore" },
                        speakers = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    speaker = new { type = "string", description = "Speaker name as used in the text" },
                                    voice = new { type = "string", description = "Voice name for this speaker" }
                                },
                                required = new[] { "speaker", "voice" }
                            },
                            description = "For multi-speaker: array of {speaker, voice} mappings. Max 2 speakers."
                        },
                        output_name = new { type = "string", description = "Base name for output WAV file (saved to workspace/output/)" }
                    },
                    required = new[] { "text", "output_name" }
                }
            }
        ];
    }

    public async Task<object> ExecuteToolAsync(string name, JsonElement args, CancellationToken cancellationToken = default)
    {
        return name switch
        {
            "transcribe_audio" => await TranscribeAudioAsync(args, cancellationToken),
            "analyze_audio" => await AnalyzeAudioAsync(args, cancellationToken),
            "query_audio" => await QueryAudioAsync(args, cancellationToken),
            "generate_audio" => await GenerateAudioAsync(args, cancellationToken),
            _ => new { success = false, error = $"Unknown tool: {name}" }
        };
    }

    private async Task<object> TranscribeAudioAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var audioPath = args.GetProperty("audio_path").GetString() ?? "";
        var includeTimestamps = args.TryGetProperty("include_timestamps", out var ts) && ts.GetBoolean();
        var detectSpeakers = !args.TryGetProperty("detect_speakers", out var ds) || ds.GetBoolean();
        var detectEmotions = args.TryGetProperty("detect_emotions", out var de) && de.GetBoolean();
        var translateTo = args.TryGetProperty("translate_to", out var tt) ? tt.GetString() : null;
        var outputName = args.TryGetProperty("output_name", out var on) ? on.GetString() : null;

        ConsoleLogger.Tool("transcribe_audio", new { audio_path = audioPath, timestamps = includeTimestamps, speakers = detectSpeakers });

        try
        {
            var audio = await LoadAudioAsync(audioPath, cancellationToken);

            var result = await geminiClient.TranscribeAudioAsync(
                audio.FileUri, audio.AudioBase64, audio.MimeType,
                includeTimestamps, detectSpeakers, detectEmotions, translateTo,
                cancellationToken);

            if (outputName != null)
            {
                var outputPath = await SaveOutputAsync(outputName, "json", result, cancellationToken);
                ConsoleLogger.Success($"Transcription saved: {outputPath}");
                return new { success = true, audio_path = audioPath, output_path = outputPath, transcription = result };
            }

            ConsoleLogger.Success($"Transcribed: {result.Segments.Count} segments");
            return new { success = true, audio_path = audioPath, transcription = result };
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("transcribe_audio", ex.Message);
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object> AnalyzeAudioAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var audioPath = args.GetProperty("audio_path").GetString() ?? "";
        var analysisType = args.TryGetProperty("analysis_type", out var at) ? at.GetString() ?? "general" : "general";
        var customPrompt = args.TryGetProperty("custom_prompt", out var cp) ? cp.GetString() : null;
        var outputName = args.TryGetProperty("output_name", out var on) ? on.GetString() : null;

        ConsoleLogger.Tool("analyze_audio", new { audio_path = audioPath, analysis_type = analysisType });

        try
        {
            var audio = await LoadAudioAsync(audioPath, cancellationToken);
            var result = await geminiClient.AnalyzeAudioAsync(
                audio.FileUri, audio.AudioBase64, audio.MimeType,
                analysisType, customPrompt, cancellationToken);

            if (outputName != null)
            {
                var outputPath = await SaveOutputAsync(outputName, "json", result, cancellationToken);
                ConsoleLogger.Success($"Analysis saved: {outputPath}");
                return new { success = true, audio_path = audioPath, analysis_type = analysisType, output_path = outputPath, analysis = result };
            }

            ConsoleLogger.Success($"Analyzed: {result.AudioType}");
            return new { success = true, audio_path = audioPath, analysis_type = analysisType, analysis = result };
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("analyze_audio", ex.Message);
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object> QueryAudioAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var audioPath = args.GetProperty("audio_path").GetString() ?? "";
        var question = args.GetProperty("question").GetString() ?? "";

        ConsoleLogger.Tool("query_audio", new { audio_path = audioPath, question = question[..Math.Min(50, question.Length)] + "..." });

        try
        {
            var audio = await LoadAudioAsync(audioPath, cancellationToken);
            var result = await geminiClient.ProcessAudioAsync(
                audio.FileUri, audio.AudioBase64, audio.MimeType,
                question, null, cancellationToken);

            ConsoleLogger.Success($"Query answered ({result.Length} chars)");
            return new { success = true, audio_path = audioPath, question, answer = result };
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("query_audio", ex.Message);
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<object> GenerateAudioAsync(JsonElement args, CancellationToken cancellationToken)
    {
        var text = args.GetProperty("text").GetString() ?? "";
        var voice = args.TryGetProperty("voice", out var v) ? v.GetString() ?? "Kore" : "Kore";
        var outputName = args.GetProperty("output_name").GetString() ?? "output";

        List<(string Speaker, string Voice)>? speakers = null;
        if (args.TryGetProperty("speakers", out var sp) && sp.ValueKind == JsonValueKind.Array)
        {
            speakers = [];
            foreach (var item in sp.EnumerateArray())
            {
                speakers.Add((
                    item.GetProperty("speaker").GetString() ?? "",
                    item.GetProperty("voice").GetString() ?? "Kore"
                ));
            }
        }

        var isMultiSpeaker = speakers != null && speakers.Count > 0;
        ConsoleLogger.Tool("generate_audio", new
        {
            mode = isMultiSpeaker ? "multi-speaker" : "single-speaker",
            voice = isMultiSpeaker ? string.Join(", ", speakers!.Select(s => s.Voice)) : voice,
            text_length = text.Length
        });

        try
        {
            TtsResult result;
            if (isMultiSpeaker)
            {
                if (speakers!.Count > 2)
                    return new { success = false, error = "Maximum 2 speakers supported for multi-speaker TTS" };

                result = await geminiClient.GenerateMultiSpeakerSpeechAsync(text, speakers!, cancellationToken);
            }
            else
            {
                if (!GeminiClient.TtsVoices.ContainsKey(voice))
                {
                    var validVoices = string.Join(", ", GeminiClient.TtsVoices.Keys);
                    return new { success = false, error = $"Invalid voice \"{voice}\". Valid options: {validVoices}" };
                }
                result = await geminiClient.GenerateSpeechAsync(text, voice, cancellationToken);
            }

            var filename = $"{outputName}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.wav";
            var outputPath = Path.Combine(projectRoot, "workspace", "output", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await WriteWavFileAsync(outputPath, result.Data, cancellationToken);

            var relativePath = $"workspace/output/{filename}";
            ConsoleLogger.Success($"Audio saved: {relativePath}");

            object voiceInfo = isMultiSpeaker
                ? speakers!.Select(s => new { speaker = s.Speaker, voice = s.Voice }).ToList()
                : voice;

            return new
            {
                success = true,
                mode = isMultiSpeaker ? "multi-speaker" : "single-speaker",
                output_path = relativePath,
                absolute_path = outputPath,
                project_root = projectRoot,
                voice = voiceInfo,
                text_length = text.Length,
                format = "WAV (24kHz, 16-bit, mono)"
            };
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("generate_audio", ex.Message);
            return new { success = false, error = ex.Message };
        }
    }

    private async Task<LoadedAudio> LoadAudioAsync(string audioPath, CancellationToken cancellationToken)
    {
        // Handle YouTube URLs
        if (audioPath.Contains("youtube.com/watch") || audioPath.Contains("youtu.be/"))
        {
            ConsoleLogger.Info("YouTube URL detected");
            return new LoadedAudio(null, null, "video/mp4", audioPath);
        }

        var fullPath = Path.Combine(projectRoot, audioPath);
        var buffer = await File.ReadAllBytesAsync(fullPath, cancellationToken);
        var ext = Path.GetExtension(audioPath).ToLowerInvariant();
        var mimeType = MimeTypes.GetValueOrDefault(ext, "audio/mpeg");
        var displayName = Path.GetFileName(audioPath);

        if (buffer.Length > InlineSizeLimit)
        {
            ConsoleLogger.Info("Audio file > 20MB, using upload API...");
            var uploaded = await geminiClient.UploadAudioFileAsync(buffer, mimeType, displayName, cancellationToken);
            return new LoadedAudio(null, null, mimeType, uploaded.FileUri);
        }

        return new LoadedAudio(Convert.ToBase64String(buffer), null, mimeType, null);
    }

    private async Task<string> SaveOutputAsync(string baseName, string extension, object data, CancellationToken cancellationToken)
    {
        var outputDir = Path.Combine(projectRoot, "workspace", "output");
        Directory.CreateDirectory(outputDir);

        var filename = $"{baseName}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.{extension}";
        var outputPath = Path.Combine(outputDir, filename);
        var content = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(outputPath, content, cancellationToken);

        return $"workspace/output/{filename}";
    }

    private static async Task WriteWavFileAsync(string filepath, byte[] pcmBuffer, CancellationToken cancellationToken)
    {
        const int sampleRate = 24000;
        const int numChannels = 1;
        const int bitsPerSample = 16;
        var byteRate = sampleRate * numChannels * (bitsPerSample / 8);
        var blockAlign = numChannels * (bitsPerSample / 8);
        var dataSize = pcmBuffer.Length;
        const int headerSize = 44;

        var wavBuffer = new byte[headerSize + dataSize];

        // RIFF header
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wavBuffer, 0);
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(4), 36 + dataSize);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wavBuffer, 8);

        // fmt chunk
        Encoding.ASCII.GetBytes("fmt ").CopyTo(wavBuffer, 12);
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(16), (ushort)16);  // chunk size
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(20), (ushort)1);   // PCM format
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(22), (ushort)numChannels);
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(24), (uint)sampleRate);
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(28), (uint)byteRate);
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(32), (ushort)blockAlign);
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(34), (ushort)bitsPerSample);

        // data chunk
        Encoding.ASCII.GetBytes("data").CopyTo(wavBuffer, 36);
        BitConverter.TryWriteBytes(wavBuffer.AsSpan(40), dataSize);
        pcmBuffer.CopyTo(wavBuffer, headerSize);

        await File.WriteAllBytesAsync(filepath, wavBuffer, cancellationToken);
    }

    private sealed record LoadedAudio(string? AudioBase64, string? LocalPath, string MimeType, string? FileUri);
}
