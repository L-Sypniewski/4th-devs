# Investigation: Different Tool Types

## Objective
Investigate the different categories of tools (local functions, MCP tools, agent tools, human tools) and how they can be implemented and unified in .NET.

---

## Source Files

### TypeScript Tool Types
- **Tool Type Definition**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/types.ts
  - `ToolType = 'sync' | 'async' | 'agent' | 'human'`
- **Tool Definitions**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/definitions/
  - `calculator.ts` - Sync tool
  - `delegate.ts` - Agent tool (spawns child agents)
  - `ask-user.ts` - Human tool (requires external input)
  - `send-message.ts` - Sync tool with cross-agent effects

### MCP Tool Integration
- **MCP Client**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/client.ts
- **MCP Tool Examples**: https://github.com/i-am-alice/4th-devs/blob/main/mcp/files-mcp/src/tools/

### Runner Tool Dispatch
- **Tool Dispatch Logic**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
  - Lines 250-395: Type-based tool dispatch

---

## Microsoft Documentation

- **Function Tools**: https://learn.microsoft.com/agent-framework/agents/tools/function-tools
- **AIFunctionFactory**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory
- **MCP for .NET**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai (search for MCP integration)

---

## Investigation Questions

1. **Tool Type Categories**
   - What types of tools exist in TS (`sync`, `async`, `agent`, `human`)?
   - How do MCP tools differ from local function tools?
   - Does .NET have equivalent categorization or is it unified?

2. **MCP Tool Integration**
   - How are MCP tools discovered and registered in TS?
   - What is the .NET equivalent for MCP tool integration?
   - How do MCP tools differ in naming (`server__toolName` pattern)?

3. **Custom Tool Types**
   - How to create custom tool types in .NET?
   - What abstractions exist for extending tool behavior?
   - How do permissions and capabilities map between TS and .NET?

4. **Tool Permissions**
   - How are tool permissions handled in TS (deferred vs immediate)?
   - What is the .NET approach to tool authorization?
   - How can tools be sandboxed or restricted?

---

## Code Patterns to Analyze

### TypeScript Tool Types
```typescript
// From types.ts
export type ToolType = 'sync' | 'async' | 'agent' | 'human'

export interface Tool {
  type: ToolType
  definition: FunctionTool
  handler: ToolHandler
}

export interface ToolRegistry {
  register(tool: Tool): void
  get(name: string): Tool | undefined
  list(): FunctionTool[]
  execute(name: string, args: Record<string, unknown>, signal?: AbortSignal): Promise<ToolResult>
}
```

### Tool Type Dispatch in Runner
```typescript
// From runner.ts - handleTurnResponse()
for (const fc of functionCalls) {
  const tool = runtime.tools.get(fc.name)

  // MCP Tool (external server)
  if (runtime.mcp.parseName(fc.name)) {
    const output = await runtime.mcp.callTool(fc.name, fc.arguments, signal)
    // Store result, emit events...
    hasSyncTools = true
    continue
  }

  // Unknown tool - defer for external handling
  if (!tool) {
    waitingFor.push({ callId: fc.callId, type: 'tool', name: fc.name })
    continue
  }

  // Agent Tool - spawn child agent
  if (tool.type === 'agent') {
    const delegateResult = await handleDelegation(fc.callId, fc.arguments, ...)
    if (delegateResult.type === 'completed') {
      hasSyncTools = true
    } else if (delegateResult.type === 'waiting') {
      waitingFor.push(delegateResult.wait)
    }
    continue
  }

  // Human Tool - defer for human input
  if (tool.type === 'human') {
    waitingFor.push({
      callId: fc.callId,
      type: 'human',
      name: fc.name,
      description: fc.arguments.question ?? tool.definition.description,
    })
    continue
  }

  // Sync Tool - execute immediately
  if (tool.type === 'sync') {
    const result = await runtime.tools.execute(fc.name, fc.arguments, signal)
    // Store result, emit events...
    hasSyncTools = true
    continue
  }

  // Async / unknown - defer
  waitingFor.push({ callId: fc.callId, type: 'tool', name: fc.name })
}
```

### Agent Tool (Delegation)
```typescript
// From runner.ts - handleDelegation()
async function handleDelegation(
  callId: CallId,
  args: Record<string, unknown>,
  parent: Agent,
  runtime: RuntimeContext,
  exec: ExecutionContext,
  turnNumber: number,
  signal?: AbortSignal,
): Promise<DelegationResult> {
  const { agent: agentName, task } = args as unknown as DelegateArgs

  // Resolve child agent template
  const template = await getAgent(agentName)

  // Create child agent
  const child = await runtime.repositories.agents.create({
    sessionId: parent.sessionId,
    parentId: parent.id,
    sourceCallId: callId,
    depth: exec.depth + 1,
    task: template.config.systemPrompt,
    config: { model: template.config.model, tools: template.config.tools },
  })

  // Run child agent
  const childResult = await runAgent(child.id, runtime, { maxTurns: 10, signal, execution: childExec })

  // Propagate result to parent
  if (childResult.status === 'completed') {
    const output = extractAgentResult(childResult.items)
    // Store output on parent...
    return { type: 'completed' }
  }
}
```

### Human Tool Definition
```typescript
// From ask-user.ts
export const askUserTool: Tool = {
  type: 'human',
  definition: {
    type: 'function',
    name: 'ask_user',
    description: 'Ask the user a question and wait for their response',
    parameters: {
      type: 'object',
      properties: {
        question: { type: 'string', description: 'The question to ask' },
      },
      required: ['question'],
    },
  },
  handler: async (args) => {
    // This handler is never called - tool is deferred
    return { ok: true, output: 'Deferred for human input' }
  },
}
```

---

## Expected Deliverables

### 1. Tool Type Hierarchy Diagram
```
                    ┌─────────────────┐
                    │   ITool (base)  │
                    └────────┬────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
         ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│  SyncTool       │ │  DeferredTool   │ │  MCPTool        │
│  (immediate)    │ │  (wait state)   │ │  (external)     │
└─────────────────┘ └────────┬────────┘ └─────────────────┘
                             │
                    ┌────────┴────────┐
                    │                 │
                    ▼                 ▼
            ┌─────────────┐   ┌─────────────┐
            │ AgentTool   │   │ HumanTool   │
            │ (sub-agent) │   │ (user input)│
            └─────────────┘   └─────────────┘
```

### 2. Custom Tool Implementation in C#
```csharp
// Base tool interface
public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement ParametersSchema { get; }
    ToolExecutionType ExecutionType { get; }
}

public enum ToolExecutionType
{
    Sync,        // Execute immediately
    Deferred,    // Requires external resolution
    Agent,       // Spawns sub-agent
    Human,       // Requires human input
    MCP          // External MCP server
}

// Sync tool implementation
public class SyncTool<TArgs, TResult> : ITool
{
    public string Name { get; init; }
    public string Description { get; init; }
    public JsonElement ParametersSchema { get; init; }
    public ToolExecutionType ExecutionType => ToolExecutionType.Sync;

    public Func<TArgs, CancellationToken, Task<TResult>> Handler { get; init; }
}

// Deferred tool implementation
public abstract class DeferredTool : ITool
{
    public ToolExecutionType ExecutionType => ToolExecutionType.Deferred;
    public abstract Task<WaitingFor> CreateWaitingRequest(string callId, JsonElement args);
    public abstract Task<string> ProcessResult(JsonElement deliveredResult);
}

// Agent tool implementation
public class AgentTool : DeferredTool
{
    public string TargetAgentName { get; init; }

    public override async Task<WaitingFor> CreateWaitingRequest(string callId, JsonElement args)
    {
        // Spawn child agent and return waiting state
        return new WaitingFor
        {
            CallId = callId,
            Type = ToolExecutionType.Agent,
            Name = $"delegate:{TargetAgentName}",
            Description = $"Waiting for agent '{TargetAgentName}' to complete"
        };
    }
}

// MCP tool wrapper
public class MCPTool : ITool
{
    public string Name { get; init; }  // Format: "server__toolName"
    public string Description { get; init; }
    public JsonElement ParametersSchema { get; init; }
    public ToolExecutionType ExecutionType => ToolExecutionType.MCP;

    public IMCPClient Client { get; init; }

    public async Task<string> ExecuteAsync(JsonElement args, CancellationToken cancellationToken)
    {
        return await Client.CallToolAsync(Name, args, cancellationToken);
    }
}
```

### 3. MCP Tool Wrapper Pattern
```csharp
// MCP tool adapter for .NET
public class MCPToolAdapter
{
    private readonly IMCPClient _client;
    private readonly Dictionary<string, MCPTool> _tools = new();

    public async Task DiscoverToolsAsync()
    {
        var tools = await _client.ListToolsAsync();
        foreach (var tool in tools)
        {
            // Parse "server__toolName" format
            var parts = tool.Name.Split("__");
            if (parts.Length == 2)
            {
                _tools[tool.Name] = new MCPTool
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    ParametersSchema = tool.InputSchema,
                    Client = _client
                };
            }
        }
    }

    public MCPTool? Get(string name) => _tools.TryGetValue(name, out var tool) ? tool : null;
    public bool IsMCPTool(string name) => _tools.ContainsKey(name);
}
```

### 4. Comparison Table

| Feature | TypeScript | .NET | Notes |
|---------|------------|------|-------|
| Type definition | Union type literal | Enum + interfaces | |
| Sync tools | `type: 'sync'` | `ToolExecutionType.Sync` | |
| Deferred tools | `waitingFor.push()` | `DeferredTool` base class | |
| Agent tools | `handleDelegation()` | `AgentTool` implementation | |
| Human tools | `type: 'human'` | `ToolExecutionType.Human` | |
| MCP tools | `runtime.mcp.callTool()` | `MCPTool` wrapper | |
| Permissions | Tool-level | Not built-in | Custom implementation needed |
| Discovery | Manual registration | Reflection + DI | |

---

## Additional Research Areas

- [ ] Research .NET MCP SDK availability
- [ ] Investigate `Agent` class in Microsoft Agent Framework for delegation
- [ ] Document permission/authorization patterns
- [ ] Research tool sandboxing approaches
- [ ] Look into dynamic tool loading patterns
- [ ] Investigate tool versioning strategies

---

## Status

- [ ] TypeScript sources reviewed
- [ ] Microsoft docs consulted
- [ ] Type hierarchy diagram created
- [ ] C# implementations documented
- [ ] MCP wrapper pattern documented
- [ ] Comparison table completed
