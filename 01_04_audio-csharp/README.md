# 01_04_audio-csharp

Audio transcription, analysis, and text-to-speech with Gemini (C# / .NET 10).

## Prerequisites

- .NET 10 SDK or later
- API keys (set via user secrets or environment variables)

## Run

```bash
cd 01_04_audio-csharp
dotnet run
```

## Configuration

### User Secrets (Recommended)

```bash
dotnet user-secrets set OPENAI_API_KEY your-key-here
dotnet user-secrets set GEMINI_API_KEY your-gemini-key
dotnet user-secrets set AI_PROVIDER openai
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `OPENAI_API_KEY` | OpenAI API key (for Responses API) |
| `OPENROUTER_API_KEY` | OpenRouter API key (alternative) |
| `GEMINI_API_KEY` | Gemini API key (required for audio processing) |
| `AI_PROVIDER` | Provider: `openai` or `openrouter` |

## Features

1. **Audio Transcription** - Convert speech to text with:
   - Timestamps (MM:SS format)
   - Speaker diarization
   - Emotion detection
   - Translation

2. **Audio Analysis** - Analyze audio content:
   - General analysis
   - Music analysis (genre, tempo, instruments)
   - Speech analysis (style, pace, clarity)
   - Sound identification

3. **Text-to-Speech** - Generate natural speech:
   - 30 voice options
   - Style control via natural language
   - Multi-speaker support (up to 2)

## Usage

Put source files in `workspace/input/` and run the agent:

```
You: Transcribe workspace/input/meeting.wav
You: Analyze the music in workspace/input/song.mp3
You: Generate audio: Welcome to our podcast!
```

## Project Structure

- `Program.cs` - Main entry point and REPL
- `Configuration.cs` - API key and settings management
- `Services/ResponsesApiClient.cs` - Responses API client
- `Services/GeminiClient.cs` - Gemini API for audio processing
- `Services/AudioTools.cs` - Native audio processing tools
- `Services/AgentLoop.cs` - Agent loop with tool support
- `Helpers/ConsoleLogger.cs` - Colored terminal output
- `Helpers/UsageStats.cs` - Token usage tracking
- `Models/` - API request/response models

## Notes

- Use `clear` to reset the conversation and `exit` to quit
- Large files (>20MB) automatically use Gemini upload API
- Supports local files (MP3, WAV, AIFF, AAC, OGG, FLAC) and YouTube URLs

## Original Source

Converted from `01_04_audio` (JavaScript/TypeScript)
