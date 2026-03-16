# Investigation: High-Level TypeScript to .NET Architecture Mapping

## Objective
Investigate how TypeScript components map to .NET equivalents in the Microsoft Agent Framework.

---

## Source Files

- **Runner**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
- **Agent Domain**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/domain/agent.ts

---

## Investigation Questions

1. **Agent Class Mapping**
   - How does the TS `Agent` class map to Microsoft Agent Framework's `Agent` base class?
   - What properties and methods are directly translatable?
   - What TS-specific patterns need adaptation?

2. **Provider Pattern**
   - What is the .NET equivalent of the TS provider pattern?
   - How does dependency injection in .NET compare to TS provider injection?
   - Are there built-in .NET abstractions for providers?

3. **Interface Translation**
   - How do TS interfaces like `Provider`, `AgentState`, `Tool` translate to C#?
   - Should these become interfaces, abstract classes, or records?
   - What about TypeScript-specific types (e.g., union types, type guards)?

---

## Code Patterns to Analyze

### Agent Creation
```typescript
// TS Pattern
const agent = new Agent({
  providers: [provider1, provider2],
  tools: [tool1, tool2],
  initialState: AgentState.Idle
});
```

- How is agent instantiation handled in .NET?
- What builder patterns or factory methods are idiomatic?

### State Transitions
```typescript
// TS Pattern
agent.transitionTo(AgentState.Running);
```

- What is the .NET equivalent for state management?
- Are there event-based patterns for state changes?

### Provider Injection
```typescript
// TS Pattern
class MyAgent extends Agent {
  constructor(private providers: Provider[]) {}
}
```

- How does constructor injection work in .NET?
- What about `IServiceProvider` and DI containers?

---

## Microsoft Documentation

- **Agent Framework Overview**: https://learn.microsoft.com/agent-framework/overview/agent-framework-overview
- **Getting Started Guide**: https://learn.microsoft.com/agent-framework/getting-started
- **Agent Basics**: https://learn.microsoft.com/agent-framework/agents/basics

---

## Expected Deliverables

1. **Class Diagram**: Visual representation showing TS to .NET mapping
   - Include: Agent, Provider, Tool, AgentState classes/interfaces
   - Show: Inheritance hierarchies, composition relationships

2. **Mapping Table**: Detailed property/method mapping

| TypeScript | C# Equivalent | Notes |
|------------|---------------|-------|
| `Agent` class | `Agent` base class | ... |
| `Provider` interface | `IProvider` interface | ... |
| `AgentState` enum | `AgentState` enum | ... |
| `Tool` interface | `ITool` interface | ... |

3. **Code Samples**: Side-by-side comparison of key patterns

---

## Additional Research Areas

- [ ] Compare async/await patterns between TS and C#
- [ ] Investigate event handling differences
- [ ] Document any TypeScript features without direct C# equivalents
- [ ] Identify third-party NuGet packages that may be needed

---

## Status

- [ ] Sources reviewed
- [ ] Microsoft docs consulted
- [ ] Class diagram created
- [ ] Mapping table completed
- [ ] Code samples documented
