# 01_01_interaction-csharp

Multi-turn conversation with the Responses API using full input history (C# / .NET 10).

## Prerequisites

- .NET 10 SDK or later
- API key (set via user secrets or environment variable)

## Run

```bash
dotnet run
```

## What it does

1. Sends a question: "What is 25 * 48?"
2. Sends a follow-up: "Divide that by 4." with the previous exchange as context
3. Prints both answers with reasoning token counts

## Configuration

### User Secrets (Recommended)

Set your API key securely using .NET user secrets (shared across all C# examples):

```bash
dotnet user-secrets set OPENAI_API_KEY your-key-here
dotnet user-secrets set AI_PROVIDER openai
```

Or for OpenRouter:

```bash
dotnet user-secrets set OPENROUTER_API_KEY your-key-here
dotnet user-secrets set AI_PROVIDER openrouter
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `OPENAI_API_KEY` | OpenAI API key |
| `OPENROUTER_API_KEY` | OpenRouter API key |
| `AI_PROVIDER` | Optional: `openai` or `openrouter` |
| `OPENROUTER_HTTP_REFERER` | Optional: HTTP-Referer header for OpenRouter |
| `OPENROUTER_APP_NAME` | Optional: X-Title header for OpenRouter |

## Project Structure

- `Program.cs` - Main entry point with conversation flow
- `ChatClient.cs` - HTTP client for the Responses API
- `Configuration.cs` - Configuration loading and data models
- `ResponseHelpers.cs` - Helper methods for response parsing
- `InteractionExample.csproj` - Project file

## Original Source

Converted from `01_01_interaction` (JavaScript/Node.js)
