# Investigation: Non-Blocking Execution with Waiting/Resume Patterns

## Objective
Investigate how TypeScript implements non-blocking agent execution with waiting states and resumption, then design equivalent patterns for .NET using TaskCompletionSource and related primitives.

---

## Source Files

- **Runner**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
- **Agent Domain**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/domain/agent.ts

---

## Investigation Questions

1. **Agent Waiting States**
   - How does TypeScript handle agent waiting states (waiting for tool result, human input)?
   - What is the `WaitingFor` type and what variants does it support (tool, human, agent)?
   - How does `waitForMany()` transition an agent to waiting state?
   - What metadata is preserved during waiting (callId, name, description)?

2. **Execution Pause and Resume**
   - How is execution paused when an agent needs external input?
   - What happens when `runAgent()` returns `{ status: 'waiting' }`?
   - How does `deliverResult()` resume a waiting agent?
   - What is the role of `deliverOne()` in clearing individual wait items?

3. **.NET Patterns for Async Waiting**
   - How can `TaskCompletionSource<T>` represent pending tool results?
   - What role do `Channel<T>` and `BlockingCollection<T>` play?
   - How to use `CancellationToken` for timeout and cancellation?
   - What about `AsyncManualResetEvent` or `AsyncAutoResetEvent`?

4. **Durable Waiting (Persistence)**
   - How to persist waiting state to survive process restarts?
   - What information must be serialized (agent state, conversation, waiting list)?
   - How to implement out-of-process resumption (e.g., via message queue)?
   - What patterns exist for distributed waiting (Dapr, Azure Durable Functions)?

---

## Code Patterns to Analyze

### TypeScript Waiting State Transition

```typescript
// From agent.ts - Transition to waiting
export function waitForMany(agent: Agent, waiting: WaitingFor[]): TransitionResult {
  if (agent.status !== 'running') {
    return { ok: false, error: `Cannot wait agent in status: ${agent.status}` }
  }
  return {
    ok: true,
    agent: { ...agent, status: 'waiting', waitingFor: waiting },
  }
}
```

**Analysis Points:**
- State transition only valid from 'running' status
- Waiting list replaces any previous waiting items
- Agent retains all context (sessionId, traceId, hierarchy)

### TypeScript Resume After Delivery

```typescript
// From agent.ts - Resume after single delivery
export function deliverOne(agent: Agent, callId: CallId): TransitionResult {
  if (agent.status !== 'waiting') {
    return { ok: false, error: `Cannot deliver to agent in status: ${agent.status}` }
  }

  const remaining = agent.waitingFor.filter(w => w.callId !== callId)

  if (remaining.length === agent.waitingFor.length) {
    return { ok: false, error: `Agent not waiting for callId: ${callId}` }
  }

  const newStatus = remaining.length === 0 ? 'running' : 'waiting'

  return {
    ok: true,
    agent: { ...agent, status: newStatus, waitingFor: remaining },
  }
}
```

**Analysis Points:**
- Partial delivery supported (multiple concurrent waits)
- Auto-transitions to 'running' when all waits satisfied
- Validates callId exists in waiting list

### .NET Equivalent - TaskCompletionSource Pattern

```csharp
// C# Pattern Template
public class WaitingContext<TResult>
{
    private readonly TaskCompletionSource<TResult> _tcs = new();
    private readonly CancellationTokenSource _timeoutCts;
    private readonly DateTimeOffset _createdAt;

    public CallId CallId { get; }
    public WaitingType Type { get; }
    public string Name { get; }
    public string? Description { get; }
    public Task<TResult> Task => _tcs.Task;
    public bool IsCompleted => _tcs.Task.IsCompleted;

    public WaitingContext(CallId callId, WaitingType type, string name, TimeSpan? timeout = null)
    {
        CallId = callId;
        Type = type;
        Name = name;
        _createdAt = DateTimeOffset.UtcNow;

        if (timeout.HasValue)
        {
            _timeoutCts = new CancellationTokenSource(timeout.Value);
            _timeoutCts.Token.Register(() =>
                _tcs.TrySetException(new TimeoutException($"Wait for {name} timed out")));
        }
    }

    public bool TrySetResult(TResult result) => _tcs.TrySetResult(result);
    public bool TrySetException(Exception ex) => _tcs.TrySetException(ex);
    public bool TrySetCancelled() => _tcs.TrySetCanceled();
}

public enum WaitingType { Tool, Human, Agent }
```

### .NET Equivalent - Async State Machine

```csharp
// C# Pattern Template - Agent execution loop
public async Task<RunResult> RunAgentAsync(
    AgentId agentId,
    CancellationToken cancellationToken = default)
{
    var agent = await _agentRepository.GetByIdAsync(agentId, cancellationToken);

    while (agent.Status == AgentStatus.Running && agent.TurnCount < _maxTurns)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var turnResult = await ExecuteTurnAsync(agent, cancellationToken);

        if (turnResult.IsWaiting)
        {
            // Non-blocking return - agent waits for external input
            agent = await TransitionToWaitingAsync(agent, turnResult.WaitingFor);
            return RunResult.Waiting(agent, turnResult.WaitingFor);
        }

        if (!turnResult.ShouldContinue)
            break;

        agent = IncrementTurn(agent);
        await _agentRepository.UpdateAsync(agent, cancellationToken);
    }

    return RunResult.Completed(agent);
}
```

### .NET Equivalent - Durable Waiting with Persistence

```csharp
// C# Pattern Template - Persistable waiting state
public class DurableWaitingState
{
    public AgentId AgentId { get; init; }
    public string TraceId { get; init; }
    public IReadOnlyList<WaitingForItem> WaitingFor { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string? CheckpointData { get; init; } // Serialized conversation state

    public static DurableWaitingState FromAgent(Agent agent)
    {
        return new DurableWaitingState
        {
            AgentId = agent.Id,
            TraceId = agent.TraceId,
            WaitingFor = agent.WaitingFor.ToList(),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

public interface IWaitingStateStore
{
    Task SaveAsync(DurableWaitingState state, CancellationToken cancellationToken);
    Task<DurableWaitingState?> LoadAsync(AgentId agentId, CancellationToken cancellationToken);
    Task DeleteAsync(AgentId agentId, CancellationToken cancellationToken);
}
```

---

## State Diagram: Waiting and Resume Flow

```
                    +----------+
                    | Running  |
                    +----------+
                         |
                         | waitForMany()
                         v
                  +------------+    deliverOne()     +------------+
                  |  Waiting   | ------------------> |  Waiting   |
                  +------------+ (partial)           +------------+
                       |   ^                              |
                       |   |                              | deliverOne()
                       |   |    deliverOne()              | (all complete)
                       |   +------------------------------+
                       |                              |
                       |                              v
                       +------------------------> +----------+
                                                  | Running  |
                                                  +----------+
```

---

## Microsoft Documentation

- **TaskCompletionSource**: https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskcompletionsource-1
- **Channels**: https://learn.microsoft.com/dotnet/core/extensions/channels
- **Async Programming**: https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/
- **Durable Functions**: https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-overview
- **Dapr Actor State**: https://learn.microsoft.com/dotnet/develop-dotnet-actors

---

## Expected Deliverables

1. **Waiting/Resume Service Interface**

```csharp
public interface IWaitingService
{
    /// <summary>
    /// Creates a waiting context for a tool/human/agent call
    /// </summary>
    WaitingContext<ToolResult> CreateWait(CallId callId, WaitingType type, string name, TimeSpan? timeout = null);

    /// <summary>
    /// Gets an existing waiting context
    /// </summary>
    WaitingContext<ToolResult>? GetWait(CallId callId);

    /// <summary>
    /// Delivers a result to a waiting context
    /// </summary>
    bool TryDeliverResult(CallId callId, ToolResult result);

    /// <summary>
    /// Waits for all specified callIds to complete
    /// </summary>
    Task<Dictionary<CallId, ToolResult>> WaitAllAsync(
        IEnumerable<CallId> callIds,
        CancellationToken cancellationToken = default);
}
```

2. **Agent State Persistence Interface**

```csharp
public interface IAgentStatePersistor
{
    Task CheckpointAsync(Agent agent, CancellationToken cancellationToken = default);
    Task<Agent?> RestoreAsync(AgentId agentId, CancellationToken cancellationToken = default);
    Task ClearAsync(AgentId agentId, CancellationToken cancellationToken = default);
}
```

3. **Implementation Notes Document** covering:
   - Thread-safety considerations for concurrent waiters
   - Timeout handling strategies
   - Memory management for long-lived waits
   - Integration with DI container

---

## Validation Checklist

- [ ] WaitingFor type variants documented
- [ ] State transition rules mapped
- [ ] TaskCompletionSource pattern designed
- [ ] Timeout handling implemented
- [ ] Persistence strategy defined
- [ ] Thread-safety verified

---

## Status

- [ ] Source code analyzed
- [ ] Waiting state transitions documented
- [ ] C# TaskCompletionSource pattern implemented
- [ ] Durable waiting pattern designed
- [ ] Integration tests written
