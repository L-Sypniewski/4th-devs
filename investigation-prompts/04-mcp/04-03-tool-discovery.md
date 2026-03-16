# Investigation: Dynamic Tool Discovery from MCP Servers

## Objective
Investigate how tools are dynamically discovered from MCP servers, how schemas are converted to LLM-compatible formats, and how to implement efficient caching and change detection.

---

## Source Files

### TypeScript Tool Discovery
- **MCP Client (Tool Discovery)**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/client.ts
- **MCP Types**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/types.ts
- **Tool Registry Integration**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/registry.ts

### MCP Server Tool Examples
- **FS Read Tool**: https://github.com/i-am-alice/4th-devs/blob/main/mcp/files-mcp/src/tools/fs-read.tool.ts
- **MCP Tool Registry**: https://github.com/i-am-alice/4th-devs/blob/main/mcp/uploadthing-mcp/src/shared/tools/registry.ts

---

## Microsoft Documentation

- **Function Tools**: https://learn.microsoft.com/agent-framework/agents/tools/function-tools
- **AIFunctionFactory**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.aifunctionfactory
- **ChatTool Schema**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chattool
- **JSON Schema Generation**: https://learn.microsoft.com/dotnet/api/system.text.json.schema

---

## Investigation Questions

1. **Tool Discovery Mechanism**
   - How does TS discover tools using `client.listTools()`?
   - What information is returned (name, description, inputSchema)?
   - How often should tools be re-discovered?

2. **Schema Conversion**
   - How are MCP tool schemas converted to LLM-compatible `FunctionTool` format?
   - What is the structure of `inputSchema` in MCP vs JSON Schema in LLM APIs?
   - How to handle complex schemas (nested objects, arrays, enums)?

3. **Tool Name Namespacing**
   - Why use the `server__tool` prefix pattern?
   - How does this prevent naming conflicts between servers?
   - What is the parse/mapping logic for prefixed names?

4. **Caching Strategies**
   - How should discovered tools be cached in memory?
   - When should the cache be invalidated (time-based, manual, on reconnect)?
   - How to detect schema changes without full re-discovery?

5. **Integration with LLM**
   - How do MCP tools get merged with built-in tools?
   - What is the unified tool list presented to the LLM?
   - How are MCP tool results converted back for the LLM?

---

## Code Patterns to Analyze

### Tool Discovery from MCP Server
```typescript
// From client.ts
async function listServerTools(serverName: string): Promise<McpToolInfo[]> {
  const client = clients.get(serverName)
  if (!client) return []
  const result = await client.listTools()
  return mapTools(serverName, result.tools)
}

async function listTools(): Promise<McpToolInfo[]> {
  const all = await Promise.all([...clients.keys()].map(listServerTools))
  return all.flat()
}
```

- What is the .NET equivalent for `listTools()`?
- How should multiple server queries be parallelized in .NET?

### Tool Mapping with Prefixing
```typescript
// From client.ts
function mapTools(serverName: string, tools: Array<{ name: string; description?: string; inputSchema: unknown }>): McpToolInfo[] {
  return tools.map(t => ({
    server: serverName,
    originalName: t.name,
    prefixedName: `${serverName}${SEPARATOR}${t.name}`,
    description: t.description,
    inputSchema: (t.inputSchema ?? {}) as Record<string, unknown>,
  }))
}

// From types.ts
export interface McpToolInfo {
  server: string
  originalName: string
  prefixedName: string
  description?: string
  inputSchema: Record<string, unknown>
}
```

- How should this mapping be implemented in C#?
- What is the best way to handle the separator pattern?

### MCP Tool Schema Example (Zod-based)
```typescript
// From fs-read.tool.ts
export const fsReadInputSchema = z.object({
  path: z.string().min(1).describe('Relative path to file or directory'),
  mode: z.enum(['auto', 'tree', 'list', 'content']).optional().default('auto'),
  limit: z.number().int().min(1).max(2000).optional().default(100),
  include_files: z.boolean().optional().default(true),
  include_dirs: z.boolean().optional().default(true),
  track_changes: z.boolean().optional().default(false),
  pages: z.string().optional().describe('For PDFs: page range like "1-5, 8, 10-20"'),
  cell_id: z.string().optional().describe('For Jupyter notebooks: specific cell ID'),
})

export const fsReadTool = {
  name: 'fs_read',
  description: 'Read files and list directories from the workspace...',
  inputSchema: fsReadInputSchema,
  handler: async (args: unknown, _extra: HandlerExtra): Promise<CallToolResult> => {
    const parsed = fsReadInputSchema.safeParse(args)
    if (!parsed.success) {
      return {
        isError: true,
        content: [{ type: 'text', text: `Invalid arguments: ${parsed.error.message}` }],
      }
    }
    // Handler logic...
  },
}
```

- How does Zod schema translate to JSON Schema?
- What is the .NET equivalent for schema definition?

### Converting to LLM FunctionTool
```typescript
// Integration pattern (not in source, but implied)
function toFunctionTool(tool: McpToolInfo): FunctionTool {
  return {
    type: 'function',
    function: {
      name: tool.prefixedName,
      description: tool.description,
      parameters: tool.inputSchema, // Already in JSON Schema format
    },
  }
}
```

- How should MCP tools be converted to Microsoft.Extensions.AI `ChatTool`?
- What validation is needed for schema compatibility?

### Tool Execution with Prefix Parsing
```typescript
// From client.ts
async function callTool(prefixedName: string, args: Record<string, unknown>, signal?: AbortSignal): Promise<string> {
  const parsed = parsePrefixedName(prefixedName)
  if (!parsed) throw new Error(`Invalid MCP tool name: ${prefixedName}`)

  const client = clients.get(parsed.server)
  if (!client) {
    if (authRequired.has(parsed.server)) {
      throw new Error(`MCP server "${parsed.server}" requires OAuth authorization`)
    }
    throw new Error(`MCP server not connected: ${parsed.server}`)
  }

  const timeout = AbortSignal.timeout(CALL_TIMEOUT_MS)
  const combined = signal ? AbortSignal.any([signal, timeout]) : timeout

  const result = await client.callTool(
    { name: parsed.tool, arguments: args },
    undefined,
    { signal: combined },
  )

  if (result.isError) {
    const msg = extractText(result.content)
    throw new Error(msg || 'MCP tool returned an error')
  }

  if (result.structuredContent) {
    return JSON.stringify(result.structuredContent)
  }

  const text = extractText(result.content)
  return text || JSON.stringify(result.content)
}
```

- How should tool execution be routed in .NET?
- What is the best pattern for result extraction?

---

## Expected Deliverables

### 1. C# Tool Discovery Service
```csharp
// Proposed C# interfaces
public interface IMcpToolDiscoveryService
{
    Task<IReadOnlyList<McpToolInfo>> DiscoverAllToolsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<McpToolInfo>> DiscoverServerToolsAsync(string serverName, CancellationToken cancellationToken = default);
    void InvalidateCache(string? serverName = null);
    event EventHandler<ToolsChangedEventArgs>? ToolsChanged;
}

public record McpToolInfo
{
    public required string Server { get; init; }
    public required string OriginalName { get; init; }
    public required string PrefixedName { get; init; }
    public string? Description { get; init; }
    public required JsonElement InputSchema { get; init; }
}

public class ToolsChangedEventArgs : EventArgs
{
    public string? ServerName { get; init; }
    public IReadOnlyList<McpToolInfo>? AddedTools { get; init; }
    public IReadOnlyList<McpToolInfo>? RemovedTools { get; init; }
}
```

### 2. Schema Mapping to ChatTool
```csharp
// Converting MCP tools to Microsoft.Extensions.AI format
public static class McpToolExtensions
{
    private const string Separator = "__";

    public static ChatTool ToChatTool(this McpToolInfo mcpTool)
    {
        return new ChatTool
        {
            Name = mcpTool.PrefixedName,
            Description = mcpTool.Description,
            Parameters = mcpTool.InputSchema, // JsonElement
        };
    }

    public static (string Server, string Tool)? ParsePrefixedName(string prefixedName)
    {
        var idx = prefixedName.IndexOf(Separator, StringComparison.Ordinal);
        if (idx == -1) return null;
        return (
            prefixedName[..idx],
            prefixedName[(idx + Separator.Length)..]
        );
    }

    public static string CreatePrefixedName(string server, string tool)
        => $"{server}{Separator}{tool}";
}
```

### 3. Caching Implementation
```csharp
// Cached tool discovery with expiration
public class CachedMcpToolDiscovery : IMcpToolDiscoveryService
{
    private readonly IMcpToolDiscoveryService _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public async Task<IReadOnlyList<McpToolInfo>> DiscoverAllToolsAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = "mcp:tools:all";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<McpToolInfo>? cached))
        {
            return cached!;
        }

        var tools = await _inner.DiscoverAllToolsAsync(cancellationToken);

        _cache.Set(cacheKey, tools, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = _cacheDuration,
            SlidingExpiration = TimeSpan.FromMinutes(2),
        });

        return tools;
    }

    public void InvalidateCache(string? serverName = null)
    {
        if (serverName is null)
        {
            _cache.Remove("mcp:tools:all");
        }
        else
        {
            _cache.Remove($"mcp:tools:{serverName}");
        }
    }
}
```

### 4. Schema Comparison Table

| MCP Schema Feature | JSON Schema | LLM Compatibility | .NET Handling |
|-------------------|-------------|-------------------|---------------|
| `type` | Required | Required | Map directly |
| `properties` | Object | Object | JsonElement |
| `required` | String[] | String[] | Map directly |
| `enum` | String[] | String[] | Map directly |
| `description` | String | String | Map directly |
| `default` | Any | Optional | Handle carefully |
| `$ref` | Reference | Expand or error | Resolve references |
| `oneOf`/`anyOf` | Complex | May not support | Flatten or error |
| `additionalProperties` | Boolean/Object | Varies | Handle per provider |

### 5. Tool Discovery Flow Diagram
```
┌─────────────────────────────────────────────────────────────┐
│                    Agent Initialization                      │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                Load Built-in Tools                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │ Calculator  │  │  Delegate   │  │  Ask User   │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│              Connect to MCP Servers                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │ filesystem  │  │   github    │  │  slack      │         │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘         │
│         │                │                │                 │
│         ▼                ▼                ▼                 │
│  listTools()      listTools()      listTools()             │
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                   Map & Prefix Tools                         │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ filesystem__fs_read     filesystem__fs_write            ││
│  │ github__create_issue    github__list_prs                ││
│  │ slack__send_message     slack__list_channels            ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                  Merge All Tools                             │
│  ┌─────────────────────────────────────────────────────────┐│
│  │ Built-in Tools + MCP Tools (prefixed)                   ││
│  │                                                         ││
│  │ calculator, delegate, ask_user,                         ││
│  │ filesystem__fs_read, github__create_issue, ...          ││
│  └─────────────────────────────────────────────────────────┘│
└─────────────────────────┬───────────────────────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────┐
│               Present to LLM as FunctionTools               │
└─────────────────────────────────────────────────────────────┘
```

---

## Additional Research Areas

- [ ] Investigate JSON Schema validation libraries for .NET
- [ ] Research schema diffing algorithms for change detection
- [ ] Explore distributed caching for multi-instance deployments
- [ ] Document OpenAPI to MCP schema conversion
- [ ] Research async streaming for tool discovery
- [ ] Investigate tool versioning strategies

---

## Status

- [ ] TypeScript discovery patterns reviewed
- [ ] Schema mapping documented
- [ ] C# interfaces designed
- [ ] Caching strategy implemented
- [ ] Discovery flow diagram created
