# Investigation Prompts: Porting 01_05_agent to Microsoft .NET Frameworks

## Overview

This investigation guides porting a TypeScript agent runtime to .NET using:
- **Microsoft Agent Framework** for high-level orchestration
- **Barebone ChatClient** from Microsoft.Extensions.AI for low-level control
- **.NET Aspire** for service hosting
- **OpenTelemetry** for observability

## Reference Sources

### GitHub Repository
- **Main URL**: https://github.com/i-am-alice/4th-devs/tree/main/01_05_agent

### Critical Files to Analyze
1. `src/runtime/runner.ts` - Core agent execution loop (1078 lines)
2. `src/providers/types.ts` - Provider abstraction interface (79 lines)
3. `src/domain/agent.ts` - Agent entity and state machine (195 lines)
4. `src/runtime/context.ts` - RuntimeContext DI pattern (48 lines)
5. `src/mcp/client.ts` - MCP client manager (269 lines)

### Microsoft Documentation URLs

**ChatClient (Microsoft.Extensions.AI):**
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.ichatclient
- https://learn.microsoft.com/dotnet/ai/ichatclient
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.chatclientbuilder
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient
- https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.functioninvokingchatclient

**Agent Framework:**
- https://learn.microsoft.com/agent-framework/overview/agent-framework-overview
- https://learn.microsoft.com/agent-framework/agents/agent-pipeline
- https://learn.microsoft.com/agent-framework/agents/observability
- https://learn.microsoft.com/agent-framework/agents/tools/function-tools

**Resilience:**
- https://learn.microsoft.com/dotnet/core/resilience/

## Reading Guide

| Phase | Focus | Files | Priority |
|-------|-------|-------|----------|
| Phase 1 | Foundation | 00, 01-01, 02-01 | High |
| Phase 2 | Core Patterns | 01-02, 03-01, 03-02 | High |
| Phase 3 | Providers | 02-02, 02-03 | Medium |
| Phase 4 | Agent Features | 01-03, 03-03, 08-01 | Medium |
| Phase 5 | Production | 05-01, 06-01, 06-03, 06-04 | Medium |
| Phase 6 | Integration | 04-01, 04-02 | Low |
| Phase 7 | Deployment | 07-01, 07-02, 09-02 | Low |
| Phase 8 | Execution | 10-01, 10-02, 10-03 | Low |

**Investigation Order:**
1. Phase 1 - Foundation (files 00, 01-01, 02-01)
2. Phase 2 - Core Patterns (files 01-02, 03-01, 03-02)
3. Phase 3 - Providers (files 02-02, 02-03)
4. Phase 4 - Agent Features (files 01-03, 03-03, 08-01)
5. Phase 5 - Production (files 05-01, 06-01, 06-03, 06-04)
6. Phase 6 - Integration (files 04-01, 04-02)
7. Phase 7 - Deployment (files 07-01, 07-02, 09-02)
8. Phase 8 - Execution (files 10-01, 10-02, 10-03)

## Key Feature Mapping Table

| 01_05_agent Feature | Microsoft Equivalent | Status |
|---------------------|---------------------|--------|
| Provider interface | IChatClient | Map |
| Rate limiting | UseRateLimiting/Polly | Map |
| Retry logic | Microsoft.Extensions.Resilience | Map |
| OpenTelemetry | OpenTelemetryChatClient | Map |
| Tool registry | AIFunctionFactory | Extend |
| MCP integration | Custom | Implement |
| Event system | Agent middleware | Extend |
| Conversation state | ChatHistoryProvider | Extend |
| Hierarchical agents | Agent handoffs | Extend |
| Non-blocking execution | Custom | Implement |
| SQLite persistence | Custom provider | Implement |
