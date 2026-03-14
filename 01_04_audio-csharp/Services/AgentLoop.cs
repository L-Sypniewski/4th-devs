using System.Text.Json;
using AudioAgent.Helpers;
using AudioAgent.Models;

namespace AudioAgent.Services;

/// <summary>
/// Agent loop — chat → tool calls → results cycle until completion.
/// </summary>
public sealed class AgentLoop(ResponsesApiClient apiClient, AudioTools audioTools)
{
    private const int MaxSteps = 50;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private const string SystemInstructions = @"You are an autonomous audio processing agent.

## GOAL
Process, transcribe, analyze, and generate audio. Handle speech-to-text, audio analysis, and text-to-speech tasks.

## RESOURCES
- workspace/input/   → Source audio files to process
- workspace/output/  → Generated audio, transcriptions, and analysis results

## TOOLS
- transcribe_audio: Convert speech to text with timestamps, speaker detection, emotion detection, translation
- analyze_audio: Analyze audio content (music, speech patterns, sound identification)
- query_audio: Ask any custom question about audio content
- generate_audio: Text-to-speech generation (single or multi-speaker)

## AUDIO INPUT
Supported sources:
- Local files: workspace/input/audio.mp3 (MP3, WAV, AIFF, AAC, OGG, FLAC)
- YouTube URLs: https://www.youtube.com/watch?v=... or https://youtu.be/...

Max length: 9.5 hours for local files, ~1-3 hours for YouTube (context limit)

Transcription features:
- Speaker diarization (identify who is speaking)
- Timestamps (MM:SS format)
- Language detection and translation
- Emotion detection (happy, sad, angry, neutral)

Analysis types:
- general: Comprehensive overview
- music: Genre, tempo, instruments, structure
- speech: Speaker characteristics, clarity, pace
- sounds: Sound source identification

## TEXT-TO-SPEECH
Generate natural speech with controllable style, tone, pace, and accent.

Voices (30 available):
- Kore (Firm), Puck (Upbeat), Charon (Informative), Aoede (Breezy)
- Fenrir (Excitable), Enceladus (Breathy), Sulafat (Warm), etc.

Style control via natural language:
- ""Say cheerfully: Hello!"" → happy tone
- ""In a whisper: The secret..."" → soft, quiet
- ""Speak slowly and dramatically: The end."" → pacing control

Multi-speaker (up to 2):
- Format: ""Speaker1: Hello! Speaker2: Hi there!""
- Assign different voices to each speaker

## WORKFLOW

1. UNDERSTAND THE REQUEST
   - Transcription? → transcribe_audio
   - Analysis? → analyze_audio
   - Generate speech? → generate_audio
   - Custom question? → query_audio

2. FOR GENERATION
   - Choose appropriate voice for the content/mood
   - Include style directions in the text prompt
   - For dialogue, use multi-speaker with distinct voices

3. DELIVER RESULTS
   - Save to workspace/output/ when requested
   - Return file paths and summaries

## RULES

1. Check workspace/input/ for available source files
2. Large files (>20MB) use upload API automatically
3. For TTS, match voice personality to content
4. Save outputs with descriptive names
5. Report output paths clearly

Run autonomously. Be creative with voice generation.";

    public async Task<AgentResult> RunAsync(
        string query,
        List<object>? conversationHistory = null,
        CancellationToken cancellationToken = default)
    {
        var tools = AudioTools.GetToolDefinitions();
        var messages = conversationHistory ?? [];
        messages.Add(new { role = "user", content = query });

        ConsoleLogger.Query(query);

        for (var step = 1; step <= MaxSteps; step++)
        {
            ConsoleLogger.Api($"Step {step}", messages.Count);

            var response = await apiClient.ChatAsync(
                messages,
                tools,
                SystemInstructions,
                16384,
                cancellationToken);

            ConsoleLogger.ApiDone(response.Usage != null
                ? new UsageStats { InputTokens = response.Usage.InputTokens, OutputTokens = response.Usage.OutputTokens }
                : null);

            var toolCalls = apiClient.ExtractToolCalls(response);

            if (toolCalls.Count == 0)
            {
                var text = apiClient.ExtractText(response) ?? "No response";
                if (response.Output != null)
                    messages.AddRange(response.Output);

                return new AgentResult(text, messages);
            }

            if (response.Output != null)
                messages.AddRange(response.Output);

            var results = await RunToolsAsync(toolCalls, cancellationToken);
            messages.AddRange(results);
        }

        throw new Exception($"Max steps ({MaxSteps}) reached");
    }

    private async Task<List<ApiFunctionCallOutput>> RunToolsAsync(
        List<ApiFunctionCall> toolCalls,
        CancellationToken cancellationToken)
    {
        var results = new List<ApiFunctionCallOutput>();

        foreach (var toolCall in toolCalls)
        {
            var args = JsonDocument.Parse(toolCall.Arguments).RootElement;
            ConsoleLogger.Tool(toolCall.Name, args);

            try
            {
                var result = await audioTools.ExecuteToolAsync(toolCall.Name, args, cancellationToken);
                var output = JsonSerializer.Serialize(result, JsonOptions);
                ConsoleLogger.ToolResult(toolCall.Name, true, output);
                results.Add(new ApiFunctionCallOutput { CallId = toolCall.CallId, Output = output });
            }
            catch (Exception ex)
            {
                var output = JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
                ConsoleLogger.ToolResult(toolCall.Name, false, ex.Message);
                results.Add(new ApiFunctionCallOutput { CallId = toolCall.CallId, Output = output });
            }
        }

        return results;
    }
}

public sealed record AgentResult(string Response, List<object> ConversationHistory);
