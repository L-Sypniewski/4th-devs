---
name: ts-to-csharp
description: This skill should be used when the user asks to "convert TypeScript to C#", "convert to dotnet", "convert JS to C#", "translate TypeScript example", "port to .NET", or mentions a TypeScript/JavaScript folder for conversion to .NET. Provides guidance for converting self-contained TypeScript code examples to modern C# / .NET 10.
version: 0.1.0
---

# TypeScript to C# Conversion

Convert self-contained TypeScript/JavaScript code examples to C# / .NET 10 with proper project structure.

## Conversion Workflow

### 1. Analyze Source

Read all files in the TypeScript folder:
- `*.js`, `*.ts` files - main code
- `package.json` - dependencies and metadata
- `README.md` - documentation
- Any config files (e.g., `config.js` in parent directories)

Identify:
- Entry point (usually `app.js`, `main.js`, `index.js`)
- Helper/utility modules
- External dependencies (fetch, file system, etc.)
- Shared configuration (often in parent `config.js`)

### 2. Create Output Structure

Create a new folder with `-csharp` suffix:
```
01_01_interaction/          # Original TypeScript
01_01_interaction-csharp/   # Converted C#
├── Program.cs              # Entry point
├── *.cs                    # Additional classes/files
├── ProjectName.csproj      # Project file
└── README.md               # Updated documentation
```

### 3. Project File Template

Create a `.csproj` file with shared user secrets for API keys:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UserSecretsId>4th-devs-csharp-examples</UserSecretsId>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="9.0.0" />
  </ItemGroup>
</Project>
```

**Note:** Adjust `TargetFramework` to `net9.0` or `net8.0` if .NET 10 SDK is not available.

### 4. Code Conversion Patterns

#### JavaScript Module → C# File
```javascript
// helpers.js
export const extractResponseText = (data) => { ... };
export const toMessage = (role, content) => ({ ... });
```

```csharp
// ResponseHelpers.cs
namespace ProjectNamespace;

public static class ResponseHelpers
{
    public static string ExtractResponseText(...) { ... }
}
```

#### Async Functions
```javascript
async function chat(input, history = []) {
  const response = await fetch(...);
  const data = await response.json();
  return { text, reasoningTokens };
}
```

```csharp
public async Task<ChatResponse> ChatAsync(string input, IReadOnlyList<Message>? history = null)
{
    using var response = await _httpClient.SendAsync(request);
    var data = await response.Content.ReadFromJsonAsync<ResponseType>();
    return new ChatResponse(text, reasoningTokens);
}
```

#### Destructuring → Records
```javascript
return { text, reasoningTokens: data?.usage?.output_tokens_details?.reasoning_tokens ?? 0 };
```

```csharp
public sealed record ChatResponse(string Text, int ReasoningTokens);
```

#### Objects → Records with JsonPropertyName
```javascript
{ type: "message", role: "user", content: "Hello" }
```

```csharp
public sealed record ApiMessage
{
    [JsonPropertyName("type")]
    public string Type => "message";

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}
```

#### Environment Variables → IConfiguration
```javascript
const apiKey = process.env.OPENAI_API_KEY?.trim() ?? "";
```

```csharp
var config = new ConfigurationBuilder()
    .AddUserSecrets<Configuration>()
    .AddEnvironmentVariables()
    .Build();

var apiKey = (config["OPENAI_API_KEY"] ?? "").Trim();
```

#### Fetch → HttpClient
```javascript
const response = await fetch(url, {
  method: "POST",
  headers: { "Authorization": `Bearer ${apiKey}` },
  body: JSON.stringify(data)
});
```

```csharp
using var request = new HttpRequestMessage(HttpMethod.Post, url);
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
request.Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
using var response = await _httpClient.SendAsync(request);
```

### 5. C# / .NET 10 Features to Use

- **File-scoped namespaces**: `namespace MyNamespace;`
- **Primary constructors**: `public class ChatClient(Configuration config)`
- **Required properties**: `public required string ApiKey { get; init; }`
- **Records**: `public sealed record Message(string Role, string Content);`
- **Collection expressions**: `List<Message> messages = [];`
- **Nullable reference types**: Enable `<Nullable>enable</Nullable>`
- **Implicit usings**: Enable `<ImplicitUsings>enable</ImplicitUsings>`
- **Snake case JSON**: `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`

### 6. README Template

```markdown
# project-name-csharp

Brief description (C# / .NET 10).

## Prerequisites

- .NET 10 SDK or later
- API key (set via user secrets or environment variable)

## Run

```bash
dotnet run
```

## Configuration

### User Secrets (Recommended)

```bash
dotnet user-secrets set OPENAI_API_KEY your-key-here
dotnet user-secrets set AI_PROVIDER openai
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `OPENAI_API_KEY` | OpenAI API key |
| `OPENROUTER_API_KEY` | OpenRouter API key |

## Project Structure

- `Program.cs` - Main entry point
- `*.cs` - Additional files

## Original Source

Converted from `original-folder` (JavaScript/TypeScript)
```

## Validation Checklist

Before completing conversion:

- [ ] All TypeScript files analyzed and converted
- [ ] `.csproj` file created with shared `UserSecretsId`
- [ ] Code compiles: `dotnet build`
- [ ] Code runs (may fail without API keys, but should fail gracefully)
- [ ] README updated with C# instructions
- [ ] Original TypeScript folder preserved (not deleted)

## Additional Resources

### Reference Files

- **`references/json-patterns.md`** - Detailed JSON serialization patterns
- **`references/async-patterns.md`** - Async/await patterns in C#

### Example Files

- **`examples/interaction-example/`** - Complete converted project example
