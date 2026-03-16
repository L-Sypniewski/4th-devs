# Investigation: Retry Logic and Error Handling

**Status**: Pending
**Priority**: High
**Category**: Resilience

---

## Source Files

### TypeScript Reference
- [runner.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runner.ts) - Main execution loop
  - Look for retry logic in agent execution
  - Check error handling patterns
- [providers/](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/) - Provider implementations
  - Examine API error handling
  - Check for retry mechanisms

### Key TypeScript Patterns to Investigate
```typescript
// Look for patterns like:
// - Exponential backoff implementation
// - Error classification (transient vs permanent)
// - Retry counters and max retry limits
// - Circuit breaker patterns
```

---

## Microsoft Documentation

### Primary References
- [Microsoft.Extensions.Resilience](https://learn.microsoft.com/dotnet/api/microsoft.extensions.resilience)
- [Polly Documentation](https://www.thepollyproject.org/)
- [Resilience Strategies](https://learn.microsoft.com/dotnet/core/resilience/)

### Related Types
- `RetryStrategy` - Retry policy configuration
- `CircuitBreakerStrategy` - Circuit breaker pattern
- `HedgingRobot` - Hedging resilience strategy
- `ResiliencePipeline` - Composite resilience pipeline

---

## Investigation Questions

### 1. TypeScript Error Handling
- How does TS handle API errors and retries?
- What error types are considered retryable?
- Are there different retry strategies per error type?

### 2. Retry Strategies
- What retry strategies exist (exponential backoff, jitter)?
- How to implement jitter to avoid thundering herd?
- What are optimal retry counts and delays for LLM APIs?

### 3. HedgingRobot in .NET
- How to use HedgingRobot in .NET for resilience?
- When is hedging appropriate vs retry?
- How does hedging work with streaming responses?

### 4. Error Classification
- How to handle transient vs permanent failures?
- How to classify HTTP status codes (429, 500, 503)?
- How to handle content filter and moderation errors?

---

## Code Patterns

### Retry Policy with Exponential Backoff (Expected .NET)
```csharp
// Using Microsoft.Extensions.Resilience
var retryOptions = new RetryStrategyOptions
{
    MaxRetryAttempts = 3,
    BackoffType = DelayBackoffType.Exponential,
    BaseDelay = TimeSpan.FromSeconds(1),
    MaxDelay = TimeSpan.FromSeconds(30),
    UseJitter = true
};

// Using Polly directly
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .OrResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) +
            TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000)), // Jitter
        onRetry: (outcome, timeSpan, retryCount, context) =>
        {
            logger.LogWarning("Retry {RetryCount} after {Delay}s", retryCount, timeSpan.TotalSeconds);
        });
```

### Circuit Breaker Pattern (Expected .NET)
```csharp
var circuitBreaker = new CircuitBreakerStrategyOptions
{
    FailureRatio = 0.5,
    MinimumThroughput = 10,
    SamplingDuration = TimeSpan.FromSeconds(30),
    BreakDuration = TimeSpan.FromSeconds(30),
    OnOpened = args =>
    {
        logger.LogError("Circuit opened due to {Failures} failures", args.FailureCount);
        return ValueTask.CompletedTask;
    }
};
```

### Resilience Pipeline Composition (Expected .NET)
```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = new PredicateBuilder()
            .Handle<HttpRequestException>()
            .Handle<TimeoutException>()
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 10,
        SamplingDuration = TimeSpan.FromSeconds(30)
    })
    .AddTimeout(TimeSpan.FromSeconds(30))
    .Build();
```

### Error Classification (TypeScript Pattern)
```typescript
// Look for error classification patterns
function isRetryableError(error: unknown): boolean {
  if (error instanceof NetworkError) return true
  if (error instanceof RateLimitError) return true
  if (error instanceof TimeoutError) return true
  if (error instanceof ContentFilterError) return false // Never retry
  return false
}
```

---

## HTTP Status Code Classification

| Status Code | Category | Action |
|-------------|----------|--------|
| 400 | Permanent | Do not retry, log error |
| 401 | Permanent | Check credentials, do not retry |
| 403 | Permanent | Check permissions, do not retry |
| 404 | Permanent | Resource not found, do not retry |
| 429 | Transient | Retry with backoff, respect Retry-After |
| 500 | Transient | Retry with exponential backoff |
| 502 | Transient | Retry with exponential backoff |
| 503 | Transient | Retry with exponential backoff |
| 504 | Transient | Retry with exponential backoff |

---

## Expected Deliverables

1. **Resilience Pipeline Configuration**
   - Retry policy with exponential backoff and jitter
   - Circuit breaker for sustained failures
   - Timeout handling

2. **Error Classification System**
   - Provider-specific error types
   - Retryable vs non-retryable classification
   - Custom exception types

3. **ChatClientBuilder Integration**
   - Resilience middleware for chat client
   - Configurable resilience options
   - Logging and telemetry hooks

---

## Notes

- Different providers may require different retry configurations
- Content filter errors should never be retried (they are intentional blocks)
- Consider implementing a dead letter queue for failed requests
- Integration with health checks for circuit breaker state
