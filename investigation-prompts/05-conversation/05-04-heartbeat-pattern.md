# Investigation Prompt: Heartbeat Pattern and User Feedback

**Created**: 2026-03-17
**Focus**: Heartbeat pattern for long-running operations and progress feedback

---

## Source Files

Investigate the following TypeScript source files for heartbeat and progress patterns:

### Primary Files to Analyze

| File Path | Purpose |
|-----------|---------|
| `01_05_agent/src/runtime/runner.ts` | Main execution runner with streaming |
| `01_05_agent/src/runtime/streaming.ts` | Streaming response implementation |
| `01_05_agent/src/services/progress*` | Progress tracking services |
| Any files containing `stream`, `progress`, `heartbeat`, `status` | Related functionality |

### Search Patterns

```bash
# Find streaming-related files
find . -type f -name "*.ts" | xargs grep -l "stream\|Stream" | head -20

# Find progress/heartbeat patterns
grep -r "progress\|heartbeat\|status.*update" --include="*.ts" | head -20

# Find SSE/WebSocket implementations
grep -r "SSE\|EventSource\|WebSocket" --include="*.ts" | head -20

# Find async iterators/generators (streaming patterns)
grep -r "async.*\*\|yield\|AsyncIterable" --include="*.ts" | head -20
```

---

## Investigation Questions

### 1. What is a heartbeat pattern?

A heartbeat pattern sends periodic updates during long-running operations to:
- Keep users informed that work is in progress
- Provide feedback on operation status
- Prevent timeouts from occurring
- Enable progress indication in UI

**Key characteristics:**
- Periodic status messages during execution
- Incremental progress updates (0% -> 25% -> 50% -> etc.)
- Status information (current step, operation name, ETA)
- Continue-until-complete or interruptible streams

### 2. How does TS implement streaming responses?

| Approach | Description | Use Case |
|----------|-------------|----------|
| Server-Sent Events (SSE) | HTTP-based streaming, one-way | Server pushing updates to client |
| WebSocket | Full-duplex communication | Real-time bidirectional updates |
| Async Iterators | Generator-based streaming | Streaming data chunks |
| Polling | Client asks for status periodically | Simple implementations, firewalls |

### 3. What TypeScript patterns to look for?

#### Streaming Response Pattern

```typescript
// Look for patterns like:
async function* streamCompletion(
  prompt: string
): AsyncIterable<CompletionUpdate> {
  // How are chunks yielded?
  yield { type: 'progress', data: { percent: 10 } };
  yield { type: 'chunk', data: { text: 'Hello' } };
  yield { type: 'done', data: { fullResponse: 'Hello world' } };
}
```

#### Progress Callback Pattern

```typescript
// Look for patterns like:
interface ProgressOptions {
  onProgress?: (update: ProgressUpdate) => void;
  onStatus?: (status: string) => void;
}

async function longRunningOperation(
  options: ProgressOptions
): Promise<Result> {
  options.onProgress?.({ percent: 50, message: 'Processing...' });
  // ...
}
```

#### Event Emitter Pattern

```typescript
// Look for patterns like:
class OperationRunner extends EventEmitter {
  async run() {
    this.emit('progress', { percent: 25 });
    this.emit('status', 'Processing file...');
    this.emit('complete', { result });
  }
}
```

### 4. What C# patterns to implement?

```csharp
/// <summary>
/// Provides heartbeat/progress updates for long-running operations.
/// </summary>
public interface IHeartbeatProvider
{
    /// <summary>
    /// Streams updates as an async enumerable.
    /// </summary>
    IAsyncEnumerable<HeartbeatUpdate> StreamUpdates(
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a single progress update.
    /// </summary>
    Task SendProgressAsync(
        string operationId,
        int progress,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a status change update.
    /// </summary>
    Task SendStatusAsync(
        string operationId,
        string status,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a single heartbeat update.
/// </summary>
public record HeartbeatUpdate(
    string OperationId,
    HeartbeatType Type,
    int Progress,           // 0-100
    string Status,
    string? Message = null,
    DateTime? Timestamp = null
)
{
    public DateTime Timestamp { get; init; } = Timestamp ?? DateTime.UtcNow;
}

/// <summary>
/// Types of heartbeat updates.
/// </summary>
public enum HeartbeatType
{
    Started,      // Operation started
    Progress,     // Progress update (0-100%)
    Status,       // Status change message
    Warning,      // Non-fatal issue
    Completed,    // Operation completed successfully
    Failed        // Operation failed
}
```

---

## Implementation Approaches

### 1. Server-Sent Events (SSE)

**Pros:**
- Standard HTTP, no special infrastructure
- Automatic reconnection handling
- One-way streaming (server -> client)
- Built-in event types and data format

**Cons:**
- One-way only (client cannot send to server over same connection)
- Limited to text data

**C# Implementation:**

```csharp
// ASP.NET Core Endpoint
app.MapGet("/operations/{operationId}/stream", async (
    string operationId,
    IHeartbeatProvider heartbeatProvider,
    CancellationToken cancellationToken) =>
{
    return Results.Ok(new StreamedResult(
        async (output, ct) =>
        {
            await foreach (var update in heartbeatProvider.StreamUpdates(operationId, ct))
            {
                await output.WriteAsync($"data: {JsonSerializer.Serialize(update)}\n\n", ct);
                await output.FlushAsync(ct);
            }
        },
        "text/event-stream"));
});
```

**Client consumption:**

```typescript
const eventSource = new EventSource('/operations/123/stream');

eventSource.onmessage = (event) => {
  const update = JSON.parse(event.data);
  updateProgressBar(update.progress);
  updateStatus(update.message);
};
```

### 2. WebSocket-based

**Pros:**
- Full-duplex communication
- Lower latency than SSE
- Binary data support

**Cons:**
- More complex setup
- Requires WebSocket infrastructure
- Manual reconnection handling

**C# Implementation:**

```csharp
public class OperationWebSocketHandler : WebSocketHandler
{
    private readonly IHeartbeatProvider _heartbeatProvider;

    public override async Task OnConnectedAsync(WebSocket socket)
    {
        var operationId = GetOperationIdFromSocket(socket);
        _ = Task.Run(async () =>
        {
            await foreach (var update in _heartbeatProvider.StreamUpdates(operationId))
            {
                await SendAsync(socket, JsonSerializer.SerializeToBytes(update));
            }
        });
    }
}
```

### 3. Channel-based (In-Memory)

**Pros:**
- Simple in-process communication
- No network overhead
- Type-safe

**Cons:**
- Same process only
- Not suitable for distributed systems

**C# Implementation:**

```csharp
public class ChannelHeartbeatProvider : IHeartbeatProvider
{
    private readonly ConcurrentDictionary<string, Channel<HeartbeatUpdate>> _channels
        = new();

    public async IAsyncEnumerable<HeartbeatUpdate> StreamUpdates(
        string operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = _channels.GetOrAdd(operationId, _ =>
            Channel.CreateUnbounded<HeartbeatUpdate>());

        await foreach (var update in channel.ReadAllAsync(cancellationToken))
        {
            yield return update;
        }
    }

    public async Task SendProgressAsync(
        string operationId,
        int progress,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (_channels.TryGetValue(operationId, out var channel))
        {
            await channel.Writer.WriteAsync(
                new HeartbeatUpdate(operationId, HeartbeatType.Progress, progress, message),
                cancellationToken);
        }
    }
}
```

---

## Use Cases

### 1. Long-Running AI Completions

```csharp
public class StreamingCompletionService
{
    private readonly IHeartbeatProvider _heartbeat;

    public async Task<string> CompleteAsync(
        string prompt,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        await _heartbeat.SendStatusAsync(operationId, "Initializing...");

        var progress = 0;
        await _heartbeat.SendProgressAsync(operationId, progress, "Starting completion");

        // Simulate streaming
        var response = new StringBuilder();
        await foreach (var chunk in _llmClient.StreamCompleteAsync(prompt, cancellationToken))
        {
            response.Append(chunk);
            progress = Math.Min(100, progress + 10);

            await _heartbeat.SendProgressAsync(
                operationId,
                progress,
                "Generating response..."
            );
        }

        await _heartbeat.SendProgressAsync(operationId, 100, "Complete");

        return response.ToString();
    }
}
```

### 2. File Processing

```csharp
public class FileProcessingService
{
    private readonly IHeartbeatProvider _heartbeat;

    public async Task<ProcessResult> ProcessFileAsync(
        string filePath,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        await _heartbeat.SendStatusAsync(operationId, "Reading file...");

        var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
        var totalLines = lines.Length;
        var processedLines = 0;

        foreach (var line in lines)
        {
            // Process line
            await ProcessLineAsync(line, cancellationToken);

            processedLines++;
            var progress = (int)((processedLines / (double)totalLines) * 100);

            await _heartbeat.SendProgressAsync(
                operationId,
                progress,
                $"Processed {processedLines}/{totalLines} lines"
            );
        }

        await _heartbeat.SendStatusAsync(operationId, "Complete");

        return new ProcessResult(processedLines);
    }
}
```

### 3. Multi-Step Workflows

```csharp
public class WorkflowRunner
{
    private readonly IHeartbeatProvider _heartbeat;

    public async Task RunWorkflowAsync(
        WorkflowDefinition workflow,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var totalSteps = workflow.Steps.Count;
        var currentStep = 0;

        foreach (var step in workflow.Steps)
        {
            currentStep++;
            var stepProgress = (int)((currentStep / (double)totalSteps) * 100);

            await _heartbeat.SendStatusAsync(
                operationId,
                $"Running step {currentStep}: {step.Name}"
            );

            await _heartbeat.SendProgressAsync(
                operationId,
                stepProgress,
                $"Executing {step.Name}"
            );

            await step.ExecuteAsync(cancellationToken);
        }

        await _heartbeat.SendProgressAsync(operationId, 100, "Workflow complete");
    }
}
```

---

## Code Patterns to Investigate

### Async Iterator Pattern

```typescript
// Look for patterns like:
async function* executeWithHeartbeat(
  operation: Operation
): AsyncGenerator<HeartbeatUpdate, Result, unknown> {
  yield { type: 'started', operationId: operation.id };

  for (const step of operation.steps) {
    yield { type: 'progress', percent: step.progress, status: step.name };
    await step.execute();
  }

  yield { type: 'completed', result: operation.result };
}
```

### Progress Decorator Pattern

```typescript
// Look for patterns like:
function withProgress<T>(
  fn: () => Promise<T>,
  onProgress: (p: number) => void
): Promise<T> {
  return new Promise((resolve, reject) => {
    onProgress(0);
    fn()
      .then(result => {
        onProgress(100);
        resolve(result);
      })
      .catch(reject);
  });
}
```

### Observable Pattern

```typescript
// Look for patterns like:
class ObservableOperation<T> {
  private progressSubject = new Subject<ProgressUpdate>();

  readonly progress$ = this.progressSubject.asObservable();

  async execute(): Promise<T> {
    // Emit progress updates
    this.progressSubject.next({ percent: 50 });
    // ...
  }
}
```

---

## Expected Deliverables

### 1. Heartbeat Provider Interface

```csharp
public interface IHeartbeatProvider
{
    IAsyncEnumerable<HeartbeatUpdate> StreamUpdates(
        string operationId,
        CancellationToken cancellationToken = default);

    Task SendProgressAsync(
        string operationId,
        int progress,
        string message,
        CancellationToken cancellationToken = default);

    Task SendStatusAsync(
        string operationId,
        string status,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        string operationId,
        string? result = null,
        CancellationToken cancellationToken = default);

    Task FailAsync(
        string operationId,
        string error,
        CancellationToken cancellationToken = default);
}
```

### 2. Heartbeat Update Models

```csharp
public record HeartbeatUpdate(
    string OperationId,
    HeartbeatType Type,
    int Progress,
    string Status,
    string? Message = null,
    DateTime? Timestamp = null,
    object? Data = null
)
{
    public DateTime Timestamp { get; init; } = Timestamp ?? DateTime.UtcNow;
}

public enum HeartbeatType
{
    Started,
    Progress,
    Status,
    Warning,
    Completed,
    Failed
}

public record ProgressUpdate(
    int PercentComplete,
    string CurrentStep,
    TimeSpan? EstimatedTimeRemaining = null
);
```

### 3. In-Memory Implementation

```csharp
public class InMemoryHeartbeatProvider : IHeartbeatProvider
{
    private readonly ConcurrentDictionary<string, Channel<HeartbeatUpdate>> _channels
        = new();

    public async IAsyncEnumerable<HeartbeatUpdate> StreamUpdates(
        string operationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = _channels.GetOrAdd(operationId,
            _ => Channel.CreateUnbounded<HeartbeatUpdate>());

        try
        {
            await foreach (var update in channel.ReadAllAsync(cancellationToken))
            {
                yield return update;
            }
        }
        finally
        {
            _channels.TryRemove(operationId, out _);
        }
    }

    public async Task SendProgressAsync(
        string operationId,
        int progress,
        string message,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(operationId,
            new HeartbeatUpdate(operationId, HeartbeatType.Progress, progress, "In Progress", message),
            cancellationToken);
    }

    public async Task SendStatusAsync(
        string operationId,
        string status,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(operationId,
            new HeartbeatUpdate(operationId, HeartbeatType.Status, 0, status),
            cancellationToken);
    }

    public async Task CompleteAsync(
        string operationId,
        string? result = null,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(operationId,
            new HeartbeatUpdate(operationId, HeartbeatType.Completed, 100, "Complete", result),
            cancellationToken);

        // Close the channel
        if (_channels.TryGetValue(operationId, out var channel))
        {
            channel.Writer.Complete();
        }
    }

    public async Task FailAsync(
        string operationId,
        string error,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(operationId,
            new HeartbeatUpdate(operationId, HeartbeatType.Failed, 0, "Failed", error),
            cancellationToken);

        if (_channels.TryGetValue(operationId, out var channel))
        {
            channel.Writer.Complete();
        }
    }

    private async Task SendAsync(
        string operationId,
        HeartbeatUpdate update,
        CancellationToken cancellationToken)
    {
        if (_channels.TryGetValue(operationId, out var channel))
        {
            await channel.Writer.WriteAsync(update, cancellationToken);
        }
    }
}
```

### 4. SSE Endpoint for ASP.NET Core

```csharp
public static class HeartbeatEndpoints
{
    public static void MapHeartbeatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/operations/{operationId}/stream",
            async (string operationId, IHeartbeatProvider heartbeat, CancellationToken ct) =>
            {
                return new ServerSentEventsResult(async (output, ct) =>
                {
                    await foreach (var update in heartbeat.StreamUpdates(operationId, ct))
                    {
                        await output.WriteAsync($"event: {update.Type}\n", ct);
                        await output.WriteAsync($"data: {JsonSerializer.Serialize(update)}\n\n", ct);
                        await output.FlushAsync(ct);
                    }
                });
            });
    }
}

// Helper class for SSE responses
public class ServerSentEventsResult : IResult
{
    private readonly Func<Stream, CancellationToken, Task> _writeEvent;

    public ServerSentEventsResult(Func<Stream, CancellationToken, Task> writeEvent)
    {
        _writeEvent = writeEvent;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";

        await _writeEvent(response.Body, httpContext.RequestAborted);
    }
}
```

### 5. Progress Decorator Helper

```csharp
public static class HeartbeatExtensions
{
    public static async Task<T> WithProgressAsync<T>(
        this IHeartbeatProvider heartbeat,
        string operationId,
        Func<IProgressReporter, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        var reporter = new ProgressReporter(heartbeat, operationId);

        try
        {
            var result = await operation(reporter);
            await heartbeat.CompleteAsync(operationId, cancellationToken: cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            await heartbeat.FailAsync(operationId, ex.Message, cancellationToken);
            throw;
        }
    }
}

public interface IProgressReporter
{
    Task ReportProgressAsync(int percent, string? message = null);
    Task ReportStatusAsync(string status);
    Task ReportWarningAsync(string warning);
}

public class ProgressReporter : IProgressReporter
{
    private readonly IHeartbeatProvider _heartbeat;
    private readonly string _operationId;

    public ProgressReporter(IHeartbeatProvider heartbeat, string operationId)
    {
        _heartbeat = heartbeat;
        _operationId = operationId;
    }

    public Task ReportProgressAsync(int percent, string? message = null)
    {
        return _heartbeat.SendProgressAsync(_operationId, percent, message ?? "Processing...");
    }

    public Task ReportStatusAsync(string status)
    {
        return _heartbeat.SendStatusAsync(_operationId, status);
    }

    public Task ReportWarningAsync(string warning)
    {
        return _heartbeat.SendStatusAsync(_operationId, $"Warning: {warning}");
    }
}
```

### 6. Usage Example

```csharp
// In a service or controller
public class CompletionService
{
    private readonly IHeartbeatProvider _heartbeat;
    private readonly ILlmClient _llm;

    public async Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid().ToString();

        return await _heartbeat.WithProgressAsync(operationId, async progress =>
        {
            await progress.ReportStatusAsync("Initializing LLM client");

            var response = new StringBuilder();
            var totalTokens = 0;

            await foreach (var chunk in _llm.StreamCompleteAsync(prompt, cancellationToken))
            {
                response.Append(chunk.Text);
                totalTokens += chunk.TokenCount;

                // Estimate progress (this is illustrative)
                var estimatedProgress = Math.Min(95, totalTokens / 10);
                await progress.ReportProgressAsync(estimatedProgress, "Generating response");
            }

            await progress.ReportProgressAsync(100, "Complete");
            return response.ToString();
        }, cancellationToken);
    }
}
```

---

## Investigation Tasks

- [ ] Locate TypeScript streaming/heartbeat implementation
- [ ] Document how async generators are used for streaming
- [ ] Identify progress update patterns
- [ ] Note any SSE or WebSocket implementations
- [ ] Document heartbeat update structure/format
- [ ] Design .NET heartbeat provider interface
- [ ] Implement in-memory channel-based provider
- [ ] Create SSE endpoint for web clients
- [ ] Implement progress decorator helper
- [ ] Add unit tests for heartbeat provider
- [ ] Add integration tests for SSE streaming
- [ ] Document client consumption patterns

---

## Notes

- Consider using `System.Threading.Channels` for in-memory streaming
- SSE is simpler than WebSocket for one-way server-to-client communication
- Always include operation IDs to correlate updates with operations
- Consider heartbeat timeout detection for dead operations
- Progress percentage should be monotonic (never decrease)
- Consider adding ETA calculation based on progress rate
- For distributed scenarios, consider using Redis Streams or message bus
- Always handle cancellation tokens properly
- Consider rate limiting progress updates for very fast operations
