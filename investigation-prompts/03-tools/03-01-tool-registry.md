# Investigation: Tool/Function Registration Patterns

## Objective
Investigate how tools/functions are defined, registered, and exposed to LLMs in both TypeScript and .NET Microsoft frameworks.

---

## Source Files

### TypeScript Tool System
- **Tool Types**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/types.ts
- **Tool Registry**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/registry.ts
- **Tool Definitions**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/definitions/
  - `calculator.ts` - Simple sync tool example
  - `delegate.ts` - Agent delegation tool
  - `ask-user.ts` - Human-in-the-loop tool
  - `send-message.ts` - Cross-agent messaging tool

### MCP Tool Pattern
- **FS Read Tool**: https://github.com/i-am-alice/4th-devs/blob/main/mcp/files-mcp/src/tools/fs-read.tool.ts
- **MCP Tool Registry**: https://github.com/i-am-alice/4th-devs/blob/main/mcp/uploadthing-mcp/src/shared/tools/registry.ts

---

## Microsoft Documentation

- **Function Tools Overview**: https://learn.microsoft.com/agent-framework/agents/tools/function-tools
- **AIFunctionFactory**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory
- **ChatClient Tools**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chattool

---

## Investigation Questions

1. **Tool Definition Patterns**
   - How are tools defined and registered in TS (using `Tool` interface)?
   - What is `AIFunctionFactory` in .NET and how does it create tools from C# methods?
   - How do JSON schemas get generated from TypeScript vs C# type definitions?

2. **Schema Generation**
   - How does TS generate JSON Schema for tool parameters (manual vs library)?
   - What is the .NET equivalent for schema generation (reflection, attributes, source generators)?
   - How do Zod schemas in MCP tools compare to .NET validation approaches?

3. **Tool Registration**
   - How are tools registered with the agent in TS (`ToolRegistry.register()`)?
   - How does .NET register tools with `ChatClient` or `Agent`?
   - What is the lifecycle of registered tools?

4. **Tool Types**
   - How does TS differentiate between `sync`, `async`, `agent`, and `human` tool types?
   - Does .NET have equivalent tool type categorization?
   - How do built-in tools differ from MCP-provided tools?

---

## Code Patterns to Analyze

### TypeScript Tool Definition
```typescript
// From calculator.ts
export const calculatorTool: Tool = {
  type: 'sync',
  definition: {
    type: 'function',
    name: 'calculator',
    description: 'Perform basic math operations',
    parameters: {
      type: 'object',
      properties: {
        operation: {
          type: 'string',
          enum: ['add', 'subtract', 'multiply', 'divide'],
          description: 'The math operation to perform',
        },
        a: { type: 'number', description: 'First operand' },
        b: { type: 'number', description: 'Second operand' },
      },
      required: ['operation', 'a', 'b'],
    },
  },
  handler: async (args) => {
    const { operation, a, b } = args as CalculatorArgs
    const result = calculate({ operation, a, b })
    return { ok: true, output: String(result) }
  },
}
```

- Compare to .NET `AIFunction` attribute-based definition
- How does handler invocation differ?

### Tool Registry Pattern
```typescript
// From registry.ts
export function createToolRegistry(): ToolRegistry {
  const tools = new Map<string, Tool>()

  return {
    register(tool) {
      tools.set(tool.definition.name, tool)
    },
    get(name) {
      return tools.get(name)
    },
    list(): FunctionTool[] {
      return Array.from(tools.values()).map(t => t.definition)
    },
    async execute(name, args, signal): Promise<ToolResult> {
      const tool = tools.get(name)
      if (!tool) {
        return { ok: false, error: `Tool not found: ${name}` }
      }
      return await tool.handler(args, signal)
    },
  }
}
```

- What is the .NET equivalent for tool registration?
- How does DI container integration work?

### MCP Tool with Zod Schema
```typescript
// From fs-read.tool.ts
export const fsReadInputSchema = z.object({
  path: z.string().min(1).describe('Relative path to file or directory'),
  mode: z.enum(['auto', 'tree', 'list', 'content']).optional().default('auto'),
  limit: z.number().int().min(1).max(2000).optional().default(100),
  // ...
})

export const fsReadTool = {
  name: 'fs_read',
  description: 'Read files and list directories...',
  inputSchema: fsReadInputSchema,
  handler: async (args: unknown, _extra: HandlerExtra): Promise<CallToolResult> => {
    const parsed = fsReadInputSchema.safeParse(args)
    if (!parsed.success) {
      return { isError: true, content: [...] }
    }
    // ...
  },
}
```

- How does Zod schema generation compare to .NET schema generation?
- What is the .NET equivalent for runtime validation?

---

## Expected Deliverables

### 1. Tool Registration Pattern in C#
```csharp
// Proposed C# equivalent
public interface IToolRegistry
{
    void Register<T>(string name, Func<T, CancellationToken, Task<ToolResult>> handler);
    ToolDefinition? Get(string name);
    IReadOnlyList<ToolDefinition> List();
    Task<ToolResult> ExecuteAsync(string name, JsonElement args, CancellationToken cancellationToken);
}
```

### 2. Schema Generation Example
Document how to generate JSON Schema from:
- C# record types with attributes
- C# methods with `[Description]` attributes
- Compare with TypeScript manual schema definition

### 3. Registration Comparison Table

| Feature | TypeScript | .NET | Notes |
|---------|------------|------|-------|
| Definition | Manual JSON Schema | AIFunctionFactory / Attributes | |
| Registry | Map-based custom | Service collection / DI | |
| Validation | Runtime type check | Schema validation | |
| Handler | Async function | Delegate / MethodInfo | |
| Lifecycle | Per-agent | Scoped / Singleton | |

---

## Additional Research Areas

- [ ] Investigate `AIFunctionFactory.CreateFromMethod()` patterns
- [ ] Compare attribute-based vs fluent API tool definitions
- [ ] Document JSON Schema generation options in .NET
- [ ] Research System.Text.Json schema generator
- [ ] Look into nullable reference types impact on schema generation

---

## Status

- [ ] TypeScript sources reviewed
- [ ] Microsoft docs consulted
- [ ] C# patterns documented
- [ ] Schema generation example created
- [ ] Registration comparison completed
