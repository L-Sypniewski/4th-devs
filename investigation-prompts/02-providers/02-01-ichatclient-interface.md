# Investigation: IChatClient Interface

**Status**: Pending
**Priority**: High
**Category**: Core Abstraction

---

## Source Files

### TypeScript Reference
- [providers/types.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/types.ts) (79 lines)
  - `Provider` interface (lines 60-64)
  - `ProviderRequest` interface (lines 6-15)
  - `ProviderResponse` interface (lines 23-29)
  - `ProviderStreamEvent` type (lines 46-58)

### Key TypeScript Patterns
```typescript
export interface Provider {
  name: string
  generate(request: ProviderRequest): Promise<ProviderResponse>
  stream(request: ProviderRequest): AsyncIterable<ProviderStreamEvent>
}
```

---

## Microsoft Documentation

### Primary References
- [IChatClient Interface](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.ichatclient)
- [IChatClient Overview](https://learn.microsoft.com/dotnet/ai/ichatclient)

### Related Types
- `ChatResponse` - Response structure
- `ChatMessage` - Message abstraction
- `ChatClientBuilder` - Middleware pipeline builder

---

## Investigation Questions

### 1. Interface Comparison: TS Provider vs IChatClient
- How does the TS `Provider` interface compare to `IChatClient`?
- What are the method signature differences?
- How does naming map (generate -> CompleteAsync)?

### 2. Core Methods
- What methods does `IChatClient` provide?
  - `CompleteAsync()` - non-streaming
  - `CompleteStreamingAsync()` - streaming
- What is the return type structure?

### 3. Response Structure Comparison
- How is `ChatResponse` structured vs TS `ProviderResponse`?
- What properties exist on `ChatResponse`?
- How are `ChatMessage` and content parts represented?

### 4. ChatClientBuilder and Middleware
- What is the role of `ChatClientBuilder` for middleware pipeline?
- How does the decorator pattern work in .NET?
- How does this compare to TS provider composition?

---

## Code Patterns

### Provider Registration (TypeScript)
```typescript
// registry.ts
const providers = new Map<string, Provider>()

export function registerProvider(provider: Provider): void {
  providers.set(provider.name, provider)
}

export function resolveProvider(modelString: string) {
  const parsed = parseModelString(modelString) // "openai:gpt-4"
  return { provider: providers.get(parsed.providerName), model: parsed.model }
}
```

### Dependency Injection Pattern (Expected .NET)
```csharp
// Expected DI registration pattern
services.AddScoped<IChatClient>(sp => {
    var config = sp.GetRequiredService<IConfiguration>();
    return new OpenAIClient(config["OpenAI:ApiKey"])
        .GetChatClient("gpt-4")
        .AsIChatClient();
});

// Or using ChatClientBuilder
services.AddScoped<IChatClient>(sp => {
    return new ChatClientBuilder()
        .UseLogging()
        .UseRetry()
        .Build(/* inner client */);
});
```

---

## Interface Mapping Table

| TypeScript | .NET (Microsoft.Extensions.AI) | Notes |
|------------|-------------------------------|-------|
| `Provider` | `IChatClient` | Core abstraction |
| `generate()` | `CompleteAsync()` | Non-streaming completion |
| `stream()` | `CompleteStreamingAsync()` | Returns `IAsyncEnumerable` |
| `ProviderRequest` | `ChatMessage[]` + options | Request structure differs |
| `ProviderResponse` | `ChatResponse` | Response wrapper |
| `ProviderStreamEvent` | `StreamingChatCompletionUpdate` | Streaming chunks |
| `ProviderInputItem` | `ChatMessage` | Input items |
| `ProviderOutputItem` | `ChatMessage` (from response) | Output items |
| `AbortSignal` | `CancellationToken` | Cancellation mechanism |

---

## Expected Deliverables

1. **Interface Mapping Document**
   - Complete comparison table of TS vs .NET types
   - Method signature mappings
   - Property name translations

2. **DI Registration Code**
   - Service collection extension methods
   - Configuration binding patterns
   - Named/typed client registration

3. **Base Provider Implementation**
   - Abstract base class implementing common patterns
   - Logging and telemetry hooks
   - Error handling wrapper

---

## Notes

- The TS `Provider` interface is intentionally minimal (2 methods)
- .NET's `IChatClient` may have more built-in patterns
- Need to investigate `ChatClientBuilder` middleware approach
- Consider how `Microsoft.Extensions.AI` abstractions work with dependency injection
