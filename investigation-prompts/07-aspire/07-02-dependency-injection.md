# Investigation: Dependency Injection

**Status**: Pending
**Priority**: High
**Category**: Aspire

---

## Source Files

### TypeScript Reference
- [01_05_agent/src/runtime/context.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/context.ts) (48 lines - RuntimeContext DI pattern)

### Key TypeScript Patterns to Investigate
```typescript
/**
 * Runtime context — dependencies for agent execution
 */
import type { AgentEventEmitter } from '../events/index.js'
import type { Repositories } from '../repositories/index.js'
import type { ToolRegistry } from '../tools/index.js'
import type { McpManager } from '../mcp/index.js'
import type { TraceId, AgentId } from '../domain/index.js'

export interface RuntimeContext {
  events: AgentEventEmitter
  repositories: Repositories
  tools: ToolRegistry
  mcp: McpManager
}

// Per-request execution context (passed through agent hierarchy)
export interface ExecutionContext {
  traceId: TraceId
  rootAgentId: AgentId
  parentAgentId?: AgentId
  depth: number
  /** Propagated to events for trace-level attribution */
  userId?: string
  /** Propagated to events for trace-level input */
  userInput?: unknown
  /** Human-readable agent name (e.g. "alice") */
  agentName?: string
}

export function createContext(
  events: AgentEventEmitter,
  repositories: Repositories,
  tools: ToolRegistry,
  mcp: McpManager,
): RuntimeContext {
  return { events, repositories, tools, mcp }
}

export function createExecutionContext(
  traceId: TraceId,
  rootAgentId: AgentId,
  parentAgentId?: AgentId,
  depth: number = 0,
): ExecutionContext {
  return { traceId, rootAgentId, parentAgentId, depth }
}
```

---

## Microsoft Documentation

### Primary References
- [Dependency Injection in .NET](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
- [Service Lifetimes](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#service-lifetimes)
- [Factory-based Middleware](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/write#factory-based-middleware-activation)

### Related Types
- `IServiceCollection` - Service registration
- `IServiceProvider` - Service resolution
- `IServiceScope` - Scoped service lifetime
- `IHostApplicationBuilder` - Aspire host builder

---

## Investigation Questions

### 1. TypeScript RuntimeContext Pattern
- How does TS RuntimeContext manage dependencies?
- What is the pattern for passing context through agent hierarchy?
- How are ExecutionContext and RuntimeContext related?

### 2. .NET DI Container Registration
- How to register services in .NET DI container?
- What is the best pattern for registering agent services?
- How to use extension methods for service registration?

### 3. Service Lifetimes
- How to implement scoped vs singleton services?
- When should agent services be scoped vs singleton?
- How to handle per-request context in singleton services?

### 4. Service Injection Patterns
- How to inject IChatClient and other services?
- How to inject IServiceProvider for late resolution?
- How to implement factory patterns for complex services?

---

## Code Patterns

### Service Registration Extensions (Expected .NET)
```csharp
// Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentRuntime(
        this IServiceCollection services)
    {
        // Core services
        services.AddSingleton<IAgentEventEmitter, AgentEventEmitter>();
        services.AddScoped<IRepositories, Repositories>();
        services.AddSingleton<IToolRegistry, ToolRegistry>();
        services.AddSingleton<IMcpManager, McpManager>();

        // Runtime context
        services.AddScoped<IRuntimeContext, RuntimeContext>();

        return services;
    }

    public static IServiceCollection AddAgentServices(
        this IServiceCollection services)
    {
        services.AddChatClient();
        services.AddAgentRuntime();
        services.AddConversationServices();

        return services;
    }
}
```

### RuntimeContext Implementation (Expected .NET)
```csharp
// Runtime/RuntimeContext.cs
public interface IRuntimeContext
{
    IAgentEventEmitter Events { get; }
    IRepositories Repositories { get; }
    IToolRegistry Tools { get; }
    IMcpManager Mcp { get; }
}

public class RuntimeContext : IRuntimeContext
{
    public IAgentEventEmitter Events { get; }
    public IRepositories Repositories { get; }
    public IToolRegistry Tools { get; }
    public IMcpManager Mcp { get; }

    public RuntimeContext(
        IAgentEventEmitter events,
        IRepositories repositories,
        IToolRegistry tools,
        IMcpManager mcp)
    {
        Events = events;
        Repositories = repositories;
        Tools = tools;
        Mcp = mcp;
    }
}
```

### ExecutionContext Implementation (Expected .NET)
```csharp
// Runtime/ExecutionContext.cs
public interface IExecutionContext
{
    string TraceId { get; }
    string RootAgentId { get; }
    string? ParentAgentId { get; }
    int Depth { get; }
    string? UserId { get; }
    string? AgentName { get; }
}

public class ExecutionContext : IExecutionContext
{
    public string TraceId { get; init; }
    public string RootAgentId { get; init; }
    public string? ParentAgentId { get; init; }
    public int Depth { get; init; }
    public string? UserId { get; init; }
    public string? AgentName { get; init; }

    public static IExecutionContext Create(
        string traceId,
        string rootAgentId,
        string? parentAgentId = null,
        int depth = 0)
    {
        return new ExecutionContext
        {
            TraceId = traceId,
            RootAgentId = rootAgentId,
            ParentAgentId = parentAgentId,
            Depth = depth
        };
    }
}
```

### Scoped Service Pattern (Expected .NET)
```csharp
// Using scoped services in singleton
public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IServiceProvider _serviceProvider;

    public AgentOrchestrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<AgentResult> ExecuteAsync(AgentRequest request)
    {
        // Create scope for per-request services
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IRuntimeContext>();
        var chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();

        // Execute within scope
        return await ExecuteWithContextAsync(request, context, chatClient);
    }
}
```

### Factory Pattern for Services (Expected .NET)
```csharp
// Factory-based service registration
services.AddScoped<IAgentService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["AI:Provider"];

    return provider switch
    {
        "openai" => new OpenAIAgentService(sp.GetRequiredService<OpenAIClient>()),
        "anthropic" => new AnthropicAgentService(sp.GetRequiredService<AnthropicClient>()),
        _ => throw new InvalidOperationException($"Unknown provider: {provider}")
    };
});
```

---

## Service Lifetime Guide

| Lifetime | Use Case | Example |
|----------|----------|---------|
| Singleton | Stateless, thread-safe services | ToolRegistry, McpManager |
| Scoped | Per-request context | RuntimeContext, ExecutionContext |
| Transient | Lightweight, short-lived | Factories, Builders |

---

## Expected Deliverables

1. **Service Registration Extensions**
   - `AddAgentRuntime()` extension method
   - `AddAgentServices()` extension method
   - Aspire-compatible registration

2. **Context Interfaces and Implementations**
   - `IRuntimeContext` and implementation
   - `IExecutionContext` and implementation
   - Context factory methods

3. **DI Configuration for Agent Services**
   - ChatClient registration patterns
   - Repository registration
   - Tool registry setup
   - MCP manager configuration

---

## Notes

- Follow .NET conventions for DI registration (Add* extension methods)
- Use scoped services for per-request data to avoid thread-safety issues
- Consider using `KeyedService` for multiple implementations of same interface
- Integrate with Aspire's service discovery for cross-service DI
