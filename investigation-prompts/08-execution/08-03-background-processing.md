# Investigation: Background Processing Patterns for AI Agents

## Objective
Investigate background processing patterns for AI agents in TypeScript and design .NET equivalents using IHostedService, Channels, and job queue implementations (Hangfire, MassTransit, Azure Service Bus).

---

## Source Files

- **Agent Runtime**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
- **Agent Domain**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/domain/agent.ts

---

## Investigation Questions

1. **Fire-and-Forget Task Patterns**
   - How does TypeScript handle fire-and-forget operations in the agent runtime?
   - Are there any background task patterns in the existing codebase?
   - How are async operations spawned without awaiting completion?
   - What error handling exists for unobserved task failures?

2. **Job Queue Implementations**
   - Are there any queue implementations in the TypeScript codebase?
   - How are multiple concurrent operations managed?
   - What patterns exist for job enqueuing and processing?
   - How is job priority handled?

3. **Long-Running Operations**
   - How does the runtime handle operations that take minutes or hours?
   - Are there any timeout or cancellation patterns?
   - How is progress communicated for long-running tasks?
   - What happens when a process restarts during a long operation?

4. **Status Tracking and Progress Reporting**
   - How are job statuses tracked (pending, running, completed, failed)?
   - What progress reporting mechanisms exist?
   - How do clients poll for or receive status updates?
   - Is there any persistence of job state?

5. **TypeScript Async Operation Handling**
   - How does `01_05_agent` handle async operations?
   - Are there any in-memory queue implementations?
   - How is job status tracked across async boundaries?
   - What patterns exist for concurrent execution limits?

---

## Code Patterns to Analyze

### TypeScript Fire-and-Forget Pattern

```typescript
// Investigate if this pattern exists
export async function runAgentAsync(
  agentId: AgentId,
  runtime: RuntimeContext
): Promise<void> {
  // Fire-and-forget execution?
  const runPromise = executeAgent(agentId, runtime);
  // Does the code store this promise somewhere?
}
```

**Analysis Points:**
- Does the codebase spawn tasks without awaiting?
- How are unobserved exceptions handled?
- Is there any task tracking mechanism?

### .NET IHostedService Background Worker

```csharp
// C# Pattern Template - Background worker for agent processing
public class AgentBackgroundWorker : BackgroundService
{
    private readonly IBackgroundJobQueue<AgentJob> _queue;
    private readonly IAgentRunner _runner;
    private readonly ILogger<AgentBackgroundWorker> _logger;
    private readonly IAgentStateStore _stateStore;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.DequeueAsync(stoppingToken))
        {
            try
            {
                await _stateStore.UpdateStatusAsync(job.JobId, JobState.Running);

                var result = await _runner.RunAgentAsync(job.AgentId, stoppingToken);

                await _stateStore.UpdateStatusAsync(job.JobId, JobState.Completed);
            }
            catch (OperationCanceledException)
            {
                await _stateStore.UpdateStatusAsync(job.JobId, JobState.Cancelled);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed", job.JobId);
                await _stateStore.UpdateStatusAsync(job.JobId, JobState.Failed, ex.Message);
            }
        }
    }
}
```

### .NET Channel-based Job Queue

```csharp
// C# Pattern Template - In-memory job queue
public interface IBackgroundJobQueue<TJob>
{
    Task<string> EnqueueAsync(TJob job, CancellationToken ct = default);
    IAsyncEnumerable<TJob> DequeueAsync(CancellationToken ct = default);
    Task<JobStatus> GetStatusAsync(string jobId, CancellationToken ct = default);
    Task CancelAsync(string jobId, CancellationToken ct = default);
}

public class BackgroundJobQueue<TJob> : IBackgroundJobQueue<TJob>
{
    private readonly Channel<QueuedJob<TJob>> _channel;
    private readonly ConcurrentDictionary<string, JobState> _jobStates;
    private readonly IJobStateStore _stateStore;

    public BackgroundJobQueue(IJobStateStore stateStore, int capacity = 1000)
    {
        _stateStore = stateStore;
        _jobStates = new();
        _channel = Channel.CreateBounded<QueuedJob<TJob>>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task<string> EnqueueAsync(TJob job, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString();
        var status = new JobStatus(jobId, JobState.Pending, 0, null);

        await _stateStore.SaveAsync(status, ct);
        _jobStates[jobId] = JobState.Pending;

        await _channel.Writer.WriteAsync(new QueuedJob<TJob>(jobId, job), ct);
        return jobId;
    }

    public async IAsyncEnumerable<TJob> DequeueAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var queuedJob in _channel.Reader.ReadAllAsync(ct))
        {
            _jobStates[queuedJob.JobId] = JobState.Running;
            yield return queuedJob.Job;
        }
    }

    public Task<JobStatus> GetStatusAsync(string jobId, CancellationToken ct = default)
        => _stateStore.GetAsync(jobId, ct);

    public Task CancelAsync(string jobId, CancellationToken ct = default)
    {
        _jobStates[jobId] = JobState.Cancelled;
        return _stateStore.UpdateStatusAsync(jobId, JobState.Cancelled);
    }
}

public record JobStatus(string Id, JobState State, int Progress, string? Error);

public enum JobState { Pending, Running, Completed, Failed, Cancelled }

public record QueuedJob<TJob>(string JobId, TJob Job);
```

### .NET Hangfire Integration

```csharp
// C# Pattern Template - Hangfire background job
public static class AgentJobExtensions
{
    public static async Task<string> EnqueueAgentJobAsync(
        this IBackgroundJobClient client,
        AgentId agentId,
        CancellationToken ct = default)
    {
        var jobId = client.Enqueue<AgentJobExecutor>(x => x.ExecuteAsync(agentId, JobC.Null));
        return jobId;
    }

    public static async Task<string> ScheduleAgentJobAsync(
        this IBackgroundJobClient client,
        AgentId agentId,
        DateTimeOffset scheduleAt,
        CancellationToken ct = default)
    {
        var jobId = client.Schedule<AgentJobExecutor>(
            x => x.ExecuteAsync(agentId, JobC.Null),
            scheduleAt);
        return jobId;
    }

    public static async Task<bool> CancelJobAsync(
        this IBackgroundJobClient client,
        string jobId,
        CancellationToken ct = default)
    {
        return client.ChangeState(jobId, new DeletedState());
    }

    public static Task<JobStatus?> GetJobStatusAsync(
        this IBackgroundJobClient client,
        string jobId,
        CancellationToken ct = default)
    {
        var connection = JobStorage.Current.GetConnection();
        var stateData = connection.GetStateData(jobId);

        if (stateData == null) return Task.FromResult<JobStatus?>(null);

        var state = stateData.Name switch
        {
            "Enqueued" => JobState.Pending,
            "Processing" => JobState.Running,
            "Succeeded" => JobState.Completed,
            "Failed" => JobState.Failed,
            "Deleted" => JobState.Cancelled,
            _ => JobState.Pending
        };

        return Task.FromResult<JobStatus?>(new JobStatus(jobId, state, 0, null));
    }
}

public class AgentJobExecutor
{
    private readonly IAgentRunner _runner;
    private readonly IAgentStateStore _stateStore;

    [DisableConcurrentExecution(timeoutInSeconds: 3600)]
    public async Task ExecuteAsync(AgentId agentId, IJobCancellationToken cancellationToken)
    {
        await _runner.RunAgentAsync(agentId, cancellationToken.ShutdownToken);
    }
}
```

### .NET MassTransit Integration

```csharp
// C# Pattern Template - MassTransit job consumer
public record AgentJobMessage(string JobId, AgentId AgentId, DateTimeOffset CreatedAt);

public class AgentJobConsumer : IConsumer<AgentJobMessage>
{
    private readonly IAgentRunner _runner;
    private readonly IJobStateStore _stateStore;
    private readonly ILogger<AgentJobConsumer> _logger;

    public async Task Consume(ConsumeContext<AgentJobMessage> context)
    {
        var message = context.Message;
        await _stateStore.UpdateStatusAsync(message.JobId, JobState.Running);

        try
        {
            var result = await _runner.RunAgentAsync(message.AgentId, context.CancellationToken);
            await _stateStore.UpdateStatusAsync(message.JobId, JobState.Completed);

            await context.Publish(new AgentJobCompletedEvent(message.JobId, result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", message.JobId);
            await _stateStore.UpdateStatusAsync(message.JobId, JobState.Failed, ex.Message);
            await context.Publish(new AgentJobFailedEvent(message.JobId, ex.Message));
            throw;
        }
    }
}

public record AgentJobCompletedEvent(string JobId, AgentRunResult Result);
public record AgentJobFailedEvent(string JobId, string Error);
```

### .NET Progress Reporting Pattern

```csharp
// C# Pattern Template - Progress reporting
public interface IProgressReporter
{
    Task ReportProgressAsync(string jobId, int progress, string? message = null, CancellationToken ct = default);
    IAsyncEnumerable<ProgressUpdate> SubscribeAsync(string jobId, CancellationToken ct = default);
}

public record ProgressUpdate(string JobId, int Progress, string? Message, DateTimeOffset UpdatedAt);

public class InMemoryProgressReporter : IProgressReporter
{
    private readonly ConcurrentDictionary<string, List<ProgressUpdate>> _updates = new();
    private readonly ConcurrentDictionary<string, Channel<ProgressUpdate>> _channels = new();

    public async Task ReportProgressAsync(string jobId, int progress, string? message = null, CancellationToken ct = default)
    {
        var update = new ProgressUpdate(jobId, progress, message, DateTimeOffset.UtcNow);
        _updates.AddOrUpdate(jobId,
            _ => new() { update },
            (_, list) => { list.Add(update); return list; });

        if (_channels.TryGetValue(jobId, out var channel))
        {
            await channel.Writer.WriteAsync(update, ct);
        }
    }

    public async IAsyncEnumerable<ProgressUpdate> SubscribeAsync(
        string jobId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = _channels.GetOrAdd(jobId, _ =>
            Channel.CreateUnbounded<ProgressUpdate>());

        await foreach (var update in channel.Reader.ReadAllAsync(ct))
        {
            yield return update;
        }
    }
}
```

---

## Architecture Diagram: Background Processing

```
+-----------+     +-------------+     +----------------+     +----------------+
|  Client   |---->| Job API     |---->| Job Queue      |---->| Background      |
+-----------+     +-------------+     | (In-Memory/    |     | Workers (xN)    |
                      |               |  External MQ)  |     +----------------+
                      |               +----------------+             |
                      v                      |                      v
                +-------------+             |               +----------------+
                | Job Status  |<------------+               | Agent Runner   |
                | Store       |<----------------------------+                |
                +-------------+     +----------------+     +----------------+
                                     | Progress       |---->| Client (SSE/   |
                                     | Reporter       |     |  WebSocket)    |
                                     +----------------+     +----------------+
```

---

## Microsoft Documentation

- **IHostedService**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice
- **BackgroundService**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.backgroundservice
- **Channels**: https://learn.microsoft.com/dotnet/core/extensions/channels
- **Hangfire**: https://docs.hangfire.io/en/latest/
- **MassTransit**: https://masstransit-project.com/
- **Azure Service Bus**: https://learn.microsoft.com/azure/service-bus-messaging/
- **Durable Functions**: https://learn.microsoft.com/azure/azure-functions/durable/

---

## Expected Deliverables

1. **Background Job Queue Interface**

```csharp
public interface IBackgroundJobQueue<TJob>
{
    /// <summary>
    /// Enqueues a job for background processing
    /// </summary>
    Task<string> EnqueueAsync(TJob job, CancellationToken ct = default);

    /// <summary>
    /// Schedules a job to run at a specific time
    /// </summary>
    Task<string> ScheduleAsync(TJob job, DateTimeOffset scheduleAt, CancellationToken ct = default);

    /// <summary>
    /// Gets the current status of a job
    /// </summary>
    Task<JobStatus> GetStatusAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Cancels a pending or running job
    /// </summary>
    Task CancelAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Lists all jobs with optional filtering
    /// </summary>
    Task<IReadOnlyList<JobStatus>> ListJobsAsync(
        JobState? state = null,
        int? limit = null,
        CancellationToken ct = default);
}

public record JobStatus(
    string Id,
    JobState State,
    int Progress,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

public enum JobState { Pending, Scheduled, Running, Completed, Failed, Cancelled }
```

2. **Background Worker Service**

```csharp
public interface IBackgroundWorker<TJob>
{
    Task ExecuteAsync(TJob job, string jobId, CancellationToken cancellationToken);
}
```

3. **Job State Persistence Interface**

```csharp
public interface IJobStateStore
{
    Task SaveAsync(JobStatus status, CancellationToken ct = default);
    Task<JobStatus?> GetAsync(string jobId, CancellationToken ct = default);
    Task UpdateStatusAsync(string jobId, JobState state, string? error = null, CancellationToken ct = default);
    Task UpdateProgressAsync(string jobId, int progress, CancellationToken ct = default);
    Task DeleteAsync(string jobId, CancellationToken ct = default);
}
```

4. **Implementation Strategy Document** covering:
   - In-memory vs distributed queue selection criteria
   - Hangfire vs MassTransit vs Azure Service Bus comparison
   - Scaling strategies for multiple workers
   - Fault tolerance and retry policies
   - Job timeout and cancellation handling

---

## Use Cases for AI Agents

1. **Long-Running Document Processing**
   - PDF analysis with OCR
   - Batch document summarization
   - Multi-file analysis pipelines

2. **Batch Operations**
   - Bulk data processing
   - Batch tool calls across datasets
   - Periodic sync operations

3. **Scheduled Tasks**
   - Daily report generation
   - Scheduled data refresh
   - Recurring agent workflows

4. **Interactive Background Tasks**
   - Agent continues thinking while user provides input
   - Parallel research with progress updates
   - Streaming result delivery

---

## Validation Checklist

- [ ] TypeScript async patterns analyzed
- [ ] Queue interface defined
- [ ] IHostedService worker implemented
- [ ] Progress reporting working
- [ ] Job persistence designed
- [ ] Cancellation handling verified
- [ ] Multiple workers tested
- [ ] Integration with Hangfire/MassTransit documented

---

## Integration with Message Queuing

> **See Also**: `08-execution/08-05-message-queuing.md` for detailed message queue patterns

### Queue-Driven Background Processing

Background processing integrates with message queues for scalable, reliable job execution:

```
+------------+     +----------------+     +------------------+
|  Client    |---->| Message Queue  |---->| Background       |
|  Request   |     | (Priority)     |     | Workers (xN)     |
+------------+     +----------------+     +------------------+
                          |                       |
                          v                       v
                   +-------------+         +-------------+
                   | Job State   |<------->| Agent       |
                   | Store       |         | Runner      |
                   +-------------+         +-------------+
```

### Queue Integration Pattern

```csharp
public class QueueDrivenBackgroundProcessor : BackgroundService
{
    private readonly IMessageQueue<AgentJob> _queue;
    private readonly IAgentRunner _runner;
    private readonly IJobStateStore _stateStore;
    private readonly ILogger<QueueDrivenBackgroundProcessor> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue-driven processor starting");

        await foreach (var job in _queue.DequeueAsync(stoppingToken))
        {
            try
            {
                await _stateStore.UpdateStatusAsync(job.MessageId, JobState.Running);
                await _runner.RunAgentAsync(job.Message.AgentId, stoppingToken);
                await _stateStore.UpdateStatusAsync(job.MessageId, JobState.Completed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed", job.MessageId);
                await _stateStore.UpdateStatusAsync(job.MessageId, JobState.Failed, ex.Message);
            }
        }
    }
}
```

---

## Status

- [ ] Source code analyzed
- [ ] Background patterns documented
- [ ] Job queue interface designed
- [ ] Worker service implemented
- [ ] Progress tracking working
- [ ] Integration tests written
- [ ] External MQ integration documented
- [ ] Message queue integration added (see 08-05-message-queuing.md)
