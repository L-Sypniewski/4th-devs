# Investigation: Message Queuing and Multithreading

**Status**: Pending
**Priority**: High
**Category**: Execution

---

## Source Files

### TypeScript Reference
- **Agent Runtime**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
- **Session Manager**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/session/manager.ts

Note: The 01_05_agent codebase currently processes requests synchronously. This investigation defines patterns for adding message queuing and concurrent session support.

---

## Microsoft Documentation

### Primary References
- **System.Threading.Channels**: https://learn.microsoft.com/dotnet/core/extensions/channels
- **BackgroundService**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice
- **ThreadPool**: https://learn.microsoft.com/dotnet/api/system.threading.threadpool
- **Parallel Programming**: https://learn.microsoft.com/dotnet/standard/parallel-programming/

### External Libraries
- **Hangfire**: https://docs.hangfire.io/
- **MassTransit**: https://masstransit-project.com/
- **Azure Service Bus**: https://learn.microsoft.com/azure/service-bus-messaging/

---

## Investigation Questions

### 1. Message Queue Fundamentals
- Why is message queuing needed for AI agent systems?
- What are the benefits of async processing for LLM operations?
- How does queuing improve user experience for long-running operations?
- When should you use in-memory vs distributed queues?

### 2. Queue Patterns
- **Request Queue**: How to queue incoming agent requests?
- **Priority Queuing**: How to prioritize certain users or request types?
- **Dead Letter Queue**: How to handle failed messages?
- **Delayed Processing**: How to schedule future executions?

### 3. Concurrency Management
- How to limit concurrent LLM API calls per provider?
- How to manage thread pools for agent execution?
- What patterns exist for graceful degradation under load?
- How to prevent resource exhaustion?

### 4. Session Isolation
- How to ensure session data integrity during concurrent access?
- What locking strategies work for session repositories?
- How to handle session timeouts in queue?
- What about session affinity for distributed workers?

### 5. .NET Implementation Patterns
- How to implement `IMessageQueue<T>` interface?
- How to use `Channel<T>` for producer-consumer patterns?
- How to integrate with `IHostedService`?
- How to implement priority with `PriorityQueue<TElement, TPriority>`?

### 6. Scaling Considerations
- When to scale horizontally vs vertically?
- How to implement worker scaling with queue depth?
- What metrics indicate scaling needs?
- How to handle backpressure?

---

## Code Patterns to Analyze

### .NET Message Queue Interface

```csharp
/// <summary>
/// Generic message queue for agent operations
/// </summary>
public interface IMessageQueue<TMessage>
{
    /// <summary>
    /// Enqueues a message for processing
    /// </summary>
    Task<string> EnqueueAsync(TMessage message, CancellationToken ct = default);

    /// <summary>
    /// Enqueues a message with priority
    /// </summary>
    Task<string> EnqueueAsync(TMessage message, int priority, CancellationToken ct = default);

    /// <summary>
    /// Dequeues messages for processing
    /// </summary>
    IAsyncEnumerable<QueuedMessage<TMessage>> DequeueAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current queue depth
    /// </summary>
    Task<int> GetQueueDepthAsync(CancellationToken ct = default);

    /// <summary>
    /// Attempts to cancel a queued message
    /// </summary>
    Task<bool> TryCancelAsync(string messageId, CancellationToken ct = default);
}

public record QueuedMessage<TMessage>(
    string MessageId,
    TMessage Message,
    int Priority,
    DateTimeOffset EnqueuedAt
);
```

### .NET Channel-based In-Memory Queue

```csharp
public class InMemoryMessageQueue<TMessage> : IMessageQueue<TMessage>
{
    private readonly Channel<QueuedMessage<TMessage>> _channel;
    private readonly ConcurrentDictionary<string, QueuedMessage<TMessage>> _pending;
    private readonly ILogger<InMemoryMessageQueue<TMessage>> _logger;
    private int _counter;

    public InMemoryMessageQueue(
        int capacity = 1000,
        ILogger<InMemoryMessageQueue<TMessage>> logger)
    {
        _logger = logger;
        _pending = new ConcurrentDictionary<string, QueuedMessage<TMessage>>();
        _channel = Channel.CreateBounded<QueuedMessage<TMessage>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public async Task<string> EnqueueAsync(TMessage message, CancellationToken ct = default)
    {
        return await EnqueueAsync(message, priority: 0, ct);
    }

    public async Task<string> EnqueueAsync(TMessage message, int priority, CancellationToken ct = default)
    {
        var messageId = $"msg_{Interlocked.Increment(ref _counter)}";
        var queued = new QueuedMessage<TMessage>(
            messageId,
            message,
            priority,
            DateTimeOffset.UtcNow
        );

        _pending[messageId] = queued;
        await _channel.Writer.WriteAsync(queued, ct);

        _logger.LogDebug("Enqueued message {MessageId} with priority {Priority}",
            messageId, priority);

        return messageId;
    }

    public async IAsyncEnumerable<QueuedMessage<TMessage>> DequeueAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(ct))
        {
            _pending.TryRemove(message.MessageId, out _);
            yield return message;
        }
    }

    public Task<int> GetQueueDepthAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_channel.Reader.Count);
    }

    public Task<bool> TryCancelAsync(string messageId, CancellationToken ct = default)
    {
        // Note: Already dequeued messages cannot be cancelled
        var removed = _pending.TryRemove(messageId, out _);
        return Task.FromResult(removed);
    }
}
```

### .NET Priority Queue Implementation

```csharp
public class PriorityMessageQueue<TMessage> : IMessageQueue<TMessage>
{
    private readonly PriorityQueue<QueuedMessage<TMessage>, (int Priority, DateTimeOffset EnqueuedAt)> _queue;
    private readonly SemaphoreSlim _signal = new(0);
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, QueuedMessage<TMessage>> _pending;
    private int _counter;

    public PriorityMessageQueue()
    {
        _queue = new PriorityQueue<QueuedMessage<TMessage>, (int, DateTimeOffset)>();
        _pending = new ConcurrentDictionary<string, QueuedMessage<TMessage>>();
    }

    public Task<string> EnqueueAsync(TMessage message, int priority = 0, CancellationToken ct = default)
    {
        var messageId = $"msg_{Interlocked.Increment(ref _counter)}";
        var queued = new QueuedMessage<TMessage>(messageId, message, priority, DateTimeOffset.UtcNow);

        lock (_lock)
        {
            _pending[messageId] = queued;
            // Lower priority value = higher priority (process first)
            _queue.Enqueue(queued, (priority, queued.EnqueuedAt));
        }

        _signal.Release();
        return Task.FromResult(messageId);
    }

    public async IAsyncEnumerable<QueuedMessage<TMessage>> DequeueAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            await _signal.WaitAsync(ct);

            QueuedMessage<TMessage>? message = null;
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    message = _queue.Dequeue();
                    _pending.TryRemove(message.MessageId, out _);
                }
            }

            if (message is not null)
            {
                yield return message;
            }
        }
    }

    public Task<int> GetQueueDepthAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_queue.Count);
        }
    }

    public Task<bool> TryCancelAsync(string messageId, CancellationToken ct = default)
    {
        // Mark for removal (actual removal happens during dequeue)
        return Task.FromResult(_pending.TryRemove(messageId, out _));
    }
}
```

### .NET Queue Processor Background Service

```csharp
public class MessageQueueProcessor<TMessage> : BackgroundService
{
    private readonly IMessageQueue<TMessage> _queue;
    private readonly IMessageHandler<TMessage> _handler;
    private readonly IConcurrencyLimiter _limiter;
    private readonly ILogger<MessageQueueProcessor<TMessage>> _logger;
    private readonly int _maxConcurrentHandlers;

    public MessageQueueProcessor(
        IMessageQueue<TMessage> queue,
        IMessageHandler<TMessage> handler,
        IConcurrencyLimiter limiter,
        ILogger<MessageQueueProcessor<TMessage>> logger,
        int maxConcurrentHandlers = 10)
    {
        _queue = queue;
        _handler = handler;
        _limiter = limiter;
        _logger = logger;
        _maxConcurrentHandlers = maxConcurrentHandlers;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message queue processor started");

        var tasks = new List<Task>();

        await foreach (var message in _queue.DequeueAsync(stoppingToken))
        {
            // Wait for a slot to become available
            await _limiter.WaitAsync(stoppingToken);

            var task = ProcessMessageAsync(message, stoppingToken);
            tasks.Add(task);

            // Clean up completed tasks
            tasks.RemoveAll(t => t.IsCompleted);
        }

        // Wait for remaining tasks
        await Task.WhenAll(tasks);
    }

    private async Task ProcessMessageAsync(QueuedMessage<TMessage> message, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Processing message {MessageId}", message.MessageId);
            await _handler.HandleAsync(message.Message, ct);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Message {MessageId} was cancelled", message.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
        }
        finally
        {
            _limiter.Release();
        }
    }
}

public interface IMessageHandler<TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken ct);
}

public interface IConcurrencyLimiter
{
    Task WaitAsync(CancellationToken ct);
    void Release();
}

public class SemaphoreConcurrencyLimiter : IConcurrencyLimiter
{
    private readonly SemaphoreSlim _semaphore;

    public SemaphoreConcurrencyLimiter(int maxConcurrent)
    {
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public Task WaitAsync(CancellationToken ct) => _semaphore.WaitAsync(ct);
    public void Release() => _semaphore.Release();
}
```

### .NET Session-Aware Queue with Locking

```csharp
public class SessionAwareMessageQueue<TMessage> : IMessageQueue<TMessage>
    where TMessage : ISessionMessage
{
    private readonly IMessageQueue<TMessage> _innerQueue;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _sessionLocks;
    private readonly ILogger<SessionAwareMessageQueue<TMessage>> _logger;

    public SessionAwareMessageQueue(
        IMessageQueue<TMessage> innerQueue,
        ILogger<SessionAwareMessageQueue<TMessage>> logger)
    {
        _innerQueue = innerQueue;
        _sessionLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
        _logger = logger;
    }

    public async Task<string> EnqueueAsync(TMessage message, CancellationToken ct = default)
    {
        return await EnqueueAsync(message, priority: 0, ct);
    }

    public async Task<string> EnqueueAsync(TMessage message, int priority, CancellationToken ct = default)
    {
        // Ensure session lock exists
        var sessionLock = _sessionLocks.GetOrAdd(
            message.SessionId,
            _ => new SemaphoreSlim(1, 1)
        );

        var messageId = await _innerQueue.EnqueueAsync(message, priority, ct);
        _logger.LogDebug("Enqueued message {MessageId} for session {SessionId}",
            messageId, message.SessionId);

        return messageId;
    }

    public async IAsyncEnumerable<QueuedMessage<TMessage>> DequeueAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var queued in _innerQueue.DequeueAsync(ct))
        {
            var sessionLock = _sessionLocks.GetOrAdd(
                queued.Message.SessionId,
                _ => new SemaphoreSlim(1, 1)
            );

            // Wait for session lock before yielding
            await sessionLock.WaitAsync(ct);
            try
            {
                yield return queued;
            }
            finally
            {
                sessionLock.Release();
            }
        }
    }

    public Task<int> GetQueueDepthAsync(CancellationToken ct = default)
        => _innerQueue.GetQueueDepthAsync(ct);

    public Task<bool> TryCancelAsync(string messageId, CancellationToken ct = default)
        => _innerQueue.TryCancelAsync(messageId, ct);
}

public interface ISessionMessage
{
    string SessionId { get; }
}
```

---

## Architecture Diagram: Message Queue Flow

```
+------------+     +----------------+     +-----------------+
|  Client    |---->| API Endpoint   |---->| Message Queue   |
+------------+     +----------------+     | (In-Memory/     |
                          |               |  Distributed)   |
                          v               +-----------------+
                   +-------------+              |
                   | Queue Depth |<-------------+
                   | Metrics     |
                   +-------------+
                                        |
                    +-------------------+-------------------+
                    |                   |                   |
                    v                   v                   v
             +-----------+       +-----------+       +-----------+
             | Worker 1  |       | Worker 2  |       | Worker N  |
             +-----------+       +-----------+       +-----------+
                    |                   |                   |
                    +-------------------+-------------------+
                                        |
                                        v
                              +-------------------+
                              | Agent Runner      |
                              | (per session)     |
                              +-------------------+
                                        |
                                        v
                              +-------------------+
                              | Session Store     |
                              | (with locking)    |
                              +-------------------+
```

---

## Expected Deliverables

### 1. Core Interfaces

```csharp
public interface IMessageQueue<TMessage>
{
    Task<string> EnqueueAsync(TMessage message, CancellationToken ct = default);
    Task<string> EnqueueAsync(TMessage message, int priority, CancellationToken ct = default);
    IAsyncEnumerable<QueuedMessage<TMessage>> DequeueAsync(CancellationToken ct = default);
    Task<int> GetQueueDepthAsync(CancellationToken ct = default);
    Task<bool> TryCancelAsync(string messageId, CancellationToken ct = default);
}

public interface IMessageHandler<TMessage>
{
    Task HandleAsync(TMessage message, CancellationToken ct);
}

public interface IConcurrencyLimiter
{
    Task WaitAsync(CancellationToken ct);
    void Release();
}
```

### 2. Implementations
- In-memory channel-based queue
- Priority queue with ordering
- Session-aware queue with locking
- Background service processor

### 3. Configuration Schema

```csharp
public class MessageQueueOptions
{
    public int MaxQueueCapacity { get; set; } = 1000;
    public int MaxConcurrentHandlers { get; set; } = 10;
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public bool EnablePriorityQueue { get; set; } = false;
    public bool EnableSessionLocking { get; set; } = true;
}
```

### 4. Metrics and Monitoring
- Queue depth monitoring
- Processing latency metrics
- Worker utilization tracking
- Error rate tracking

---

## Use Cases for AI Agents

1. **Concurrent Session Support**
   - Multiple users chatting simultaneously
   - Session isolation guarantees
   - Fair resource allocation

2. **Long-Running Operations**
   - Document analysis pipelines
   - Multi-step tool execution
   - Background research tasks

3. **Priority Handling**
   - VIP user prioritization
   - Urgent request escalation
   - Time-sensitive operations

4. **Rate Limit Buffering**
   - Smooth out API rate limits
   - Provider-specific queue management
   - Backpressure handling

---

## Validation Checklist

- [ ] In-memory queue implemented
- [ ] Priority queue pattern implemented
- [ ] Background service processor created
- [ ] Session locking pattern documented
- [ ] Concurrency limiter implemented
- [ ] Queue depth metrics exposed
- [ ] Graceful shutdown handled
- [ ] Dead letter queue pattern defined

---

## Status

- [ ] TypeScript patterns analyzed
- [ ] .NET interfaces designed
- [ ] In-memory implementation created
- [ ] Background service implemented
- [ ] Session locking added
- [ ] Metrics integration complete
- [ ] Load testing performed
