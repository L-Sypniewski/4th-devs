# Video Processing Agent (C# / .NET 10)

A C# implementation of a video processing agent that can analyze, transcribe, and extract structured information from videos using Google Gemini.

## Features

- **analyze_video**: Comprehensive analysis (visual + audio + content)
- **transcribe_video**: Speech with timestamps and speakers
- **extract_video**: Scenes, keyframes, objects, text
- **query_video**: Ask custom questions about video content

## Video Input

- Local files: `workspace/input/`
- YouTube URLs: work directly - no download needed
- Formats: MP4, MPEG, MOV, AVI, FLV, WebM, WMV, 3GP

## Setup

1. Copy `env.example` to `.env` in repo root
2. Set API keys:
   - `OPENAI_API_KEY` or `OPENROUTER_API_KEY`
   - `GEMINI_API_KEY`

```bash
# User secrets (recommended)
dotnet user-secrets set OPENAI_API_KEY your-key
dotnet user-secrets set GEMINI_API_KEY your-key

# Or environment variables
export OPENAI_API_KEY=your-key
export GEMINI_API_KEY=your-key
```

## Run

```bash
dotnet run
```

## Example

```
List 4 big claims from this video https://www.youtube.com/watch?v=Iar4yweKGoI
```

## Project Structure

```
├── Configuration.cs     - App configuration
├── ConsoleLogger.cs     - Logging utilities
├── UsageStats.cs        - Token tracking
├── ResponsesApiClient.cs - OpenAI API client
├── GeminiVideoService.cs - Gemini video processing
├── NativeTools.cs       - Tool definitions
├── Agent.cs             - Agent loop
├── Program.cs           - Entry point
└── README.md
```

## Original Source

Converted from `01_04_video` (JavaScript/TypeScript)
