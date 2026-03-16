# Investigation: MCP Client Manager

## Objective
Investigate how MCP (Model Context Protocol) client connections are managed, including connection lifecycle, server discovery, and multi-server coordination in both TypeScript and .NET.

---

## Source Files

### TypeScript MCP Client System
- **MCP Client Manager**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/client.ts (269 lines)
- **MCP Types**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/types.ts
- **MCP OAuth Provider**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/oauth.ts

### MCP SDK
- **@modelcontextprotocol/sdk**: Official TypeScript SDK for MCP
  - `Client` class from `@modelcontextprotocol/sdk/client/index.js`
  - `StdioClientTransport` for local process communication
  - `StreamableHTTPClientTransport` for HTTP-based MCP servers

---

## Microsoft Documentation

- **ModelContextProtocol NuGet**: https://www.nuget.org/packages/ModelContextProtocol
- **MCP .NET SDK**: https://github.com/modelcontextprotocol/csharp-sdk
- **Dependency Injection Patterns**: https://learn.microsoft.com/dotnet/core/extensions/dependency-injection

---

## Investigation Questions

1. **Connection Management**
   - How does TS manage MCP server connections using `StdioClientTransport` and `StreamableHTTPClientTransport`?
   - What is the connection lifecycle (connect, discover tools, call tools, disconnect)?
   - How are connections tracked and cleaned up?

2. **Transport Types**
   - How do stdio-based MCP servers differ from HTTP-based servers?
   - What are the trade-offs between `StdioClientTransport` and `StreamableHTTPClientTransport`?
   - How does .NET implement these transport types?

3. **Multi-Server Coordination**
   - How does TS handle multiple MCP servers with `Promise.allSettled`?
   - How are servers identified and namespaced (prefix pattern with `__` separator)?
   - What .NET patterns support managing multiple concurrent connections?

4. **Configuration Loading**
   - How is `.mcp.json` loaded and parsed?
   - What is the structure of `McpConfig` and `McpServerConfig`?
   - How can .NET achieve similar configuration binding?

5. **Error Handling**
   - How are connection failures handled with `Promise.allSettled`?
   - What happens when an MCP server requires authentication?
   - How should .NET handle transient vs permanent connection failures?

---

## Code Patterns to Analyze

### Client Creation and Transport Selection
```typescript
// From client.ts
function createStdioTransport(serverConfig: McpServerConfig & { transport?: 'stdio' }, rootDir: string) {
  return new StdioClientTransport({
    command: serverConfig.command,
    args: serverConfig.args,
    env: {
      PATH: process.env.PATH ?? '',
      HOME: process.env.HOME ?? '',
      NODE_ENV: process.env.NODE_ENV ?? '',
      ...serverConfig.env,
    },
    cwd: serverConfig.cwd ?? rootDir,
    stderr: 'inherit',
  })
}

function createHttpTransport(
  serverName: string,
  serverConfig: McpServerConfig & { transport: 'http' },
  rootDir: string,
  baseUrl: string,
) {
  const callbackUrl = `${baseUrl}/mcp/${serverName}/callback`
  const authProvider = createOAuthProvider(serverName, rootDir, callbackUrl)

  return new StreamableHTTPClientTransport(
    new URL(serverConfig.url),
    {
      authProvider,
      ...(serverConfig.headers && {
        requestInit: { headers: serverConfig.headers },
      }),
    },
  )
}
```

- What is the .NET equivalent for creating transports?
- How does .NET handle process spawning for stdio transport?

### Manager Interface Pattern
```typescript
// From client.ts
export interface McpManager {
  servers(): string[]
  serverStatus(name: string): 'connected' | 'auth_required' | 'disconnected'
  listTools(): Promise<McpToolInfo[]>
  listServerTools(serverName: string): Promise<McpToolInfo[]>
  callTool(prefixedName: string, args: Record<string, unknown>, signal?: AbortSignal): Promise<string>
  parseName(prefixedName: string): { server: string; tool: string } | undefined
  finishAuth(serverName: string, authorizationCode: string): Promise<void>
  close(): Promise<void>
}
```

- How should this interface be translated to C#?
- What async patterns should be used (Task<T>, ValueTask<T>)?

### Connection and Tool Discovery
```typescript
// From client.ts
const client = new Client(
  { name: 'agent-mcp-client', version: '1.0.0' },
  { capabilities: {} },
)

await client.connect(transport)
const result = await client.listTools()
return mapTools(serverName, result.tools)
```

- What is the .NET MCP SDK equivalent for `Client` class?
- How are tools discovered and listed in .NET?

### Tool Name Prefixing
```typescript
// From client.ts
const SEPARATOR = '__'

function mapTools(serverName: string, tools: Array<{ name: string; description?: string; inputSchema: unknown }>): McpToolInfo[] {
  return tools.map(t => ({
    server: serverName,
    originalName: t.name,
    prefixedName: `${serverName}${SEPARATOR}${t.name}`,
    description: t.description,
    inputSchema: (t.inputSchema ?? {}) as Record<string, unknown>,
  }))
}

function parsePrefixedName(prefixedName: string) {
  const idx = prefixedName.indexOf(SEPARATOR)
  if (idx === -1) return undefined
  return {
    server: prefixedName.slice(0, idx),
    tool: prefixedName.slice(idx + SEPARATOR.length),
  }
}
```

- How should name prefixing be implemented in .NET?
- Are there better approaches for namespacing tools?

---

## Expected Deliverables

### 1. C# MCP Client Manager Interface
```csharp
// Proposed C# interface
public interface IMcpManager : IAsyncDisposable
{
    IReadOnlyList<string> Servers { get; }
    McpServerStatus GetStatus(string serverName);
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<McpToolInfo>> ListServerToolsAsync(string serverName, CancellationToken cancellationToken = default);
    Task<string> CallToolAsync(string prefixedName, JsonElement args, CancellationToken cancellationToken = default);
    (string Server, string Tool)? ParseName(string prefixedName);
    Task FinishAuthAsync(string serverName, string authorizationCode, CancellationToken cancellationToken = default);
}

public enum McpServerStatus
{
    Connected,
    AuthRequired,
    Disconnected
}
```

### 2. Connection Lifecycle Diagram
```
┌─────────────────┐
│  Load Config    │  (.mcp.json)
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ Create Clients  │  For each server config
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────────┐
│ Create Transport├────►│ Stdio            │
└────────┬────────┘     │ HTTP (w/ OAuth)  │
         │              └──────────────────┘
         ▼
┌─────────────────┐
│   Connect       │  client.connect(transport)
└────────┬────────┘
         │
    ┌────┴────┐
    │ Success │ Auth Required
    ▼         ▼
┌───────┐  ┌──────────────┐
│Tools  │  │ Store AuthUrl│
│Ready  │  │ Wait OAuth   │
└───────┘  └──────────────┘
```

### 3. Configuration Binding Example
```csharp
// appsettings.json MCP configuration
{
  "Mcp": {
    "Servers": {
      "filesystem": {
        "Transport": "Stdio",
        "Command": "npx",
        "Args": ["-y", "@modelcontextprotocol/server-filesystem"]
      },
      "github": {
        "Transport": "Http",
        "Url": "https://api.github.com/mcp"
      }
    }
  }
}

// C# configuration classes
public class McpOptions
{
    public Dictionary<string, McpServerOptions> Servers { get; set; } = new();
}

public class McpServerOptions
{
    public string Transport { get; set; } = "stdio";
    public string? Command { get; set; }
    public string[]? Args { get; set; }
    public Dictionary<string, string>? Env { get; set; }
    public string? Url { get; set; }
    public Dictionary<string, string>? Headers { get; set; }
}
```

### 4. Comparison Table

| Feature | TypeScript | .NET | Notes |
|---------|------------|------|-------|
| Client Class | `Client` from MCP SDK | `McpClient` from ModelContextProtocol | |
| Transports | Stdio, HTTP | Stdio, HTTP (via HttpClient) | |
| Config Loading | Manual JSON parse | Options pattern + IOptionsMonitor | |
| Async Pattern | Promise | Task<T> | |
| Cancellation | AbortSignal | CancellationToken | |
| Error Handling | Promise.allSettled | Task.WhenAll + try/catch | |
| DI Support | Manual | Built-in IServiceCollection | |

---

## Additional Research Areas

- [ ] Investigate `ModelContextProtocol` NuGet package API surface
- [ ] Compare stdio transport implementation between TS and .NET
- [ ] Research .NET process management for stdio servers
- [ ] Document HTTP transport with HttpClient patterns
- [ ] Explore background service pattern for long-running MCP connections
- [ ] Investigate reconnection and retry policies

---

## Status

- [ ] TypeScript sources reviewed
- [ ] ModelContextProtocol NuGet explored
- [ ] C# interface designed
- [ ] Configuration binding implemented
- [ ] Connection lifecycle documented
