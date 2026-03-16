# Investigation: Result Delivery to Waiting Agents

## Objective
Investigate how TypeScript delivers tool results back to waiting agents and design a .NET result delivery service with correlation, timeout handling, and concurrent coordination support.

---

## Source Files

- **Runner (deliverResult)**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
- **Agent Domain (deliverOne)**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/domain/agent.ts

---

## Investigation Questions

1. **Result Delivery Mechanism**
   - How does TypeScript deliver tool results back to waiting agents?
   - What is the signature and flow of `deliverResult()` in runner.ts?
   - How are results stored as `function_call_output` items?
   - How does delivery trigger agent resumption?

2. **Result Correlation**
   - How to correlate results with waiting requests (by callId)?
   - What happens when delivering to a non-existent callId?
   - How to handle duplicate delivery attempts?
   - What validation ensures the agent is in 'waiting' status?

3. **Timeout Handling**
   - How to handle timeouts for waiting operations?
   - Should timeouts be per-call or aggregate for all waits?
   - How to clean up timed-out waiting contexts?
   - What error should be returned on timeout?

4. **Concurrent Coordination**
   - How to support multiple concurrent waiting operations?
   - How does `waitForMany()` handle multiple wait items?
   - How does partial delivery work (some calls complete, others pending)?
   - What happens when all waits are satisfied?

---

## Code Patterns to Analyze

### TypeScript Result Delivery

```typescript
// From runner.ts - Deliver result to waiting agent
export async function deliverResult(
  agentId: AgentId,
  callId: CallId,
  result: ToolResult,
  runtime: RuntimeContext,
  execution?: ExecutionContext
): Promise<RunResult> {
  let agent = await runtime.repositories.agents.getById(agentId)
  if (!agent) {
    return { ok: false, status: 'failed', error: `Agent not found: ${agentId}` }
  }

  if (agent.status !== 'waiting') {
    return { ok: false, status: 'failed', error: `Agent not waiting: ${agent.status}` }
  }

  // Add result as function_call_output
  await runtime.repositories.items.create(agent.id, {
    type: 'function_call_output',
    callId,
    output: result.ok ? result.output : result.error,
    isError: !result.ok,
  })

  // Remove from waiting list
  const deliverRes = deliverOne(agent, callId)
  if (!deliverRes.ok) {
    return { ok: false, status: 'failed', error: deliverRes.error }
  }
  agent = await runtime.repositories.agents.update(deliverRes.agent)

  // Emit resume event
  runtime.events.emit({
    type: 'agent.resumed',
    ctx: makeCtx(exec, agent),
    deliveredCallId: callId,
    remaining: agent.waitingFor.length,
  })

  // If still waiting, return
  if (agent.waitingFor.length > 0) {
    return { ok: true, status: 'waiting', agent, waitingFor: agent.waitingFor }
  }

  // All delivered, continue execution
  const runResult = await runAgent(agentId, runtime, { execution: exec })

  // Auto-propagation: if completed and has parent, deliver upward
  if (
    runResult.ok &&
    runResult.status === 'completed' &&
    agent.parentId &&
    agent.sourceCallId
  ) {
    const output = extractAgentResult(runResult.items)
    return deliverResult(
      agent.parentId,
      agent.sourceCallId,
      { ok: true, output },
      runtime,
      createExecutionContext(exec.traceId, agent.rootAgentId, undefined, agent.depth - 1)
    )
  }

  return runResult
}
```

**Analysis Points:**
- Validates agent exists and is in 'waiting' status
- Stores result as conversation item (function_call_output)
- Removes callId from waiting list via `deliverOne()`
- Emits 'agent.resumed' event with remaining count
- Auto-resumes execution when all waits complete
- Auto-propagates results to parent agent (hierarchy)

### TypeScript Correlation via callId

```typescript
// From agent.ts - Correlate delivery with waiting request
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
- Filters waiting list to remove matching callId
- Detects missing callId (no change in list length)
- Transitions to 'running' only when all complete

### .NET Equivalent - Result Delivery Service

```csharp
// C# Pattern Template
public interface IResultDeliveryService
{
    /// <summary>
    /// Delivers a tool result to a waiting agent
    /// </summary>
    Task<DeliveryResult> DeliverAsync(
        AgentId agentId,
        CallId callId,
        ToolResult result,
        CancellationToken cancellationToken = default);
}

public record DeliveryResult
{
    public bool Ok { get; init; }
    public DeliveryStatus Status { get; init; }
    public Agent? Agent { get; init; }
    public IReadOnlyList<WaitingForItem>? RemainingWaits { get; init; }
    public string? Error { get; init; }
}

public enum DeliveryStatus
{
    Delivered,          // Result delivered, still waiting for more
    Resumed,           // All waits complete, agent resumed
    Completed,         // Agent completed execution
    AgentNotFound,
    AgentNotWaiting,
    CallIdNotFound,
    Failed
}
```

### .NET Equivalent - Correlation with ConcurrentDictionary

```csharp
// C# Pattern Template - Thread-safe correlation
public class WaitingRegistry
{
    private readonly ConcurrentDictionary<CallId, WaitingEntry> _entries = new();

    public void Register(CallId callId, AgentId agentId, WaitingType type, string name)
    {
        var entry = new WaitingEntry(agentId, type, name, DateTimeOffset.UtcNow);
        _entries.TryAdd(callId, entry);
    }

    public bool TryGetAgentId(CallId callId, out AgentId agentId)
    {
        if (_entries.TryGetValue(callId, out var entry))
        {
            agentId = entry.AgentId;
            return true;
        }
        agentId = default;
        return false;
    }

    public void Unregister(CallId callId)
    {
        _entries.TryRemove(callId, out _);
    }

    public IReadOnlyList<CallId> GetAgentWaits(AgentId agentId)
    {
        return _entries
            .Where(kvp => kvp.Value.AgentId == agentId)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private record WaitingEntry(AgentId AgentId, WaitingType Type, string Name, DateTimeOffset CreatedAt);
}
```

### .NET Equivalent - Timeout Handling

```csharp
// C# Pattern Template - Timeout with cancellation
public class TimeoutAwareWaitingContext
{
    private readonly TaskCompletionSource<ToolResult> _tcs;
    private readonly CancellationTokenSource _timeoutCts;
    private readonly Timer? _timeoutTimer;

    public CallId CallId { get; }
    public TimeSpan Timeout { get; }
    public DateTimeOffset CreatedAt { get; }
    public Task<ToolResult> Task => _tcs.Task;

    public TimeoutAwareWaitingContext(CallId callId, TimeSpan timeout)
    {
        CallId = callId;
        Timeout = timeout;
        CreatedAt = DateTimeOffset.UtcNow;

        _tcs = new TaskCompletionSource<ToolResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _timeoutCts = new CancellationTokenSource();

        if (timeout != Timeout.InfiniteTimeSpan)
        {
            _timeoutCts.Token.Register(() =>
                _tcs.TrySetResult(new ToolResult(
                    Ok: false,
                    Output: null,
                    Error: $"Operation timed out after {timeout.TotalSeconds}s")));
        }
    }

    public bool TryDeliver(ToolResult result)
    {
        if (_tcs.Task.IsCompleted)
            return false;

        _timeoutCts.Cancel(); // Stop timeout timer
        return _tcs.TrySetResult(result);
    }

    public void Cancel()
    {
        _timeoutCts.Cancel();
        _tcs.TrySetCanceled();
    }
}
```

### .NET Equivalent - Concurrent Coordination

```csharp
// C# Pattern Template - Wait for multiple results
public class ConcurrentWaitingCoordinator
{
    private readonly Dictionary<CallId, TaskCompletionSource<ToolResult>> _waiters = new();

    public void RegisterWait(CallId callId)
    {
        _waiters[callId] = new TaskCompletionSource<ToolResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public bool TryDeliverResult(CallId callId, ToolResult result)
    {
        if (_waiters.TryGetValue(callId, out var tcs))
        {
            return tcs.TrySetResult(result);
        }
        return false;
    }

    public async Task<IReadOnlyDictionary<CallId, ToolResult>> WaitAllAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var tasks = _waiters.Select(kvp =>
            kvp.Value.Task.ContinueWith(t => new { CallId = kvp.Key, Result = t.Result }));

        try
        {
            var results = await Task.WhenAll(tasks);
            return results.ToDictionary(r => r.CallId, r => r.Result);
        }
        catch (OperationCanceledException)
        {
            // Return partial results for completed waits
            return _waiters
                .Where(kvp => kvp.Value.Task.IsCompletedSuccessfully)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Task.Result);
        }
    }

    public async Task<(CallId CallId, ToolResult Result)> WaitAnyAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var tasks = _waiters.Select(kvp =>
            kvp.Value.Task.ContinueWith(t => (CallId: kvp.Key, Result: t.Result)));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var completedTask = await Task.WhenAny(tasks);
        return await completedTask;
    }
}
```

---

## Sequence Diagram: Result Delivery Flow

```
+--------+     +-------------+     +--------------+     +-------------+
| Client |     | DeliverySvc |     | AgentRepo    |     | Runner      |
+--------+     +-------------+     +--------------+     +-------------+
    |                |                    |                    |
    | deliverResult  |                    |                    |
    |-------------->|                    |                    |
    |                | getById(agentId)   |                    |
    |                |------------------>|                    |
    |                |<-- agent (waiting)|                    |
    |                |                    |                    |
    |                | create item        |                    |
    |                | (function_output)  |                    |
    |                |------------------>|                    |
    |                |                    |                    |
    |                | deliverOne(agent)  |                    |
    |                |-------------------------+              |
    |                |<--- updated agent --------+              |
    |                |                    |                    |
    |                | update agent       |                    |
    |                |------------------>|                    |
    |                |                    |                    |
    |                | emit 'resumed'     |                    |
    |                |-------------------+                    |
    |                |                    |                    |
    |                | [if all delivered]|                    |
    |                | runAgent(agentId)  |                    |
    |                |-------------------------------------->|
    |                |                    |                    |
    |<-- RunResult --|                    |                    |
    |                |                    |                    |
```

---

## Microsoft Documentation

- **ConcurrentDictionary**: https://learn.microsoft.com/dotnet/api/system.collections.concurrent.concurrentdictionary-2
- **Task.WhenAll/WhenAny**: https://learn.microsoft.com/dotnet/api/system.threading.tasks.task.whenall
- **CancellationToken**: https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken
- **TaskCompletionSource**: https://learn.microsoft.com/dotnet/api/system.threading.tasks.taskcompletionsource-1
- **Channels**: https://learn.microsoft.com/dotnet/core/extensions/channels

---

## Expected Deliverables

1. **Result Delivery Service Implementation**

```csharp
public class ResultDeliveryService : IResultDeliveryService
{
    private readonly IAgentRepository _agentRepository;
    private readonly IItemRepository _itemRepository;
    private readonly IEventEmitter _eventEmitter;
    private readonly IAgentRunner _runner;
    private readonly WaitingRegistry _registry;

    public async Task<DeliveryResult> DeliverAsync(
        AgentId agentId,
        CallId callId,
        ToolResult result,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate agent exists and is waiting
        // 2. Store result as function_call_output
        // 3. Remove callId from waiting list
        // 4. Emit 'agent.resumed' event
        // 5. Resume execution if all waits complete
        // 6. Auto-propagate to parent if completed
    }
}
```

2. **Waiting Registry with Correlation**

```csharp
public interface IWaitingRegistry
{
    void Register(CallId callId, AgentId agentId, WaitingType type, string name);
    bool TryGetAgent(CallId callId, out AgentId agentId);
    void Unregister(CallId callId);
    IReadOnlyList<WaitingInfo> GetAgentWaits(AgentId agentId);
}
```

3. **Timeout Handling Strategy Document**

   - Per-call timeout vs aggregate timeout
   - Default timeout values (configurable)
   - Timeout error format and propagation
   - Cleanup of timed-out contexts

4. **Concurrent Coordination Tests**

   - Multiple parallel deliveries
   - Out-of-order delivery
   - Partial delivery scenarios
   - Race condition handling

---

## Validation Checklist

- [ ] Delivery flow documented
- [ ] Correlation mechanism designed
- [ ] Timeout handling implemented
- [ ] Concurrent access tested
- [ ] Parent propagation working
- [ ] Error cases handled

---

## Status

- [ ] Source code analyzed
- [ ] Delivery service interface defined
- [ ] Correlation registry implemented
- [ ] Timeout handling designed
- [ ] Concurrent coordination tested
- [ ] Auto-propagation implemented
