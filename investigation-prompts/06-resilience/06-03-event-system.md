# Investigation: Event System

**Status**: Pending
**Priority**: Medium
**Category**: Resilience

---

## Source Files

### TypeScript Reference
- [runner.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runner.ts) - Main execution loop
  - Look for event emission patterns
  - Check for state change notifications
  - Examine tool call event handling

### Key TypeScript Patterns to Investigate
```typescript
// Look for patterns like:
// - Event emitter patterns
// - State change callbacks
// - Tool call/result events
// - Error event handling
// - Progress/streaming events
```

---

## Microsoft Documentation

### Primary References
- [IObservable Interface](https://learn.microsoft.com/dotnet/api/system.iobservable-1)
- [Event Pattern in .NET](https://learn.microsoft.com/dotnet/standard/events/)
- [System.Threading.Channels](https://learn.microsoft.com/dotnet/api/system.threading.channels)

### Agent Framework References
- Look for middleware patterns in Agent Framework
- Check for hooks/lifecycle events
- Examine observability patterns

### Related Types
- `IObservable<T>` - Reactive event streams
- `IObserver<T>` - Event subscription
- `Event<T>` - Traditional .NET events
- `Channel<T>` - Async producer/consumer

---

## Investigation Questions

### 1. TypeScript Event Emission
- How does TS emit events (state changes, tool calls, errors)?
- What event types are emitted?
- How are events consumed/subscribed to?

### 2. .NET Event Patterns
- What .NET patterns exist for events (IObservable, EventPattern)?
- When to use IObservable vs traditional events?
- How to handle async event handlers?

### 3. Agent Framework Integration
- How to integrate with Agent Framework middleware?
- What hooks/lifecycle events are available?
- How to build custom middleware for events?

### 4. Async Event Handling
- How to support both sync and async event handlers?
- How to handle exceptions in event handlers?
- How to ensure event ordering?

---

## Code Patterns

### IObservable Pattern (Expected .NET)
```csharp
public class AgentEvent
{
    public string EventType { get; init; }
    public DateTime Timestamp { get; init; }
    public object? Data { get; init; }
}

public interface IAgentEventEmitter
{
    IObservable<AgentEvent> Events { get; }
}

public class AgentEventEmitter : IAgentEventEmitter
{
    private readonly Subject<AgentEvent> _events = new();

    public IObservable<AgentEvent> Events => _events.AsObservable();

    public void Emit(AgentEvent @event)
    {
        _events.OnNext(@event);
    }
}
```

### Traditional Event Pattern (Expected .NET)
```csharp
public class AgentEventArgs : EventArgs
{
    public string EventType { get; init; } = string.Empty;
    public object? Data { get; init; }
}

public event EventHandler<AgentEventArgs>? AgentEvent;

protected virtual void OnAgentEvent(AgentEventArgs e)
{
    AgentEvent?.Invoke(this, e);
}
```

### Channel-Based Events (Expected .NET)
```csharp
public class AgentEventChannel
{
    private readonly Channel<AgentEvent> _channel =
        Channel.CreateUnbounded<AgentEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

    public ValueTask WriteAsync(AgentEvent @event, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(@event, ct);

    public IAsyncEnumerable<AgentEvent> ReadAllAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAllAsync(ct);
}
```

### TypeScript Event Pattern (Investigation Target)
```typescript
// Look for patterns like:
interface AgentEvent {
  type: 'state_change' | 'tool_call' | 'tool_result' | 'error' | 'stream'
  timestamp: number
  data: unknown
}

type EventCallback = (event: AgentEvent) => void

class EventEmitter {
  private listeners: Map<string, Set<EventCallback>> = new Map()

  on(eventType: string, callback: EventCallback): () => void {
    // Subscribe and return unsubscribe function
  }

  emit(event: AgentEvent): void {
    // Emit to all listeners
  }
}
```

### Agent Framework Middleware Integration (Expected .NET)
```csharp
public class EventEmittingMiddleware : DelegatingChatClient
{
    private readonly IAgentEventEmitter _eventEmitter;

    public EventEmittingMiddleware(IChatClient innerClient, IAgentEventEmitter eventEmitter)
        : base(innerClient)
    {
        _eventEmitter = eventEmitter;
    }

    public override async Task<ChatResponse> CompleteAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _eventEmitter.Emit(new AgentEvent
        {
            EventType = "completion_started",
            Timestamp = DateTime.UtcNow
        });

        try
        {
            var response = await base.CompleteAsync(messages, options, cancellationToken);

            _eventEmitter.Emit(new AgentEvent
            {
                EventType = "completion_completed",
                Timestamp = DateTime.UtcNow,
                Data = response
            });

            return response;
        }
        catch (Exception ex)
        {
            _eventEmitter.Emit(new AgentEvent
            {
                EventType = "completion_failed",
                Timestamp = DateTime.UtcNow,
                Data = ex.Message
            });
            throw;
        }
    }
}
```

---

## Event Types

| Event Type | Description | Data |
|------------|-------------|------|
| `state_change` | Agent state transition | `{ from, to }` |
| `tool_call` | Tool invocation started | `{ tool, arguments }` |
| `tool_result` | Tool execution completed | `{ tool, result }` |
| `stream_start` | Streaming response started | `{ }` |
| `stream_chunk` | Streaming chunk received | `{ content }` |
| `stream_end` | Streaming response completed | `{ }` |
| `error` | Error occurred | `{ error, recoverable }` |
| `rate_limited` | Rate limit hit | `{ retryAfter }` |

---

## Expected Deliverables

1. **Event System Using IObservable or Channels**
   - Event emitter implementation
   - Type-safe event definitions
   - Subscription management

2. **Agent Framework Middleware**
   - Event-emitting chat client decorator
   - Lifecycle event hooks
   - Error event handling

3. **Integration with OpenTelemetry**
   - Event-to-span conversion
   - Event metrics
   - Distributed tracing support

---

## Notes

- Consider using `System.Reactive` (Rx.NET) for advanced event handling
- Channels are better for producer/consumer scenarios
- Traditional events may be simpler for basic use cases
- Ensure thread safety in event handling
- Consider event buffering for high-frequency events
