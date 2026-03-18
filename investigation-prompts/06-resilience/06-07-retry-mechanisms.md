# Investigation: Retry Mechanisms and Circuit Breaker

**Status**: Pending
**Priority**: High
**Category**: Resilience

---

## Source Files

### TypeScript Reference
- **Agent Runner**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts
- **Error Handling**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/errors/

Note: The 01_05_agent codebase has basic error handling. This investigation defines comprehensive retry and circuit breaker patterns.

---

## Microsoft Documentation

### Primary References
- **Polly Documentation**: https://www.thepollyproject.org/
- **Microsoft.Extensions.Resilience**: https://learn.microsoft.com/dotnet/api/microsoft.extensions.resilience
- **Resilience Strategies**: https://learn.microsoft.com/dotnet/core/resilience/
- **Health Checks**: https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks

### Related Patterns
- **Circuit Breaker**: Prevent cascading failures
- **Health Check Endpoints**: Monitor service health
- **Automatic Failover**: Switch to backup providers

---

## Investigation Questions

### 1. Retry Fundamentals
- What types of errors are retryable?
- How to implement exponential backoff with jitter?
- When to use fixed vs exponential backoff?
- How many retries are appropriate for LLM APIs?

### 2. Circuit Breaker Patterns
- What triggers circuit breaker to open?
- How long should circuit stay open?
- What is half-open state and how does it work?
- How to implement per-provider circuit breakers?

### 3. Health Check Integration
- How to expose health check endpoints?
- What metrics indicate healthy vs unhealthy state?
- How to integrate with ASP.NET Core health checks?
- How to implement custom health check logic?

### 4. Failover Strategies
- How to implement provider failover?
- What is the difference between active and passive failover?
- How to handle state during failover?
- When to alert on failover events?

### 5. Error Classification
- How to classify errors (transient vs permanent)?
- How to handle rate limit errors specifically?
- How to handle content filter errors?
- How to handle timeout errors?

### 6. .NET Implementation Patterns
- How to use Polly with ChatClientBuilder?
- How to implement custom resilience strategies?
- How to integrate with OpenTelemetry?
- How to expose circuit breaker state?

---

## Code Patterns to Analyze

### .NET Resilience Pipeline Configuration

```csharp
/// <summary>
/// Configures resilience pipelines for AI operations
/// </summary>
public interface IResiliencePipelineFactory
{
    /// <summary>
    /// Gets or creates a resilience pipeline for a provider
    /// </summary>
    ResiliencePipeline GetPipeline(string providerName);
}

public class ResiliencePipelineFactory : IResiliencePipelineFactory
{
    private readonly ConcurrentDictionary<string, ResiliencePipeline> _pipelines;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ResilienceOptions _options;

    public ResiliencePipelineFactory(
        ILoggerFactory loggerFactory,
        ResilienceOptions options)
    {
        _loggerFactory = loggerFactory;
        _options = options;
        _pipelines = new ConcurrentDictionary<string, ResiliencePipeline>();
    }

    public ResiliencePipeline GetPipeline(string providerName)
    {
        return _pipelines.GetOrAdd(providerName, name => BuildPipeline(name));
    }

    private ResiliencePipeline BuildPipeline(string providerName)
    {
        var builder = new ResiliencePipelineBuilder();

        // Add rate limit handling
        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
        {
            Name = $"{providerName}-rate-limit-retry",
            MaxRetryAttempts = _options.RateLimitMaxRetries,
            BackoffType = DelayBackoffType.Exponential,
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromMinutes(5),
            UseJitter = true,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests),
            OnRetry = args =>
            {
                var retryAfter = args.Outcome.Result?.Headers.RetryAfter;
                var logger = _loggerFactory.CreateLogger($"{providerName}-retry");
                logger.LogWarning(
                    "Rate limited. Attempt {Attempt}/{Max}. Retry-After: {RetryAfter}",
                    args.AttemptNumber, _options.RateLimitMaxRetries, retryAfter);
                return ValueTask.CompletedTask;
            }
        });

        // Add server error retry
        builder.AddRetry(new RetryStrategyOptions
        {
            Name = $"{providerName}-server-error-retry",
            MaxRetryAttempts = _options.ServerErrorMaxRetries,
            BackoffType = DelayBackoffType.Exponential,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(30),
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .Handle<TimeoutException>(),
            OnRetry = args =>
            {
                var logger = _loggerFactory.CreateLogger($"{providerName}-retry");
                logger.LogWarning(
                    "Server error. Attempt {Attempt}/{Max}. Exception: {Exception}",
                    args.AttemptNumber, _options.ServerErrorMaxRetries, args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });

        // Add circuit breaker
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            Name = $"{providerName}-circuit-breaker",
            FailureRatio = _options.CircuitBreakerFailureRatio,
            MinimumThroughput = _options.CircuitBreakerMinThroughput,
            SamplingDuration = _options.CircuitBreakerSamplingDuration,
            BreakDuration = _options.CircuitBreakerBreakDuration,
            OnOpened = args =>
            {
                var logger = _loggerFactory.CreateLogger($"{providerName}-circuit");
                logger.LogError(
                    "Circuit opened for {Provider}. Failure ratio: {Ratio}, Break duration: {Duration}",
                    providerName, args.FailureRate, _options.CircuitBreakerBreakDuration);
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                var logger = _loggerFactory.CreateLogger($"{providerName}-circuit");
                logger.LogInformation("Circuit closed for {Provider}", providerName);
                return ValueTask.CompletedTask;
            },
            OnHalfOpened = args =>
            {
                var logger = _loggerFactory.CreateLogger($"{providerName}-circuit");
                logger.LogInformation("Circuit half-opened for {Provider}", providerName);
                return ValueTask.CompletedTask;
            }
        });

        // Add timeout
        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Name = $"{providerName}-timeout",
            Timeout = _options.RequestTimeout,
            OnTimeout = args =>
            {
                var logger = _loggerFactory.CreateLogger($"{providerName}-timeout");
                logger.LogWarning("Request timed out after {Timeout}", _options.RequestTimeout);
                return ValueTask.CompletedTask;
            }
        });

        return builder.Build();
    }
}
```

### .NET Circuit Breaker State Monitoring

```csharp
public interface ICircuitBreakerMonitor
{
    CircuitBreakerState GetState(string providerName);
    IReadOnlyDictionary<string, CircuitBreakerState> GetAllStates();
}

public class CircuitBreakerState
{
    public string ProviderName { get; init; } = string.Empty;
    public CircuitState State { get; init; }
    public int FailureCount { get; init; }
    public int SuccessCount { get; init; }
    public DateTimeOffset? LastFailure { get; init; }
    public DateTimeOffset? LastStateChange { get; init; }
    public string? BlockedUntil { get; init; }
}

public enum CircuitState
{
    Closed,      // Normal operation
    Open,        // Requests blocked
    HalfOpen,    // Testing if recovered
    Isolated     // Manually isolated
}

public class CircuitBreakerHealthCheck : IHealthCheck
{
    private readonly ICircuitBreakerMonitor _monitor;
    private readonly ILogger<CircuitBreakerHealthCheck> _logger;

    public CircuitBreakerHealthCheck(
        ICircuitBreakerMonitor monitor,
        ILogger<CircuitBreakerHealthCheck> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var states = _monitor.GetAllStates();
        var openCircuits = states.Where(s => s.Value.State == CircuitState.Open).ToList();

        if (openCircuits.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy(
                "All circuits healthy",
                new Dictionary<string, object>
                {
                    ["circuit_count"] = states.Count,
                    ["all_healthy"] = true
                }));
        }

        var data = new Dictionary<string, object>
        {
            ["open_circuits"] = openCircuits.Select(c => c.ProviderName).ToArray(),
            ["circuit_count"] = states.Count
        };

        if (openCircuits.Count == states.Count)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "All circuits are open",
                data: data));
        }

        return Task.FromResult(HealthCheckResult.Degraded(
            $"{openCircuits.Count} circuit(s) open: {string.Join(", ", openCircuits.Select(c => c.ProviderName))}",
            data: data));
    }
}
```

### .NET Provider Failover

```csharp
public interface IProviderFailover
{
    Task<T> ExecuteWithFailoverAsync<T>(
        Func<string, CancellationToken, Task<T>> operation,
        CancellationToken ct = default);
}

public class ProviderFailover : IProviderFailover
{
    private readonly IReadOnlyList<string> _providers;
    private readonly ICircuitBreakerMonitor _circuitMonitor;
    private readonly IResiliencePipelineFactory _pipelineFactory;
    private readonly ILogger<ProviderFailover> _logger;

    public ProviderFailover(
        IEnumerable<string> providers,
        ICircuitBreakerMonitor circuitMonitor,
        IResiliencePipelineFactory pipelineFactory,
        ILogger<ProviderFailover> logger)
    {
        _providers = providers.ToList();
        _circuitMonitor = circuitMonitor;
        _pipelineFactory = pipelineFactory;
        _logger = logger;
    }

    public async Task<T> ExecuteWithFailoverAsync<T>(
        Func<string, CancellationToken, Task<T>> operation,
        CancellationToken ct = default)
    {
        var exceptions = new List<Exception>();

        foreach (var provider in GetAvailableProviders())
        {
            var pipeline = _pipelineFactory.GetPipeline(provider);

            try
            {
                var result = await pipeline.ExecuteAsync(
                    async ctx => await operation(provider, ctx),
                    ct);

                _logger.LogDebug("Operation succeeded on provider {Provider}", provider);
                return result;
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning(
                    "Circuit breaker open for {Provider}, trying next",
                    provider);
                exceptions.Add(ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Provider {Provider} failed, trying next",
                    provider);
                exceptions.Add(ex);
            }
        }

        throw new AggregateException(
            "All providers failed",
            exceptions);
    }

    private IEnumerable<string> GetAvailableProviders()
    {
        foreach (var provider in _providers)
        {
            var state = _circuitMonitor.GetState(provider);
            if (state.State != CircuitState.Open)
            {
                yield return provider;
            }
        }
    }
}
```

### .NET Error Classification

```csharp
public interface IErrorClassifier
{
    ErrorClassification Classify(Exception exception);
    ErrorClassification Classify(HttpStatusCode statusCode, string? content = null);
}

public record ErrorClassification(
    ErrorType Type,
    bool IsRetryable,
    TimeSpan? SuggestedDelay,
    string? Message
);

public enum ErrorType
{
    Transient,          // Network issues, timeouts
    RateLimited,        // Too many requests
    ServerError,        // 5xx errors
    ClientError,        // 4xx errors (except rate limit)
    ContentFiltered,    // Content policy violation
    Authentication,     // Auth/API key issues
    QuotaExceeded,      // Account quota limits
    ModelUnavailable,   // Model not available
    Unknown             // Unclassified
}

public class LlmErrorClassifier : IErrorClassifier
{
    public ErrorClassification Classify(Exception exception)
    {
        return exception switch
        {
            TaskCanceledException => new ErrorClassification(
                ErrorType.Transient, true, TimeSpan.FromSeconds(5),
                "Request timeout"),

            TimeoutException => new ErrorClassification(
                ErrorType.Transient, true, TimeSpan.FromSeconds(5),
                "Operation timeout"),

            HttpRequestException httpEx => ClassifyHttpException(httpEx),

            _ => new ErrorClassification(
                ErrorType.Unknown, false, null,
                $"Unknown error: {exception.Message}")
        };
    }

    public ErrorClassification Classify(HttpStatusCode statusCode, string? content = null)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => new ErrorClassification(
                ErrorType.RateLimited, true, ParseRetryAfter(content),
                "Rate limit exceeded"),

            HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout => new ErrorClassification(
                ErrorType.ServerError, true, TimeSpan.FromSeconds(10),
                $"Server error: {(int)statusCode}"),

            HttpStatusCode.Unauthorized => new ErrorClassification(
                ErrorType.Authentication, false, null,
                "Authentication failed"),

            HttpStatusCode.Forbidden => new ErrorClassification(
                ErrorType.Authentication, false, null,
                "Access forbidden"),

            HttpStatusCode.BadRequest => new ErrorClassification(
                ErrorType.ClientError, false, null,
                "Invalid request"),

            _ when (int)statusCode >= 500 => new ErrorClassification(
                ErrorType.ServerError, true, TimeSpan.FromSeconds(10),
                $"Server error: {(int)statusCode}"),

            _ when (int)statusCode >= 400 => new ErrorClassification(
                ErrorType.ClientError, false, null,
                $"Client error: {(int)statusCode}"),

            _ => new ErrorClassification(
                ErrorType.Unknown, false, null,
                $"Unknown status: {(int)statusCode}")
        };
    }

    private ErrorClassification ClassifyHttpException(HttpRequestException ex)
    {
        if (ex.StatusCode.HasValue)
        {
            return Classify(ex.StatusCode.Value, ex.Message);
        }

        return new ErrorClassification(
            ErrorType.Transient, true, TimeSpan.FromSeconds(5),
            "Network error");
    }

    private static TimeSpan? ParseRetryAfter(string? content)
    {
        // Try to parse retry-after from response
        // This would be more sophisticated in production
        if (string.IsNullOrEmpty(content))
            return TimeSpan.FromSeconds(60);

        // Look for patterns like "retry after 30 seconds"
        var match = System.Text.RegularExpressions.Regex.Match(
            content, @"(\d+)\s*(?:second|sec|s)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(60);
    }
}
```

### .NET Health Check Endpoints

```csharp
public static class HealthCheckExtensions
{
    public static IHealthChecksBuilder AddAiProviderHealthChecks(
        this IHealthChecksBuilder builder,
        IConfiguration configuration)
    {
        builder.AddCheck<CircuitBreakerHealthCheck>(
            "ai_circuit_breakers",
            tags: new[] { "ai", "ready" });

        builder.AddCheck<ProviderConnectivityHealthCheck>(
            "ai_provider_connectivity",
            tags: new[] { "ai", "live" });

        return builder;
    }
}

public class ProviderConnectivityHealthCheck : IHealthCheck
{
    private readonly IEnumerable<IProviderHealthCheck> _providerChecks;
    private readonly ILogger<ProviderConnectivityHealthCheck> _logger;

    public ProviderConnectivityHealthCheck(
        IEnumerable<IProviderHealthCheck> providerChecks,
        ILogger<ProviderConnectivityHealthCheck> logger)
    {
        _providerChecks = providerChecks;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HealthCheckResult>();
        var healthy = 0;
        var unhealthy = 0;

        foreach (var check in _providerChecks)
        {
            try
            {
                var result = await check.CheckHealthAsync(cancellationToken);
                results[check.ProviderName] = result;

                if (result.Status == HealthStatus.Healthy)
                    healthy++;
                else
                    unhealthy++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for {Provider}", check.ProviderName);
                results[check.ProviderName] = HealthCheckResult.Unhealthy(ex.Message);
                unhealthy++;
            }
        }

        var data = new Dictionary<string, object>
        {
            ["healthy_providers"] = healthy,
            ["unhealthy_providers"] = unhealthy,
            ["details"] = results.ToDictionary(r => r.Key, r => r.Value.Description ?? "OK")
        };

        if (healthy == 0)
        {
            return HealthCheckResult.Unhealthy("No healthy providers", data: data);
        }

        if (unhealthy > 0)
        {
            return HealthCheckResult.Degraded(
                $"{healthy} healthy, {unhealthy} unhealthy providers",
                data: data);
        }

        return HealthCheckResult.Healthy("All providers healthy", data);
    }
}

public interface IProviderHealthCheck
{
    string ProviderName { get; }
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken);
}
```

---

## Architecture Diagram: Resilience Flow

```
+------------+     +----------------+     +------------------+
|  Request   |---->| Error          |---->| Retry Strategy   |
+------------+     | Classifier     |     +------------------+
                   +----------------+              |
                          |                       v
                    +-----+-----+         +----------------+
                    | Is        |         | Exponential    |
                    | Retryable?|         | Backoff + Jitter|
                    +-----------+         +----------------+
                     |         |                   |
                    Yes        No                |
                     |         |                 |
                     v         v                 v
            +-------------+  +--------+  +----------------+
            | Retry       |  | Return |  | Circuit        |
            | Policy      |  | Error  |  | Breaker Check  |
            +-------------+  +--------+  +----------------+
                     |                         |
                     |         +---------------+
                     |         |
                     v         v
              +----------------+     +------------------+
              | Execute        |---->| Success/         |
              | Operation      |     | Failure          |
              +----------------+     +------------------+
                                              |
                          +-------------------+-------------------+
                          |                   |                   |
                          v                   v                   v
                   +-----------+       +-----------+       +-----------+
                   | Log       |       | Update    |       | Return    |
                   | Result    |       | Circuit   |       | Response  |
                   +-----------+       | State     |       +-----------+
                                       +-----------+
```

---

## Expected Deliverables

### 1. Core Interfaces

```csharp
public interface IResiliencePipelineFactory
{
    ResiliencePipeline GetPipeline(string providerName);
}

public interface ICircuitBreakerMonitor
{
    CircuitBreakerState GetState(string providerName);
    IReadOnlyDictionary<string, CircuitBreakerState> GetAllStates();
}

public interface IProviderFailover
{
    Task<T> ExecuteWithFailoverAsync<T>(
        Func<string, CancellationToken, Task<T>> operation,
        CancellationToken ct = default);
}

public interface IErrorClassifier
{
    ErrorClassification Classify(Exception exception);
    ErrorClassification Classify(HttpStatusCode statusCode, string? content = null);
}
```

### 2. Configuration

```csharp
public class ResilienceOptions
{
    public int RateLimitMaxRetries { get; set; } = 5;
    public int ServerErrorMaxRetries { get; set; } = 3;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(120);

    public double CircuitBreakerFailureRatio { get; set; } = 0.5;
    public int CircuitBreakerMinThroughput { get; set; } = 10;
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
```

### 3. Metrics
- Retry attempts by provider
- Circuit breaker state changes
- Error classification distribution
- Failover events
- Mean time to recovery

---

## Retry Strategy Comparison

| Strategy | Use Case | Pros | Cons |
|----------|----------|------|------|
| Fixed Backoff | Simple operations | Predictable | Can cause spikes |
| Linear Backoff | Gradual increase | Simple | Not optimal for rate limits |
| Exponential | Rate limits, 5xx | Efficient | Can take too long |
| Jittered | Production | Prevents thundering herd | More complex |

---

## Validation Checklist

- [ ] Resilience pipeline factory implemented
- [ ] Retry strategies configured
- [ ] Circuit breaker integrated
- [ ] Timeout handling working
- [ ] Error classifier implemented
- [ ] Provider failover working
- [ ] Health check endpoints exposed
- [ ] Metrics and logging configured

---

## Status

- [ ] TypeScript patterns analyzed
- [ ] .NET interfaces designed
- [ ] Resilience pipeline implemented
- [ ] Circuit breaker monitoring added
- [ ] Provider failover implemented
- [ ] Health checks integrated
- [ ] Error classification working
- [ ] Load testing performed
