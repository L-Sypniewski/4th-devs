# Investigation: High-Level TypeScript to .NET Architecture Mapping

## Objective
Map TypeScript components from 01_05_agent to .NET equivalents using Microsoft.Extensions.AI and Microsoft Agent Framework.

---

## Source Files

- **Runner**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
- **Agent Domain**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/domain/agent.ts
- **Provider Types**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/types.ts

---

## Core Mapping Table

| TypeScript Component | C# / .NET Equivalent | Mapping Type |
|---------------------|----------------------|--------------|
| `Provider` interface | `IChatClient` (Microsoft.Extensions.AI) | ✅ Direct |
| `Provider.generate()` | `IChatClient.GetResponseAsync()` | ✅ Direct |
| `Provider.stream()` | `IChatClient.GetStreamingResponseAsync()` | ✅ Direct |
| `ProviderRegistry` | DI + `IChatClient` factory | ✅ Direct |
| `ToolRegistry` | `AIFunctionFactory` + custom registry | ⚠️ Extend |
| `EventEmitter` | `IObservable<T>` / Agent middleware | ✅ Direct |
| `AbortSignal` | `CancellationToken` | ✅ Direct |
| `AsyncIterable<T>` | `IAsyncEnumerable<T>` | ✅ Direct |

---

## Agent Class Mapping

### TypeScript Agent Entity
```typescript
// src/domain/agent.ts
interface Agent {
  id: AgentId
  sessionId: SessionId
  status: AgentStatus  // pending | running | waiting | completed | failed | cancelled
  config: AgentConfig
  waitingFor: WaitingFor[]
  result?: unknown
  error?: string
  turnCount: number
  usage?: TokenUsage
}
```

### C# Equivalent
```csharp
public class Agent
{
    public string Id { get; set; }
    public string SessionId { get; set; }
    public AgentStatus Status { get; set; }
    public AgentConfig Config { get; set; } = new();
    public List<WaitingFor> WaitingFor { get; set; } = [];
    public object? Result { get; set; }
    public string? Error { get; set; }
    public int TurnCount { get; set; }
    public TokenUsage? Usage { get; set; }
}

public enum AgentStatus
{
    Pending,
    Running,
    Waiting,
    Completed,
    Failed,
    Cancelled
}
```

---

## Provider Pattern Mapping

### TypeScript Provider Interface
```typescript
// src/providers/types.ts:60-64
interface Provider {
  name: string
  generate(request: ProviderRequest): Promise<ProviderResponse>
  stream(request: ProviderRequest): AsyncIterable<ProviderStreamEvent>
}
```

### C# IChatClient Interface
```csharp
// Microsoft.Extensions.AI
public interface IChatClient : IDisposable
{
    Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    ChatClientMetadata Metadata { get; }
}
```

### Provider Registration (DI)
```csharp
// Use DI instead of manual registry
builder.Services.AddChatClient(services =>
{
    var baseClient = new AzureOpenAIClient(endpoint, credential)
        .GetChatClient(deployment)
        .AsIChatClient();

    return baseClient
        .AsBuilder()
        .UseRateLimiting()
        .UseOpenTelemetry()
        .Build(services);
});
```

---

## Request/Response Type Mapping

| TypeScript | C# Equivalent |
|------------|---------------|
| `ProviderRequest` | `IEnumerable<ChatMessage>` + `ChatOptions` |
| `ProviderResponse` | `ChatResponse` |
| `ProviderStreamEvent` | `ChatResponseUpdate` |
| `ProviderInputItem` | `ChatMessage` with `AIContent` variants |
| `ProviderOutputItem` | `AIContent` (TextContent, FunctionCallContent) |
| `ToolDefinition` | `AIFunction` |

---

## State Machine Mapping

### TypeScript States
```typescript
type AgentStatus = 'pending' | 'running' | 'waiting' | 'completed' | 'failed' | 'cancelled'
```

### C# Enum
```csharp
public enum AgentStatus
{
    Pending,    // Agent created, not started
    Running,    // Actively executing turns
    Waiting,    // Paused for external input (non-blocking)
    Completed,  // Successfully finished
    Failed,     // Error during execution
    Cancelled   // Aborted via signal
}
```

---

## Tool System Mapping

### TypeScript Tool Types
```typescript
type ToolType = 'sync' | 'async' | 'agent' | 'human'
```

### C# Implementations

| TS Tool Type | C# Implementation |
|--------------|-------------------|
| `sync` | `AIFunctionFactory.Create()` with `FunctionInvokingChatClient` |
| `human` | `FunctionApprovalRequestContent` for HITL |
| `agent` | Handoff orchestration via `AgentWorkflowBuilder` |
| `async` | Custom implementation (background queue + callback) |

---

## Event System Mapping

### TypeScript EventEmitter
```typescript
// TS Pattern
runtime.events.on('agent.started', (event) => { ... })
runtime.events.on('tool.completed', (event) => { ... })
```

### C# Patterns
```csharp
// Option 1: IObservable<T>
public interface IAgentEventPublisher
{
    IObservable<AgentEvent> Events { get; }
}

// Option 2: Agent Middleware
var wrappedAgent = agent
    .AsBuilder()
    .Use(async (messages, session, options, innerAgent, ct) =>
    {
        // Pre-processing: emit events
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, ct))
        {
            // Post-processing: transform, filter, emit events
            yield return update;
        }
    })
    .Build();
```

---

## Async Pattern Mapping

| TypeScript | C# |
|------------|-----|
| `Promise<T>` | `Task<T>` |
| `AsyncIterable<T>` | `IAsyncEnumerable<T>` |
| `async/await` | `async/await` (identical) |
| `AbortSignal` | `CancellationToken` |
| `Promise.allSettled()` | `Task.WhenAll()` + try/catch |

---

## Key Differences

### TypeScript-Specific Features
- **Union types**: Use C# `OneOf<T1, T2>` or inheritance hierarchies
- **Type guards**: Use C# pattern matching (`is` operator)
- **Interface merging**: Not directly supported in C# - use partial classes or composition

### .NET Advantages
- **Strong typing**: Compile-time safety for all type conversions
- **DI built-in**: `IServiceCollection` and `IServiceProvider`
- **Async patterns**: First-class `async/await` with `CancellationToken` propagation
- **Middleware pipelines**: `DelegatingChatClient` and `ChatClientBuilder`

---

## Microsoft Documentation References

| Topic | URL |
|-------|-----|
| IChatClient | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.ichatclient |
| ChatClientBuilder | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder |
| AIFunctionFactory | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory |
| Agent Framework | https://learn.microsoft.com/agent-framework/overview/ |
| Handoff Orchestration | https://learn.microsoft.com/agent-framework/workflows/orchestrations/handoff |

---

## Status

- [x] Sources reviewed
- [x] Microsoft docs consulted
- [x] Mapping table completed
- [x] Code samples documented
- [x] Key differences documented
