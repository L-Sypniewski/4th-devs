# Investigation: OpenTelemetry Integration

**Status**: Pending
**Priority**: Medium
**Category**: Resilience

---

## Microsoft Documentation

### Primary References
- [OpenTelemetryChatClient](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.opentelemetrychatclient)
- [Agent Framework Observability](https://learn.microsoft.com/agent-framework/agents/observability)
- [OpenTelemetry .NET](https://learn.microsoft.com/dotnet/core/diagnostics/distributed-tracing)

### Related Types
- `OpenTelemetryChatClient` - Telemetry middleware
- `Activity` - Span representation
- `Meter` - Metrics collection
- `TracerProvider` - Tracing configuration

---

## Investigation Questions

### 1. OpenTelemetryChatClient Middleware
- How does `OpenTelemetryChatClient` middleware work?
- What telemetry does it automatically collect?
- How to configure and customize it?

### 2. Automatic Telemetry Collection
- What spans are automatically created?
- What metrics are collected?
- How are attributes and tags added?

### 3. Custom Instrumentation
- How to add custom spans and metrics?
- How to correlate spans with agent operations?
- How to add semantic conventions?

### 4. Telemetry Export
- How to export telemetry (OTLP, Prometheus, etc.)?
- How to integrate with Aspire's observability?
- How to configure sampling and filtering?

---

## Code Patterns

### OpenTelemetryChatClient Setup (Expected .NET)
```csharp
// Basic setup
services.AddChatClient(builder =>
{
    builder.UseOpenTelemetry();
    // ... other middleware
});

// With configuration
services.AddChatClient(builder =>
{
    builder.UseOpenTelemetry(new OpenTelemetryChatClientOptions
    {
        EnableSensitiveDataLogging = false,
        SourceName = "MyAgent"
    });
});
```

### Custom Span Creation (Expected .NET)
```csharp
using var activity = ActivitySource.StartActivity("tool_invocation");
activity?.SetTag("tool.name", toolName);
activity?.SetTag("tool.arguments", JsonSerializer.Serialize(arguments));

try
{
    var result = await tool.ExecuteAsync(arguments);

    activity?.SetTag("tool.result_size", result?.ToString()?.Length ?? 0);
    activity?.SetStatus(ActivityStatusCode.Ok);

    return result;
}
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
    {
        { "exception.type", ex.GetType().FullName },
        { "exception.message", ex.Message },
        { "exception.stacktrace", ex.StackTrace }
    }));
    throw;
}
```

### Custom Metrics (Expected .NET)
```csharp
public class AgentMetrics
{
    private readonly Meter _meter;
    private readonly Counter<long> _completionCounter;
    private readonly Histogram<double> _latencyHistogram;
    private readonly Counter<long> _tokenCounter;

    public AgentMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create("MyAgent");

        _completionCounter = _meter.CreateCounter<long>("agent.completions");
        _latencyHistogram = _meter.CreateHistogram<double>("agent.latency", "ms");
        _tokenCounter = _meter.CreateCounter<long>("agent.tokens");
    }

    public void RecordCompletion(string provider, string model, long tokens, double latencyMs)
    {
        _completionCounter.Add(1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("model", model));

        _tokenCounter.Add(tokens,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("model", model));

        _latencyHistogram.Record(latencyMs,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("model", model));
    }
}
```

### OpenTelemetry Service Configuration (Expected .NET)
```csharp
// Program.cs or Startup.cs
services.AddOpenTelemetry()
    .WithTracing(builder =>
    {
        builder
            .AddSource("Microsoft.Extensions.AI")
            .AddSource("MyAgent")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://otel-collector:4317");
            });
    })
    .WithMetrics(builder =>
    {
        builder
            .AddMeter("Microsoft.Extensions.AI")
            .AddMeter("MyAgent")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddPrometheusExporter();
    });
```

### Aspire Integration (Expected .NET)
```csharp
// In AppHost
var builder = DistributedApplication.CreateBuilder(args);

var otlpEndpoint = builder.AddConnectionString("Otlp");

builder.Services.AddOpenTelemetry()
    .UseOtlpExporter();

// In Service project
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Microsoft.Extensions.AI")
        .AddSource("MyAgent"))
    .WithMetrics(metrics => metrics
        .AddMeter("Microsoft.Extensions.AI")
        .AddMeter("MyAgent"));
```

---

## Telemetry Data Points

### Spans (Traces)
| Span Name | Description | Attributes |
|-----------|-------------|------------|
| `chat.completion` | Chat completion request | `provider`, `model`, `tokens` |
| `chat.streaming` | Streaming completion | `provider`, `model` |
| `tool.invoke` | Tool execution | `tool.name`, `duration` |
| `mcp.call` | MCP server call | `server`, `method` |

### Metrics
| Metric Name | Type | Description |
|-------------|------|-------------|
| `agent.completions` | Counter | Total completions |
| `agent.tokens` | Counter | Total tokens used |
| `agent.latency` | Histogram | Completion latency |
| `agent.errors` | Counter | Error count by type |
| `agent.rate_limited` | Counter | Rate limit hits |

---

## Expected Deliverables

1. **OpenTelemetry Setup with Aspire Integration**
   - Tracing configuration
   - Metrics configuration
   - OTLP exporter setup

2. **Custom Instrumentation**
   - Agent-specific spans
   - Custom metrics
   - Semantic conventions

3. **Dashboard and Monitoring**
   - Grafana dashboard configuration
   - Alert rules
   - Performance baselines

---

## Notes

- OpenTelemetry is the standard for observability in .NET
- Aspire provides built-in OpenTelemetry support
- Consider using Azure Monitor for production
- Sample high-volume telemetry to reduce costs
- Ensure PII is not logged in telemetry
