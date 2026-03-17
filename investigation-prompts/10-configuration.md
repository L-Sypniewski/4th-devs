---
title: Configuration System
description: Application settings, model registry, and runtime context dependency injection
related_files:
  - 01_05_agent/src/lib/config.ts
  - 01_05_agent/src/config/models.ts
  - 01_05_agent/src/runtime/context.ts
---

# Configuration System

## Overview

The configuration system uses a centralized approach with runtime validation through Zod schemas and dependency injection through a RuntimeContext pattern. Configuration is loaded from environment variables, validated, and made available throughout the application.

### Key Design Principles

- **Environment-based configuration** with schema validation
- **Model registry** with context window limits and pruning thresholds
- **Dependency injection container** for runtime services
- **Type-safe configuration** with TypeScript interfaces

## Configuration Flow

```mermaid
flowchart TD
    subgraph "Environment Variables"
        ENV[.env Files]
        PROC[process.env]
    end

    subgraph "Schema Validation"
        ZOD[Zod Schema]
        PARSE[parse()]
        CONFIG[Config Object]
    end

    subgraph "Runtime Context"
        CTX[RuntimeContext]
        EVTS[events]
        REPOS[repositories]
        TOOLS[tools]
        MCP[mcp]
    end

    subgraph "Application"
        APP[Application Components]
    end

    ENV --> PROC
    PROC --> ZOD
    ZOD --> PARSE
    PARSE --> CONFIG
    CONFIG --> CTX
    CTX --> EVTS
    CTX --> REPOS
    CTX --> TOOLS
    CTX --> MCP
    CTX --> APP
```

## Configuration Options

### Application Configuration (`src/lib/config.ts`)

| Option | Environment Variable | Default | Description |
|--------|---------------------|---------|-------------|
| port | PORT | 3000 | Server port |
| host | HOST | 127.0.0.1 | Server host |
| bodyLimit | BODY_LIMIT | 1048576 | Request body size limit (1MB) |
| timeoutMs | TIMEOUT_MS | 60000 | Request timeout |
| corsOrigin | CORS_ORIGIN | * | CORS allowed origins |
| openaiApiKey | OPENAI_API_KEY | - | OpenAI API key |
| openrouterApiKey | OPENROUTER_API_KEY | - | OpenRouter API key |
| geminiApiKey | GEMINI_API_KEY | - | Gemini API key |
| defaultModel | DEFAULT_MODEL | provider:gpt-5.4 | Default model |
| maxTurns | AGENT_MAX_TURNS | 10 | Maximum turns per agent |
| databaseUrl | DATABASE_URL | file:.data/agent.db | Database URL |
| databaseAuthToken | DATABASE_AUTH_TOKEN | - | Database auth token |
| workspacePath | WORKSPACE_PATH | ./workspace | Agent templates path |
| logLevel | LOG_LEVEL | info | Logging level |
| nodeEnv | NODE_ENV | development | Environment |
| rateLimitRpm | RATE_LIMIT_RPM | 60 | Rate limit per minute |
| shutdownTimeoutMs | SHUTDOWN_TIMEOUT_MS | 10000 | Graceful shutdown timeout |
| langfusePublicKey | LANGFUSE_PUBLIC_KEY | - | Langfuse public key |
| langfuseSecretKey | LANGFUSE_SECRET_KEY | - | Langfuse secret key |
| langfuseBaseUrl | LANGFUSE_BASE_URL | - | Langfuse base URL |

### Schema Definition

```typescript
// src/lib/config.ts:6-68
const envSchema = z.object({
  PORT: z.coerce.number().default(3000),
  HOST: z.string().default('127.0.0.1'),
  BODY_LIMIT: z.coerce.number().default(1024 * 1024),
  TIMEOUT_MS: z.coerce.number().default(60000),
  CORS_ORIGIN: z.string().default('*'),
  OPENAI_API_KEY: z.string().optional(),
  OPENROUTER_API_KEY: z.string().optional(),
  GEMINI_API_KEY: z.string().optional(),
  DEFAULT_MODEL: z.string().default('provider:gpt-5.4'),
  AGENT_MAX_TURNS: z.coerce.number().default(10),
  DATABASE_URL: z.string().default('file:.data/agent.db'),
  DATABASE_AUTH_TOKEN: z.string().optional(),
  WORKSPACE_PATH: z.string().default('./workspace'),
  LOG_LEVEL: z.enum(['fatal', 'error', 'warn', 'info', 'debug', 'trace']).default('info'),
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
  RATE_LIMIT_RPM: z.coerce.number().default(60),
  SHUTDOWN_TIMEOUT_MS: z.coerce.number().default(10000),
  LANGFUSE_PUBLIC_KEY: z.string().optional(),
  LANGFUSE_SECRET_KEY: z.string().optional(),
  LANGFUSE_BASE_URL: z.string().optional(),
})

export const config = envSchema.parse(process.env)
```

## Model Registry

### Model Definitions (`src/config/models.ts:34-79`)

| Model ID | Provider | Context Window | Max Output | Pruning Config |
|----------|----------|----------------|------------|----------------|
| gpt-5.4 | openai | 400,000 | 128,000 | threshold: 0.85, target: 0.5, recent: 5 |
| gpt-5.2 | openai | 400,000 | 128,000 | threshold: 0.85, target: 0.5, recent: 5 |
| gemini-3-pro-preview | gemini | 1,048,576 | 65,536 | threshold: 0.90, target: 0.6, recent: 10 |
| gemini-3-flash-preview | gemini | 1,048,576 | 65,536 | threshold: 0.90, target: 0.6, recent: 10 |

### Pruning Thresholds Structure

```typescript
// src/config/models.ts:5-16
interface PruningThresholds {
  threshold: number           // Trigger at 85% context window
  targetUtilization: number   // Target 50% after pruning
  minRecentTurns: number      // Keep 3-5 recent turns verbatim
  maxToolOutputChars: number  // Max 10,000 chars per tool output
  enableSummarization: boolean // Enable LLM summarization
}

const DEFAULT_PRUNING: PruningThresholds = {
  threshold: 0.85,
  targetUtilization: 0.5,
  minRecentTurns: 3,
  maxToolOutputChars: 10_000,
  enableSummarization: true,
}
```

### Model Resolution

```typescript
// src/config/models.ts:82-90
function getModelDefinition(modelId: string): ModelDefinition {
  return MODELS[modelId] ?? {
    id: modelId,
    provider: 'openai',
    contextWindow: 128_000,
    maxOutputTokens: 16_000,
    pruning: DEFAULT_PRUNING,
  }
}
```

## RuntimeContext Structure

### Interface Definition

```typescript
// src/runtime/context.ts
interface RuntimeContext {
  events: AgentEventEmitter     // Event system for tracing
  repositories: Repositories    // Data access layer
  tools: ToolRegistry           // Tool registry and execution
  mcp: McpManager              // MCP protocol manager
}
```

### ExecutionContext

For request-scoped execution:

```typescript
interface ExecutionContext {
  traceId: TraceId             // Unique request trace ID
  rootAgentId: AgentId        // Root agent ID
  parentAgentId?: AgentId      // Parent agent ID (nested agents)
  depth: number               // Agent nesting depth
  userId?: string             // User identifier
  userInput?: unknown          // Original user input
  agentName?: string          // Human-readable agent name
}
```

### Factory Function

```typescript
// src/runtime/context.ts
export function createContext(
  events: AgentEventEmitter,
  repositories: Repositories,
  tools: ToolRegistry,
  mcp: McpManager,
): RuntimeContext {
  return { events, repositories, tools, mcp }
}
```

### Dependency Injection Pattern

The RuntimeContext follows a functional DI pattern:

1. Services are created externally (in main application entry point)
2. Context aggregates all dependencies
3. Components receive context as a parameter
4. No global state or singleton registry

## Workspace Configuration

The workspace path (default: `./workspace`) is used for:

- Agent template storage (`.agent.md` files)
- Tool definitions
- Custom agent configurations
- MCP configuration (`.mcp.json`)

```typescript
// src/workspace/loader.ts:114-131
export async function resolveAgent(
  name: string,
  workspacePath: string,
  registry: ToolRegistry,
  mcp: McpManager,
): Promise<LoadedAgent | undefined> {
  const filePath = join(workspacePath, 'agents', `${name}.agent.md`)
  // Load and resolve agent template...
}
```

## .NET Mapping Section

### IConfiguration Interface

```csharp
// appsettings.json
{
  "Application": {
    "Port": 3000,
    "Host": "127.0.0.1",
    "BodyLimit": 1048576,
    "TimeoutMs": 60000,
    "CorsOrigin": "*",
    "DefaultModel": "provider:gpt-5.4",
    "MaxTurns": 10,
    "WorkspacePath": "./workspace",
    "LogLevel": "info",
    "RateLimitRpm": 60,
    "ShutdownTimeoutMs": 10000
  },
  "Database": {
    "Url": "file:.data/agent.db",
    "AuthToken": null
  },
  "Providers": {
    "OpenAI": { "ApiKey": null },
    "OpenRouter": { "ApiKey": null },
    "Gemini": { "ApiKey": null }
  },
  "Langfuse": {
    "PublicKey": null,
    "SecretKey": null,
    "BaseUrl": null
  }
}

// Configuration class
public class ApplicationOptions
{
    public int Port { get; set; } = 3000;
    public string Host { get; set; } = "127.0.0.1";
    public int BodyLimit { get; set; } = 1048576;
    public int TimeoutMs { get; set; } = 60000;
    public string CorsOrigin { get; set; } = "*";
    public string DefaultModel { get; set; } = "provider:gpt-5.4";
    public int MaxTurns { get; set; } = 10;
    public string WorkspacePath { get; set; } = "./workspace";
    public string LogLevel { get; set; } = "info";
    public int RateLimitRpm { get; set; } = 60;
    public int ShutdownTimeoutMs { get; set; } = 10000;
}
```

### Options Pattern Registration

```csharp
// Program.cs
builder.Services.AddOptions<ApplicationOptions>()
    .Bind(builder.Configuration.GetSection("Application"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Access via DI
public class MyService
{
    private readonly ApplicationOptions _options;

    public MyService(IOptions<ApplicationOptions> options)
    {
        _options = options.Value;
    }
}
```

### RuntimeContext Equivalent

```csharp
// Runtime services container
public class RuntimeContext
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

// DI registration
services.AddSingleton<RuntimeContext>(provider =>
    new RuntimeContext(
        provider.GetRequiredService<IAgentEventEmitter>(),
        provider.GetRequiredService<IRepositories>(),
        provider.GetRequiredService<IToolRegistry>(),
        provider.GetRequiredService<IMcpManager>()));
```

### Model Registry Equivalent

```csharp
public class ModelRegistry
{
    private readonly Dictionary<string, ModelDefinition> _models = new()
    {
        ["gpt-5.4"] = new ModelDefinition
        {
            Id = "gpt-5.4",
            Provider = "openai",
            ContextWindow = 400_000,
            MaxOutputTokens = 128_000,
            Pruning = PruningThresholds.Default with { MinRecentTurns = 5 }
        },
        ["gemini-3-pro-preview"] = new ModelDefinition
        {
            Id = "gemini-3-pro-preview",
            Provider = "gemini",
            ContextWindow = 1_048_576,
            MaxOutputTokens = 65_536,
            Pruning = PruningThresholds.Default with
            {
                Threshold = 0.90,
                TargetUtilization = 0.6,
                MinRecentTurns = 10
            }
        }
    };

    public ModelDefinition GetModel(string modelId) =>
        _models.TryGetValue(modelId, out var definition)
            ? definition
            : ModelDefinition.Default;
}
```

### Pattern Mapping Table

| TypeScript Pattern | .NET Equivalent |
|-------------------|-----------------|
| `config` object | `IOptions<T>` |
| `RuntimeContext` | `IServiceProvider` or custom container |
| `createContext()` | Constructor injection |
| Zod validation | Data Annotations + `ValidateDataAnnotations()` |
| Environment variables | `IConfiguration` providers |
| `.env` files | `DotNetEnv` or user secrets |
