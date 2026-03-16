# Investigation: Rate Limiting

**Status**: Pending
**Priority**: High
**Category**: Resilience

---

## Source Files

### Existing Implementation
- [`01_05_agent/src/middleware/rate-limit.ts`](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/middleware/rate-limit.ts) - Fixed-window rate limiter
  - Fixed-window implementation (60-second window)
  - Header handling patterns (X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset)
  - Retry-After header on limit exceeded
  - Memory cleanup with setInterval pattern

### Key TypeScript Patterns to Investigate
```typescript
// Current implementation uses:
// - Fixed-window rate limiting (60-second window)
// - Map-based in-memory storage keyed by user ID
// - Periodic cleanup with setInterval (60s intervals)
// - Standard rate limit headers (X-RateLimit-*)
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

### 1. Existing TypeScript Implementation Analysis
- How does the current fixed-window implementation work (01_05_agent/src/middleware/rate-limit.ts)?
- What are the trade-offs of fixed-window vs sliding-window for this use case?
- How does the memory cleanup pattern with setInterval prevent memory leaks?
- Why is `.unref()` called on the cleanup interval timer?

### 2. Header Response Patterns
- What headers are set on each request (X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset)?
- How is Retry-After calculated when limit exceeded?
- How do clients parse and respect these headers?

### 3. .NET Resilience Options
- What .NET resilience options exist (Polly, Microsoft.Extensions.Resilience)?
- How does `Microsoft.Extensions.Resilience` compare to Polly?
- What built-in rate limiting abstractions exist?

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

### Existing TypeScript Implementation (Fixed-Window)
```typescript
// From 01_05_agent/src/middleware/rate-limit.ts
interface Window {
  count: number
  resetAt: number
}

const windows = new Map<string, Window>()

const CLEANUP_INTERVAL_MS = 60_000

// Periodically prune expired windows to prevent memory leak
setInterval(() => {
  const now = Date.now()
  for (const [key, window] of windows) {
    if (now >= window.resetAt) {
      windows.delete(key)
    }
  }
}, CLEANUP_INTERVAL_MS).unref()

// Fixed-window rate limiter keyed by authenticated user ID
export const rateLimiter = createMiddleware<Env>(async (ctx, next) => {
  const user = ctx.get('user')
  const key = user.id
  const now = Date.now()
  const windowMs = 60_000 // 1-minute window
  const limit = config.rateLimitRpm

  let window = windows.get(key)

  if (!window || now >= window.resetAt) {
    window = { count: 0, resetAt: now + windowMs }
    windows.set(key, window)
  }

  window.count++

  const remaining = Math.max(0, limit - window.count)
  const resetSeconds = Math.ceil((window.resetAt - now) / 1000)

  ctx.header('X-RateLimit-Limit', String(limit))
  ctx.header('X-RateLimit-Remaining', String(remaining))
  ctx.header('X-RateLimit-Reset', String(resetSeconds))

  if (window.count > limit) {
    ctx.header('Retry-After', String(resetSeconds))
    throw err.rateLimited(`Rate limit exceeded. Try again in ${resetSeconds}s`)
  }

  await next()
})
```

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

### Existing (TypeScript)
1. **Rate Limiting Middleware** (`01_05_agent/src/middleware/rate-limit.ts`)
   - Fixed-window rate limiting (60-second window)
   - User-keyed in-memory storage with Map
   - Standard rate limit headers (X-RateLimit-*, Retry-After)
   - Memory cleanup with setInterval pattern

### Pending (.NET Migration)
2. **Rate Limiting Middleware**
   - Polly-based rate limiting policy
   - Integration with `ChatClientBuilder`
   - Configurable rate limit options

3. **Rate Limit Header Parser**
   - Provider-specific header handling
   - Dynamic rate limit adjustment
   - Retry-after support

4. **Configuration Schema**
   - Rate limit settings per provider
   - Fallback behavior configuration
   - Monitoring and logging hooks

---

## Notes

- Different providers have different rate limit behaviors
- Consider per-provider rate limit configurations
- May need to handle tiered rate limits (requests/min vs requests/day)
- Integration with OpenTelemetry for rate limit metrics
