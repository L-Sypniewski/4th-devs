# Investigation: Rate Limiting

**Status**: Pending
**Priority**: High
**Category**: Resilience

---

## Source Files

### TypeScript Reference
- [providers/](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/) - Provider implementations
  - Look for rate limiting logic in provider files
  - Check for API call throttling patterns
  - Examine header handling for rate limit information

### Key TypeScript Patterns to Investigate
```typescript
// Look for patterns like:
// - Rate limit header parsing (x-ratelimit-*, retry-after)
// - Request queuing/throttling
// - Backoff strategies when rate limited
```

---

## Microsoft Documentation

### Primary References
- [.NET Resilience](https://learn.microsoft.com/dotnet/core/resilience/)
- [Polly Rate Limiting](https://www.thepollyproject.org/)
- [System.Threading.RateLimiting](https://learn.microsoft.com/dotnet/api/system.threading.ratelimiting)

### Related Types
- `RateLimiter` - Base class for rate limiters
- `TokenBucketRateLimiter` - Token bucket algorithm
- `SlidingWindowRateLimiter` - Sliding window algorithm
- `ConcurrencyLimiter` - Concurrency-based limiting

---

## Investigation Questions

### 1. TypeScript Rate Limiting Implementation
- How does TS implement rate limiting for API calls?
- Are there any built-in throttling mechanisms?
- How are rate limit headers from providers handled?

### 2. .NET Resilience Options
- What .NET resilience options exist (Polly, Microsoft.Extensions.Resilience)?
- How does `Microsoft.Extensions.Resilience` compare to Polly?
- What built-in rate limiting abstractions exist?

### 3. Rate Limiting Algorithms
- How to implement token bucket rate limiting?
- How to implement sliding window rate limiting?
- When to use fixed window vs sliding window?

### 4. Provider Rate Limit Handling
- How to handle rate limit headers from providers?
- How to parse `x-ratelimit-*` and `retry-after` headers?
- How to dynamically adjust rate limits based on responses?

---

## Code Patterns

### Token Bucket Rate Limiter (Expected .NET)
```csharp
// Using System.Threading.RateLimiting
var rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 100,
    TokensPerPeriod = 10,
    ReplenishmentPeriod = TimeSpan.FromSeconds(1)
});

// Using with Polly
var rateLimitPolicy = Policy.RateLimitAsync(100, TimeSpan.FromSeconds(1));
```

### Rate Limit Header Handling (TypeScript Pattern)
```typescript
// Look for header parsing patterns
interface RateLimitInfo {
  limit: number
  remaining: number
  reset: number
}

function parseRateLimitHeaders(headers: Headers): RateLimitInfo {
  return {
    limit: parseInt(headers.get('x-ratelimit-limit') || '0'),
    remaining: parseInt(headers.get('x-ratelimit-remaining') || '0'),
    reset: parseInt(headers.get('x-ratelimit-reset') || '0')
  }
}
```

### Integration with ChatClientBuilder (Expected .NET)
```csharp
services.AddChatClient(builder =>
{
    builder.UseRateLimiting(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 100,
        TokensPerPeriod = 10,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1)
    });
    builder.UseRetry();
});
```

---

## Algorithm Comparison

| Algorithm | Use Case | Pros | Cons |
|-----------|----------|------|------|
| Token Bucket | Burst-friendly APIs | Allows bursts, smooth average | Complex to tune |
| Sliding Window | Smooth rate limiting | No burst at boundaries | More memory |
| Fixed Window | Simple rate limiting | Easy to implement | Burst at boundaries |
| Concurrency | Limit parallel requests | Prevents overload | Not time-based |

---

## Expected Deliverables

1. **Rate Limiting Middleware**
   - Polly-based rate limiting policy
   - Integration with `ChatClientBuilder`
   - Configurable rate limit options

2. **Rate Limit Header Parser**
   - Provider-specific header handling
   - Dynamic rate limit adjustment
   - Retry-after support

3. **Configuration Schema**
   - Rate limit settings per provider
   - Fallback behavior configuration
   - Monitoring and logging hooks

---

## Notes

- Different providers have different rate limit behaviors
- Consider per-provider rate limit configurations
- May need to handle tiered rate limits (requests/min vs requests/day)
- Integration with OpenTelemetry for rate limit metrics
