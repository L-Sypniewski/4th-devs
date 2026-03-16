# Investigation: Function Invocation Patterns

## Objective
Investigate how tool/function calls are processed when received from LLM responses, including parameter deserialization, execution, and result formatting.

---

## Source Files

### TypeScript Function Handling
- **Runner (Tool Call Handling)**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
  - Lines 237-402: `handleTurnResponse()` function
  - Lines 250-395: Tool call iteration and dispatching
- **Tool Execution**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/registry.ts
- **Provider Types**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/types.ts

---

## Microsoft Documentation

- **FunctionInvokingChatClient**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient
- **ChatClientBuilder Extensions**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder
- **Function Calling**: https://learn.microsoft.com/dotnet/ai/ai-function-calling

---

## Investigation Questions

1. **Tool Call Handling**
   - How does TS handle tool calls from LLM responses in `handleTurnResponse()`?
   - What is `FunctionInvokingChatClient` and how does it intercept tool calls?
   - How does the middleware pattern differ from TS direct handling?

2. **Parameter Deserialization**
   - How are tool parameters deserialized and validated in TS?
   - How does `FunctionInvokingChatClient` deserialize JSON arguments to .NET types?
   - What validation patterns are available (DataAnnotations, FluentValidation)?

3. **Execution Pipeline**
   - How does TS execute tools and capture results?
   - What is the .NET execution pipeline through `FunctionInvokingChatClient`?
   - How are execution errors handled and surfaced?

4. **Result Formatting**
   - How are tool results returned to the conversation in TS?
   - How does .NET format results back to the LLM?
   - What about streaming results?

---

## Code Patterns to Analyze

### TypeScript Tool Call Handling
```typescript
// From runner.ts - handleTurnResponse()
async function handleTurnResponse(
  response: ProviderResponse,
  agent: Agent,
  runtime: RuntimeContext,
  exec: ExecutionContext,
  turnNumber: number,
  signal?: AbortSignal,
): Promise<TurnResult> {
  // Store output first
  await storeProviderOutput(agent.id, response.output, runtime, turnNumber)

  // Extract function calls
  const functionCalls = response.output.filter(o => o.type === 'function_call')

  // No tool calls = agent completed
  if (functionCalls.length === 0) {
    const completeResult = completeAgent(agent, response.output)
    return { continue: false, agent: completeResult.agent, usage }
  }

  // Process each function call
  for (const fc of functionCalls) {
    const tool = runtime.tools.get(fc.name)

    // Handle different tool types
    if (tool.type === 'sync') {
      const result = await runtime.tools.execute(fc.name, fc.arguments, signal)
      // Store result...
    } else if (tool.type === 'agent') {
      // Spawn child agent...
    } else if (tool.type === 'human') {
      // Defer for human input...
    }
  }
}
```

- How does .NET `FunctionInvokingChatClient` compare?
- What is the middleware intercept pattern?

### MCP Tool Invocation
```typescript
// From runner.ts - MCP tool handling
if (runtime.mcp.parseName(fc.name)) {
  log.info({ ...toolCtx, args: argsPreview }, `${fc.name}`)
  runtime.events.emit({ type: 'tool.called', ... })

  const start = Date.now()
  try {
    const output = await runtime.mcp.callTool(fc.name, fc.arguments, signal)
    const ms = Date.now() - start
    log.info({ ...toolCtx, ms, output: truncate(output) }, `${fc.name} ok ${ms}ms`)

    await runtime.repositories.items.create(agent.id, {
      type: 'function_call_output',
      callId: fc.callId,
      output,
      isError: false,
      turnNumber,
    })
    runtime.events.emit({ type: 'tool.completed', ... })
  } catch (err) {
    runtime.events.emit({ type: 'tool.failed', ... })
  }
}
```

- How would MCP tools integrate with `FunctionInvokingChatClient`?
- What about event/telemetry emission?

### Parameter Handling
```typescript
// From registry.ts - execute method
async execute(name, args, signal): Promise<ToolResult> {
  const tool = tools.get(name)
  if (!tool) {
    return { ok: false, error: `Tool not found: ${name}` }
  }

  if (signal?.aborted) {
    return { ok: false, error: 'Operation aborted' }
  }

  try {
    // args is Record<string, unknown>, handler casts as needed
    return await tool.handler(args, signal)
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Tool execution failed'
    return { ok: false, error: message }
  }
}
```

- Compare to .NET's typed parameter binding
- How does .NET handle serialization errors?

---

## Expected Deliverables

### 1. Function Invocation Pipeline Diagram
```
LLM Response
    │
    ▼
┌─────────────────────────────────────┐
│  FunctionInvokingChatClient         │
│  (Middleware)                       │
│                                     │
│  1. Detect tool calls               │
│  2. Resolve tool by name            │
│  3. Deserialize parameters          │
│  4. Validate parameters             │
│  5. Execute handler                 │
│  6. Capture result/error            │
│  7. Format for response             │
└─────────────────────────────────────┘
    │
    ▼
Tool Result → Conversation History
```

### 2. C# Example
```csharp
// Example of FunctionInvokingChatClient usage
var builder = new ChatClientBuilder(chatClient)
    .UseFunctionInvoking()
    .UseOpenTelemetry();

// Tool definition and registration
[AIFunction]
[Description("Perform basic math operations")]
public static string Calculator(
    [Description("The math operation")] string operation,
    [Description("First operand")] double a,
    [Description("Second operand")] double b)
{
    return operation switch
    {
        "add" => (a + b).ToString(),
        "subtract" => (a - b).ToString(),
        "multiply" => (a * b).ToString(),
        "divide" => (a / b).ToString(),
        _ => throw new ArgumentException($"Unknown operation: {operation}")
    };
}

// Register with client
var tools = AIFunctionFactory.CreateTools(typeof(MyTools));
await client.CompleteAsync(messages, new ChatOptions { Tools = tools });
```

### 3. Comparison Table

| Feature | TypeScript | .NET | Notes |
|---------|------------|------|-------|
| Detection | Manual filter | Automatic via middleware | |
| Dispatch | Type switch | Delegate invocation | |
| Parameters | Record<string, unknown> | Strongly typed binding | |
| Validation | Zod / manual | DataAnnotations / FluentValidation | |
| Errors | Try/catch, isError flag | Exception propagation | |
| Cancellation | AbortSignal | CancellationToken | |
| Events | Custom event emitter | OpenTelemetry / ILogger | |
| Storage | Repository pattern | Custom or built-in | |

---

## Additional Research Areas

- [ ] Investigate `FunctionInvokingChatClient` source code
- [ ] Compare synchronous vs asynchronous tool execution
- [ ] Document error handling patterns
- [ ] Research automatic retry mechanisms
- [ ] Look into tool call parallelization
- [ ] Investigate streaming tool results

---

## Status

- [ ] TypeScript sources reviewed
- [ ] Microsoft docs consulted
- [ ] Pipeline diagram created
- [ ] C# example documented
- [ ] Comparison table completed
