# Microsoft .NET Implementation Guide for 01_05_agent Architecture

> **Purpose**: This document provides a comprehensive mapping of the TypeScript agent runtime architecture to Microsoft .NET frameworks, identifying relevant documentation, code samples, and extension points ("seams") for implementation.

**Date**: 2026-03-17
**Target Stack**: .NET 10 / C# 14, Microsoft.Extensions.AI, Microsoft Agent Framework, .NET Aspire

---

## Executive Summary

The TypeScript `01_05_agent` architecture can be implemented using Microsoft's AI stack with the following mapping:

| TypeScript Component | Microsoft .NET Equivalent | Status |
|---------------------|---------------------------|--------|
| `Provider` interface | `IChatClient` | ✅ Direct |
| `ProviderRegistry` | DI + `IChatClient` factory | ✅ Direct |
| Tool Registry | `AIFunctionFactory` + custom | ⚠️ Extend |
| sync tools | `AIFunctionFactory.Create()` | ✅ Direct |
| human tools | `FunctionApprovalRequestContent` | ✅ Direct |
| agent tools | Handoff orchestration | ✅ Direct |
| async tools | Custom implementation | ⚠️ Custom |
| Runner (agent loop) | Agent Framework `AIAgent` | ✅ Direct |
| Event system | Agent middleware / IObservable | ✅ Direct |
| Non-blocking execution | Checkpointing | ✅ Direct |
| MCP integration | `ModelContextProtocol.Client` | ⚠️ Custom |
| Session/Conversation | `AgentSession` + `ChatHistoryProvider` | ✅ Direct |
| Rate limiting | `DelegatingChatClient` + `RateLimiter` | ✅ Direct |
| Retry logic | Polly / `Microsoft.Extensions.Resilience` | ✅ Direct |
| OpenTelemetry | `OpenTelemetryChatClient` | ✅ Direct |
| Repository pattern | EF Core + interfaces | ✅ Direct |

---

## 1. IChatClient Provider Abstraction

### Core Interface

The `IChatClient` interface in `Microsoft.Extensions.AI` provides the unified provider abstraction equivalent to the TypeScript `Provider` interface.

**Documentation:**
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.ichatclient
- https://learn.microsoft.com/dotnet/ai/ichatclient
- https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai

**Key Interface Members:**
```csharp
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

### TypeScript → .NET Type Mapping

| TypeScript | .NET |
|------------|------|
| `ProviderRequest` | `IEnumerable<ChatMessage>` + `ChatOptions` |
| `ProviderResponse` | `ChatResponse` |
| `ProviderStreamEvent` | `ChatResponseUpdate` |
| `ProviderInputItem` | `ChatMessage` with `AIContent` variants |
| `ProviderOutputItem` | `AIContent` (TextContent, FunctionCallContent, etc.) |
| `AbortSignal` | `CancellationToken` |

### Provider Implementations

**Azure OpenAI:**
```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

IChatClient client = new AzureOpenAIClient(
    new Uri(endpoint),
    new DefaultAzureCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

**OpenAI:**
```csharp
using OpenAI;
using Microsoft.Extensions.AI;

IChatClient client = new OpenAIClient(apiKey)
    .GetChatClient("gpt-4o")
    .AsIChatClient();
```

**Google Gemini (via custom implementation):**
- Implement `IChatClient` directly or use community packages

**Docs:**
- https://learn.microsoft.com/dotnet/ai/quickstarts/prompt-model
- https://learn.microsoft.com/dotnet/ai/quickstarts/build-chat-app

---

## 2. ChatClientBuilder Middleware Pipeline

### Overview

The `ChatClientBuilder` enables a middleware pipeline pattern equivalent to the TypeScript provider wrapper pattern.

**Documentation:**
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder
- https://learn.microsoft.com/dotnet/ai/ichatclient#functionality-pipelines

### Built-in Middleware

```csharp
IChatClient client = new ChatClientBuilder(baseClient)
    .UseDistributedCache()           // Response caching
    .UseFunctionInvocation()          // Auto tool calling
    .UseOpenTelemetry()               // Tracing/metrics
    .Build();
```

### Custom Middleware via DelegatingChatClient

```csharp
using Microsoft.Extensions.AI;
using System.Threading.RateLimiting;

public sealed class RateLimitingChatClient(
    IChatClient innerClient, RateLimiter rateLimiter)
    : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var lease = await rateLimiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
            throw new InvalidOperationException("Rate limit exceeded");

        return await base.GetResponseAsync(messages, options, cancellationToken);
    }
}
```

**Docs:** https://learn.microsoft.com/dotnet/ai/ichatclient#custom-ichatclient-middleware

### Inline Middleware Pattern

```csharp
client = ChatClientBuilderChatClientExtensions
    .AsBuilder(client)
    .Use(async (messages, options, nextAsync, cancellationToken) =>
    {
        // Pre-processing
        Console.WriteLine($"Input: {messages.Count()}");

        await nextAsync(messages, options, cancellationToken);

        // Post-processing
    })
    .UseOpenTelemetry()
    .Build();
```

**Code Samples:**
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder.use

### Extension Method Pattern

```csharp
public static class RateLimitingChatClientExtensions
{
    public static ChatClientBuilder UseRateLimiting(
        this ChatClientBuilder builder,
        RateLimiter rateLimiter) =>
        builder.Use(innerClient =>
            new RateLimitingChatClient(innerClient, rateLimiter));
}
```

---

## 3. Tool System

### AIFunctionFactory

**Documentation:**
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory

```csharp
using Microsoft.Extensions.AI;

// Create tools from methods
AIFunction getWeather = AIFunctionFactory.Create(GetWeather);
AIFunction calculator = AIFunctionFactory.Create(Calculate);

// Add to chat options
var options = new ChatOptions
{
    Tools = { getWeather, calculator }
};
```

### FunctionInvokingChatClient

Auto-executes tool calls from the model:

```csharp
IChatClient client = new ChatClientBuilder(baseClient)
    .UseFunctionInvocation()  // Auto-invokes tools
    .Build();
```

**Docs:** https://learn.microsoft.com/dotnet/ai/quickstarts/use-function-calling

### Custom Tool Types

#### Sync Tools (Built-in)
```csharp
[AIFunction]
public string Calculate(string expression) =>
    // Evaluate and return result
```

#### Human Tools (HITL)
Use `FunctionApprovalRequestContent` for human-in-the-loop:

```csharp
// Tool requiring approval
if (content is FunctionApprovalRequestContent approvalRequest)
{
    DisplayApprovalRequest(approvalRequest);
    bool approved = GetUserApproval();
    var response = approvalRequest.CreateResponse(approved);
    approvalResponses.Add(response);
}
```

**Docs:**
- https://learn.microsoft.com/agent-framework/integrations/ag-ui/human-in-the-loop

#### Agent Tools (Hierarchical)
Use Handoff orchestration (see Section 6)

#### Async Tools (Custom)
Implement custom middleware that queues work and returns immediately.

---

## 4. Microsoft Agent Framework

### Overview

**Documentation:**
- https://learn.microsoft.com/agent-framework/overview/
- https://learn.microsoft.com/agent-framework/get-started/your-first-agent

### AIAgent Abstraction

```csharp
using Microsoft.Agents.AI;

AIAgent agent = new AzureOpenAIClient(endpoint, credential)
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "You are a helpful assistant.",
        name: "MyAgent");
```

### ChatClientAgent

For simple cases with custom `IChatClient`:

```csharp
using Microsoft.Agents.AI;

var agent = new ChatClientAgent(
    chatClient,
    instructions: "You are a helpful assistant");
```

**Docs:** https://learn.microsoft.com/agent-framework/agents/

### AgentSession

Manages conversation state:

```csharp
AgentSession session = await agent.CreateSessionAsync();

// Multi-turn conversation
await agent.RunAsync("My name is Alice.", session);
await agent.RunAsync("What is my name?", session);  // Remembers context

// Persist and restore
var serialized = agent.SerializeSession(session);
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

**Docs:** https://learn.microsoft.com/agent-framework/agents/conversations/session

---

## 5. Agent Middleware & Events

### Agent Middleware Pattern

```csharp
var agent = new AzureOpenAIClient(endpoint, credential)
    .GetChatClient(deploymentName)
    .AsAIAgent(instructions: "...");

var wrappedAgent = agent
    .AsBuilder()
    .Use(runFunc: null, runStreamingFunc: CustomAgentMiddleware)
    .Build();

static async IAsyncEnumerable<AgentResponseUpdate> CustomAgentMiddleware(
    IEnumerable<ChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    [EnumeratorCancellation] CancellationToken cancellationToken)
{
    // Pre-processing: emit events, logging

    await foreach (var update in innerAgent.RunStreamingAsync(
        messages, session, options, cancellationToken))
    {
        // Post-processing: transform, filter, emit events
        yield return update;
    }
}
```

**Docs:** https://learn.microsoft.com/agent-framework/agents/middleware/defining-middleware

---

## 6. Hierarchical Agents (Handoff)

### Handoff Orchestration

**Documentation:**
- https://learn.microsoft.com/agent-framework/workflows/orchestrations/handoff

```csharp
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

// Create specialized agents
ChatClientAgent triageAgent = new(client,
    "Route questions to specialists...", "triage_agent");

ChatClientAgent mathTutor = new(client,
    "Answer math questions...", "math_tutor");

ChatClientAgent historyTutor = new(client,
    "Answer history questions...", "history_tutor");

// Configure handoff rules
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(triageAgent)
    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
    .WithHandoffs([mathTutor, historyTutor], triageAgent)
    .Build();
```

### Key Concepts

1. **Dynamic Routing**: Agents decide which agent handles next interaction
2. **Context Preservation**: Full conversation history maintained across handoffs
3. **Tool-based Handoff**: LLM calls handoff tool to transfer control
4. **Mesh Topology**: No central orchestrator, agents connect directly

**Equivalent to TypeScript `delegate` tool with `depth` tracking.**

---

## 7. Non-Blocking Execution (Checkpointing)

### Overview

**Documentation:**
- https://learn.microsoft.com/agent-framework/workflows/checkpoints

Agent Framework supports checkpointing for durable workflows that can pause and resume.

### Checkpointing with Handoff

```csharp
var workflow = AgentWorkflowBuilder
    .CreateHandoffBuilderWith(startAgent)
    .WithHandoffs(agentA, agentB)
    .WithCheckpointStorage(new FileCheckpointStorage("checkpoints/"))
    .Build();
```

### Capturing Checkpoints

Checkpoints are created at end of each "superstep":
- Executor state
- Pending messages
- Workflow position

```csharp
// In C# - capture via CheckpointManager
protected override async ValueTask OnCheckpointingAsync(
    IWorkflowContext context, CancellationToken cancellation)
{
    await context.WriteStateAsync(StateKey, this.messages);
}

protected override async ValueTask OnCheckpointRestoredAsync(
    IWorkflowContext context, CancellationToken cancellation)
{
    this.messages = await context.ReadStateAsync<List<string>>(StateKey);
}
```

### Resume from Checkpoint

```csharp
// Rehydrate from checkpoint
var workflow = WorkflowBuilder.FromCheckpoint(checkpointStorage, checkpointId);

// Continue execution
await foreach (var evt in workflow.Run(stream: true))
{
    // Process events
}
```

**This enables the TypeScript `waiting` state → `running` transition via external delivery.**

---

## 8. Chat History & Session Storage

### ChatHistoryProvider

**Documentation:**
- https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.chathistoryprovider
- https://learn.microsoft.com/agent-framework/agents/conversations/storage

```csharp
public abstract class ChatHistoryProvider
{
    // Override these for custom storage
    protected virtual Task<IReadOnlyList<ChatMessage>> ProvideChatHistoryAsync(
        AIAgent agent, AgentSession session, CancellationToken cancellationToken);

    protected virtual Task StoreChatHistoryAsync(
        AIAgent agent, AgentSession session,
        IReadOnlyList<ChatMessage> requestMessages,
        IReadOnlyList<ChatMessage> responseMessages,
        CancellationToken cancellationToken);
}
```

### Custom Implementation (e.g., SQLite)

```csharp
public class SqliteChatHistoryProvider : ChatHistoryProvider
{
    private readonly SqliteConnection _connection;

    protected override async Task<IReadOnlyList<ChatMessage>> ProvideChatHistoryAsync(
        AIAgent agent, AgentSession session, CancellationToken ct)
    {
        var sessionId = session.StateBag.GetValue<string>("db_key");
        // Load messages from SQLite
        return await LoadMessagesAsync(sessionId, ct);
    }

    protected override async Task StoreChatHistoryAsync(...)
    {
        // Store messages to SQLite
    }
}
```

### Registration

```csharp
AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions()
{
    ChatOptions = new() { Instructions = "..." },
    ChatHistoryProvider = new SqliteChatHistoryProvider(connection)
});
```

### Session Serialization

```csharp
// Persist session across restarts
JsonElement serialized = agent.SerializeSession(session);
AgentSession resumed = await agent.DeserializeSessionAsync(serialized);
```

---

## 9. MCP Integration

### Model Context Protocol Client

**Documentation:**
- https://learn.microsoft.com/mcp-for-beginners
- https://github.com/microsoft/mcp-for-beginners

### Connection Setup

```csharp
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

// Stdio transport
var clientTransport = new StdioClientTransport(new()
{
    Name = "FilesServer",
    Command = "/path/to/mcp-server",
    Arguments = []
});

await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport);
```

### Tool Discovery and Calling

```csharp
// List available tools
var tools = await mcpClient.ListToolsAsync();

// Call a tool
var result = await mcpClient.CallToolAsync(
    "fs_read",
    new Dictionary<string, object?> { ["path"] = "/etc/hosts" },
    cancellationToken: ct);

Console.WriteLine(result.Content.First(c => c.Type == "text").Text);
```

### Integration with IChatClient

```csharp
// Custom MCP tool registry adapting MCP tools to AIFunction
public class McpToolRegistry
{
    private readonly McpClient _mcpClient;

    public async Task<List<AIFunction>> GetToolsAsync()
    {
        var mcpTools = await _mcpClient.ListToolsAsync();
        return mcpTools.Select(t => AIFunctionFactory.Create(
            (JsonElement args) => CallMcpToolAsync(t.Name, args),
            new AIFunctionMetadata(t.Name)
            {
                Description = t.Description,
                JsonSchema = t.InputSchema
            })).ToList();
    }

    private async Task<string> CallMcpToolAsync(string name, JsonElement args)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object?>>(args);
        var result = await _mcpClient.CallToolAsync(name, dict!);
        return result.Content.First(c => c.Type == "text").Text;
    }
}
```

**NuGet Package:** `ModelContextProtocol.Client`

---

## 10. Resilience Patterns

### Rate Limiting

**Documentation:**
- https://learn.microsoft.com/aspnet/core/performance/rate-limit

```csharp
using System.Threading.RateLimiting;

// Per-user rate limiting
RateLimiter rateLimiter = new ConcurrencyLimiter(new()
{
    PermitLimit = 10,
    QueueLimit = 100
});

// In DelegatingChatClient
using var lease = await rateLimiter.AcquireAsync(1, ct);
```

### Retry with Polly

**Documentation:**
- https://learn.microsoft.com/dotnet/core/resilience/

```csharp
using Microsoft.Extensions.Resilience;
using Polly;

var retryPipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential
    })
    .Build();

// Use in middleware
var response = await retryPipeline.ExecuteAsync(
    async ct => await innerClient.GetResponseAsync(messages, options, ct),
    cancellationToken);
```

### ASP.NET Core Rate Limiting Middleware

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

---

## 11. OpenTelemetry Observability

### OpenTelemetryChatClient

**Documentation:**
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient

```csharp
using Microsoft.Extensions.AI;
using OpenTelemetry.Trace;

string sourceName = "MyAgentApp";
TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(sourceName)
    .AddConsoleExporter()
    .Build();

IChatClient client = new ChatClientBuilder(baseClient)
    .UseOpenTelemetry(
        sourceName: sourceName,
        configure: c => c.EnableSensitiveData = true)
    .Build();
```

### Agent Framework Observability

**Documentation:**
- https://learn.microsoft.com/agent-framework/agents/observability

The Agent Framework integrates with .NET's OpenTelemetry ecosystem for tracing agent operations.

---

## 12. .NET Aspire Service Hosting

### Overview

**Documentation:**
- https://learn.microsoft.com/dotnet/aspire/

### DI Registration

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Register IChatClient with pipeline
builder.Services.AddChatClient(services =>
{
    var baseClient = new AzureOpenAIClient(endpoint, credential)
        .GetChatClient(deployment)
        .AsIChatClient();

    return baseClient
        .AsBuilder()
        .UseDistributedCache()
        .UseRateLimiting()
        .UseOpenTelemetry()
        .Build(services);
});

// Register agent
builder.Services.AddSingleton<AIAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    return chatClient.AsAIAgent("You are a helpful assistant.");
});

var host = builder.Build();
```

### Background Service Pattern

```csharp
public class AgentBackgroundService : BackgroundService
{
    private readonly AIAgent _agent;
    private readonly IAgentTaskQueue _taskQueue;

    public AgentBackgroundService(AIAgent agent, IAgentTaskQueue taskQueue)
    {
        _agent = agent;
        _taskQueue = taskQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var task in _taskQueue.ReadAllAsync(stoppingToken))
        {
            var session = await _agent.CreateSessionAsync();
            await _agent.RunAsync(task.Prompt, session);
        }
    }
}
```

---

## 13. Context Compaction (Pruning)

### Built-in Compaction Strategies

**Documentation:**
- https://learn.microsoft.com/agent-framework/agents/conversations/compaction

```csharp
using Microsoft.Agents.AI;

PipelineCompactionStrategy compactionPipeline = new(
    new ToolResultCompactionStrategy(CompactionTriggers.TokensExceed(0x200)),
    new SummarizationCompactionStrategy(summarizerClient, CompactionTriggers.TokensExceed(0x500)),
    new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(4)),
    new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(0x8000)));

AIAgent agent = chatClient
    .AsBuilder()
    .UseAIContextProviders(new CompactionProvider(compactionPipeline))
    .BuildAIAgent(new ChatClientAgentOptions { ... });
```

---

## 14. Repository Pattern (EF Core)

### Interface Pattern

```csharp
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(string id);
    Task<Agent> CreateAsync(Agent agent);
    Task UpdateAsync(Agent agent);
    Task<Agent?> FindWaitingForCallAsync(string callId);
}

public class SqliteAgentRepository : IAgentRepository
{
    private readonly AgentDbContext _context;

    public SqliteAgentRepository(AgentDbContext context) => _context = context;

    public async Task<Agent?> GetByIdAsync(string id) =>
        await _context.Agents.FindAsync(id);

    // ... other implementations
}
```

### Session/Agent Entities

Map TypeScript domain models directly to EF Core entities:
- `Agent` → `Agent` entity with status enum
- `Session` → `Session` entity
- `Item` → Polymorphic `Item` entity or table-per-type

---

## 15. Complete Documentation Index

### Microsoft.Extensions.AI

| Topic | URL |
|-------|-----|
| Overview | https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai |
| IChatClient | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.ichatclient |
| ChatClientBuilder | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder |
| ChatClientBuilder.Use | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder.use |
| DelegatingChatClient | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.delegatingchatclient |
| AIFunctionFactory | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory |
| OpenTelemetryChatClient | https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient |
| Function Calling | https://learn.microsoft.com/dotnet/ai/quickstarts/use-function-calling |
| Chat App Tutorial | https://learn.microsoft.com/dotnet/ai/quickstarts/build-chat-app |

### Microsoft Agent Framework

| Topic | URL |
|-------|-----|
| Overview | https://learn.microsoft.com/agent-framework/overview/ |
| Your First Agent | https://learn.microsoft.com/agent-framework/get-started/your-first-agent |
| Multi-turn Conversations | https://learn.microsoft.com/agent-framework/get-started/multi-turn |
| Memory & Persistence | https://learn.microsoft.com/agent-framework/get-started/memory |
| Add Tools | https://learn.microsoft.com/agent-framework/get-started/add-tools |
| Agent Providers | https://learn.microsoft.com/agent-framework/agents/providers/ |
| Custom Agents | https://learn.microsoft.com/agent-framework/agents/providers/custom |
| Conversations Overview | https://learn.microsoft.com/agent-framework/agents/conversations/ |
| Session | https://learn.microsoft.com/agent-framework/agents/conversations/session |
| Storage | https://learn.microsoft.com/agent-framework/agents/conversations/storage |
| Compaction | https://learn.microsoft.com/agent-framework/agents/conversations/compaction |
| ChatHistoryProvider | https://learn.microsoft.com/dotnet/api/microsoft.agents.ai.chathistoryprovider |
| Agent Middleware | https://learn.microsoft.com/agent-framework/agents/middleware/defining-middleware |
| Observability | https://learn.microsoft.com/agent-framework/agents/observability |

### Workflows

| Topic | URL |
|-------|-----|
| Workflows Overview | https://learn.microsoft.com/agent-framework/workflows/ |
| Checkpoints | https://learn.microsoft.com/agent-framework/workflows/checkpoints |
| Handoff Orchestration | https://learn.microsoft.com/agent-framework/workflows/orchestrations/handoff |
| Sequential Orchestration | https://learn.microsoft.com/agent-framework/workflows/orchestrations/sequential |
| Concurrent Orchestration | https://learn.microsoft.com/agent-framework/workflows/orchestrations/concurrent |
| Workflows as Agents | https://learn.microsoft.com/agent-framework/workflows/as-agents |

### Migration Guides

| Topic | URL |
|-------|-----|
| From AutoGen | https://learn.microsoft.com/agent-framework/migration-guide/from-autogen |
| From Semantic Kernel | https://learn.microsoft.com/agent-framework/migration-guide/from-semantic-kernel |

### Human-in-the-Loop

| Topic | URL |
|-------|-----|
| HITL Integration | https://learn.microsoft.com/agent-framework/integrations/ag-ui/human-in-the-loop |

### Resilience & Aspire

| Topic | URL |
|-------|-----|
| .NET Resilience | https://learn.microsoft.com/dotnet/core/resilience/ |
| Rate Limiting | https://learn.microsoft.com/aspnet/core/performance/rate-limit |
| .NET Aspire | https://learn.microsoft.com/dotnet/aspire/ |
| Dependency Injection | https://learn.microsoft.com/dotnet/core/extensions/dependency-injection |

### MCP

| Topic | URL |
|-------|-----|
| MCP for Beginners | https://learn.microsoft.com/mcp-for-beginners |
| MCP GitHub | https://github.com/microsoft/mcp-for-beginners |
| MCP Spec | https://modelcontextprotocol.io/introduction |

---

## 16. Implementation Roadmap

### Phase 1: Foundation
1. Implement provider abstraction with `IChatClient`
2. Build `ChatClientBuilder` pipeline with middleware
3. Create `RateLimitingChatClient` and `OpenTelemetryChatClient`

### Phase 2: Core Agent
1. Create `AIAgent` with session management
2. Implement `ChatHistoryProvider` for SQLite
3. Add `AIFunction` tools (sync)

### Phase 3: Advanced Features
1. Implement human tools with `FunctionApprovalRequestContent`
2. Set up Handoff orchestration for agent tools
3. Add checkpointing for non-blocking execution

### Phase 4: MCP Integration
1. Create `McpToolRegistry` adapting MCP tools
2. Implement MCP client manager
3. Integrate with agent tool system

### Phase 5: Production
1. Add ASP.NET Core hosting
2. Configure rate limiting per user
3. Set up OpenTelemetry exporters
4. Implement repository pattern with EF Core

---

## 17. Key Extension Points (Seams)

| Seam | Purpose | Implementation |
|------|---------|----------------|
| `DelegatingChatClient` | All middleware | Inherit and override |
| `ChatClientBuilder.Use()` | Inline middleware | Delegate pattern |
| `AIFunctionFactory` | Tool creation | Create from methods |
| `ChatHistoryProvider` | Session persistence | Override virtuals |
| `CheckpointStorage` | Workflow durability | Implement interface |
| `AgentMiddleware` | Agent-level hooks | Use builder pattern |
| `McpClientFactory` | External tools | Custom registry |
| `IAgentRepository` | Agent persistence | EF Core implementation |

---

*Generated from comprehensive Microsoft documentation research on 2026-03-17*
