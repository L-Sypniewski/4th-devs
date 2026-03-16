# Investigation: Authentication Middleware

**Status**: Pending
**Priority**: High
**Category**: Resilience / Security

---

## Source Files

### TypeScript Reference
- [middleware/auth.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/middleware/auth.ts) - Bearer token authentication
- [domain/user.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/domain/user.ts) - User entity
- [repositories/sqlite/index.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/repositories/sqlite/index.ts) - User repository implementation

### Key TypeScript Patterns to Investigate
```typescript
// Bearer token parsing
const authHeader = ctx.req.header('Authorization')
const apiKey = authHeader.slice(7) // Remove 'Bearer ' prefix

// SHA-256 API key hashing using Web Crypto API
async function hashApiKey(apiKey: string): Promise<string> {
  const encoder = new TextEncoder()
  const data = encoder.encode(apiKey)
  const hashBuffer = await crypto.subtle.digest('SHA-256', data)
  const hashArray = Array.from(new Uint8Array(hashBuffer))
  return hashArray.map(b => b.toString(16).padStart(2, '0')).join('')
}

// Repository-based user lookup
const user = await runtime.repositories.users.getByApiKeyHash(apiKeyHash)
```

---

## Microsoft Documentation

### Primary References
- [ASP.NET Core Authentication](https://learn.microsoft.com/aspnet/core/security/authentication/)
- [Authentication Middleware in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/)
- [API Key Authentication](https://learn.microsoft.com/aspnet/core/security/api-key-authorization)
- [SHA-256 Hashing in .NET](https://learn.microsoft.com/dotnet/api/system.security.cryptography.sha256)

### Related Types
- `IAuthenticationHandler` - Custom authentication handler base
- `AuthenticateResult` - Authentication result
- `AuthenticationScheme` - Authentication scheme configuration
- `SHA256` / `HMACSHA256` - Cryptographic hashing

---

## Investigation Questions

### 1. .NET Authentication Middleware Options
- What authentication schemes exist in ASP.NET Core?
- How to implement custom API key authentication handler?
- How does `UseAuthentication()` and `UseAuthorization()` pipeline work?
- When to use custom authentication handler vs. policy-based authorization?

### 2. Cryptographic Hashing
- How does `crypto.subtle.digest('SHA-256', data)` compare to .NET's `SHA256`?
- Should API key hashing use HMAC with a secret key or plain SHA-256?
- How to properly handle byte array to hex string conversion in .NET?
- What are the thread safety considerations for cryptographic operations?

### 3. Repository Pattern Integration
- How to inject repository dependencies into authentication handlers?
- What's the lifetime scope for repositories used in middleware (Scoped vs Singleton)?
- How to handle database connection failures during authentication?
- Should authentication middleware access repositories directly or use a service layer?

### 4. Security Best Practices
- Should API keys be hashed with a salt or plain SHA-256?
- How to prevent timing attacks when comparing API key hashes?
- What error messages should be returned (avoid information leakage)?
- How to implement rate limiting for failed authentication attempts?

---

## Code Patterns

### Custom API Key Authentication Handler (Expected .NET)
```csharp
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeySchemeOptions>
{
    private readonly IUserRepository _userRepository;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeySchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IUserRepository userRepository)
        : base(options, logger, encoder)
    {
        _userRepository = userRepository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.Fail("Missing Authorization header");
        }

        if (!authHeader.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("Invalid Authorization format");
        }

        var apiKey = authHeader.ToString().Substring("Bearer ".Length);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return AuthenticateResult.Fail("Missing token");
        }

        var apiKeyHash = HashApiKey(apiKey);
        var user = await _userRepository.GetByApiKeyHash(apiKeyHash);

        if (user is null)
        {
            return AuthenticateResult.Fail("Invalid API key");
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    private static string HashApiKey(string apiKey)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(apiKey);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

### Middleware Registration (Expected .NET)
```csharp
// Program.cs
builder.Services
    .AddAuthentication(ApiKeySchemeOptions.SchemeName)
    .AddScheme<ApiKeySchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeySchemeOptions.SchemeName, _ => { });

builder.Services.AddScoped<IUserRepository, UserRepository>();

app.UseAuthentication();
app.UseAuthorization();

// Endpoint protection
app.MapGet("/api/agents", async (HttpContext ctx, IUserRepository repo) =>
{
    var userId = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (userId is null) return Results.Unauthorized();

    var agents = await repo.GetAgentsByUserId(userId);
    return Results.Ok(agents);
}).RequireAuthorization();
```

### Constants and Options (Expected .NET)
```csharp
public class ApiKeySchemeOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}

public record User(
    string Id,
    string Email,
    string ApiKeyHash,
    DateTime CreatedAt,
    DateTime? UpdatedAt = null
);

public interface IUserRepository
{
    Task<User?> GetByApiKeyHash(string apiKeyHash, CancellationToken cancellationToken = default);
}
```

---

## TypeScript to .NET Mapping

| TypeScript Concept | .NET Equivalent | Notes |
|-------------------|-----------------|-------|
| `crypto.subtle.digest('SHA-256')` | `SHA256.Create().ComputeHash()` | .NET is synchronous, consider `SHA256.HashData()` |
| `TextEncoder()` | `Encoding.UTF8.GetBytes()` | |
| `Array.from(new Uint8Array())` | Direct byte array access | |
| `.map(b => b.toString(16).padStart(2, '0'))` | `Convert.ToHexString().ToLowerInvariant()` | |
| Hono middleware pattern | ASP.NET Core middleware or `AuthenticationHandler` | Use `IAuthenticationHandler` for auth |
| `ctx.get('runtime')` | `HttpContext.RequestServices` | DI container access |
| `ctx.set('user', user)` | `HttpContext.User` | ClaimsPrincipal assignment |

---

## Expected Deliverables

1. **Custom Authentication Handler**
   - `ApiKeyAuthenticationHandler` implementing `AuthenticationHandler<TOptions>`
   - SHA-256 hashing for API keys
   - Integration with `IUserRepository`

2. **Configuration and Registration**
   - `ApiKeySchemeOptions` for scheme configuration
   - Service registration in DI container
   - Middleware pipeline setup

3. **Security Hardening**
   - Constant-time hash comparison
   - Proper error handling without information leakage
   - Integration with rate limiting for failed attempts

---

## Notes

- The TypeScript implementation uses Web Crypto API (`crypto.subtle.digest`) which is async. .NET's `SHA256.ComputeHash` is synchronous — consider `SHA256.HashData` for simpler one-shot hashing.
- Repository pattern requires scoped services — ensure `IUserRepository` is registered as scoped, not singleton.
- Multi-tenancy: User entity includes email for identification, API key hash for authentication.
- Consider whether to use HMAC-SHA256 (requires secret key) or plain SHA-256. The TypeScript implementation uses plain SHA-256.
- ASP.NET Core authentication requires setting `HttpContext.User` with a `ClaimsPrincipal` containing user claims.
