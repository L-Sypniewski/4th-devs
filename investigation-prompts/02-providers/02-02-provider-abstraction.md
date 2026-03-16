# Investigation: Multi-Provider Abstraction

**Status**: Pending
**Priority**: High
**Category**: Core Architecture

---

## Source Files

### TypeScript Provider Implementations

#### Provider Registry
- [providers/registry.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/registry.ts) (40 lines)
  - Simple Map-based registry
  - Model string parsing ("provider:model" format)
  - Provider resolution

#### OpenAI Provider
- [providers/openai/adapter.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/openai/adapter.ts) (353 lines)
  - Input/output mapping
  - Stream accumulation
  - Web search support

#### Gemini Provider
- [providers/gemini/adapter.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/gemini/adapter.ts)
  - Google AI SDK integration

#### Provider Exports
- [providers/index.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/index.ts)
  ```typescript
  export { createOpenAIProvider, type OpenAIConfig } from './openai/index.js'
  export { createGeminiProvider, type GeminiConfig } from './gemini/index.js'
  ```

---

## TypeScript Multi-Provider Pattern

### Provider Factory Pattern
```typescript
// Provider creation with configuration
export function createOpenAIProvider(config: OpenAIConfig): Provider {
  const client = new OpenAI({ apiKey: config.apiKey, baseURL: config.baseUrl })

  return {
    name: config.name ?? 'openai',
    async generate(request) { /* ... */ },
    async *stream(request) { /* ... */ },
  }
}

// Configuration-based selection
export function resolveProvider(modelString: string) {
  const { providerName, model } = parseModelString(modelString) // "openrouter:gpt-4"
  const provider = providers.get(providerName)
  return { provider, model }
}
```

### Model String Convention
```
openai:gpt-4o           -> OpenAI provider, gpt-4o model
openrouter:anthropic/claude-3-opus  -> OpenRouter, Claude via OpenRouter
gemini:gemini-2.0-flash -> Gemini provider, gemini-2.0-flash model
```

---

## Investigation Questions

### 1. TypeScript Multi-Provider Architecture
- How does TS support multiple LLM providers?
- What is the adapter pattern used?
- How are provider-specific features handled (web search, reasoning)?

### 2. .NET Provider Packages
- What NuGet packages exist for:
  - OpenAI (`OpenAI` or `Azure.AI.OpenAI`)
  - Anthropic (`Anthropic.SDK` or similar)
  - Azure OpenAI (`Azure.AI.OpenAI`)
- How do these implement `IChatClient`?

### 3. ChatClientBuilder for Provider-Agnostic Code
- How does `ChatClientBuilder` enable provider-agnostic code?
- What middleware components are built-in?
- How to write code that works with any provider?

---

## Microsoft Documentation

### Package References
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI)
- [Microsoft.Extensions.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI)
- [Azure.AI.OpenAI](https://www.nuget.org/packages/Azure.AI.OpenAI)

---

## Code Patterns

### Factory Pattern (TypeScript)
```typescript
// Configuration interface
export interface OpenAIConfig {
  name?: string
  apiKey: string
  baseUrl?: string
  defaultModel?: string
  webSearchMode?: 'tool' | 'model_suffix'
}

// Factory function
export function createOpenAIProvider(config: OpenAIConfig): Provider {
  // Returns Provider implementation
}
```

### Expected .NET Factory Pattern
```csharp
// Configuration class
public class OpenAIProviderOptions
{
    public string ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string DefaultModel { get; set; } = "gpt-4o";
}

// Factory method / extension
public static class OpenAIProviderExtensions
{
    public static IChatClient CreateOpenAIChatClient(
        this OpenAIProviderOptions options)
    {
        var client = new OpenAIClient(options.ApiKey);
        return client.GetChatClient(options.DefaultModel).AsIChatClient();
    }
}

// DI registration
public static IServiceCollection AddOpenAIChatClient(
    this IServiceCollection services,
    string configSection = "OpenAI")
{
    services.AddOptions<OpenAIProviderOptions>()
        .BindConfiguration(configSection);

    services.AddScoped<IChatClient>(sp =>
    {
        var options = sp.GetRequiredService<OpenAIProviderOptions>();
        return options.CreateOpenAIChatClient();
    });

    return services;
}
```

### Configuration-Based Selection
```csharp
// appsettings.json
{
  "AI": {
    "Provider": "OpenAI",  // or "Anthropic", "Azure"
    "OpenAI": { "ApiKey": "...", "DefaultModel": "gpt-4o" },
    "Anthropic": { "ApiKey": "...", "DefaultModel": "claude-3-opus" }
  }
}

// Provider factory
public interface IChatClientFactory
{
    IChatClient Create(string providerName);
    IChatClient CreateForModel(string modelString); // "openai:gpt-4o"
}
```

---

## Provider Comparison

| Feature | OpenAI | Anthropic | Azure OpenAI | Gemini |
|---------|--------|-----------|--------------|--------|
| Streaming | Yes | Yes | Yes | Yes |
| Tool Calling | Yes | Yes | Yes | Yes |
| Vision | Yes | Yes | Yes | Yes |
| Web Search | Built-in | Via tool | Via Bing | Built-in |
| Reasoning | o-series | Claude 3.5 | o-series | Gemini 2.0 |
| Official .NET SDK | Yes | Community | Yes | Yes |

---

## Expected Deliverables

### 1. Provider Factory Pattern in C#
```csharp
// Core factory interface
public interface IProviderFactory
{
    IChatClient Create(ProviderConfiguration config);
    IEnumerable<string> SupportedProviders { get; }
}

// Implementation supporting multiple providers
public class CompositeProviderFactory : IProviderFactory
{
    private readonly Dictionary<string, Func<ProviderConfiguration, IChatClient>> _factories;

    public IChatClient Create(ProviderConfiguration config)
    {
        if (_factories.TryGetValue(config.Provider, out var factory))
            return factory(config);
        throw new NotSupportedException($"Provider {config.Provider} not supported");
    }
}
```

### 2. Configuration Binding
- Options classes for each provider
- Configuration validation
- Secrets management integration

### 3. DI Extensions
- `AddOpenAIChatClient()`
- `AddAnthropicChatClient()`
- `AddAzureOpenAIChatClient()`
- `AddChatClientFromConfiguration()`

---

## Notes

- TS uses simple factory functions, .NET should use DI patterns
- Consider using `IOptions<T>` for configuration
- May need abstraction for provider-specific features (reasoning, web search)
- `ChatClientBuilder` can add cross-cutting concerns (logging, retry, telemetry)
