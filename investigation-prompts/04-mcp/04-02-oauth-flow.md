# Investigation: OAuth Authentication for MCP Servers

## Objective
Investigate how OAuth authentication is implemented for MCP servers, including token storage, refresh logic, and the authorization code flow with PKCE support.

---

## Source Files

### TypeScript OAuth Implementation
- **OAuth Provider**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/oauth.ts
- **MCP Client (OAuth usage)**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/client.ts
- **MCP Types**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/mcp/types.ts

### MCP SDK OAuth
- **OAuthClientProvider**: Interface from `@modelcontextprotocol/sdk/client/auth.js`
- **OAuthTokens**: Token structure from `@modelcontextprotocol/sdk/shared/auth.js`

---

## Microsoft Documentation

- **Microsoft.Identity.Web**: https://learn.microsoft.com/entra/identity-platform/msal-overview
- **MSAL.NET**: https://learn.microsoft.com/entra/msal/dotnet/
- **OAuth 2.0 PKCE**: https://learn.microsoft.com/entra/identity-platform/v2-oauth2-auth-code-flow
- **Token Caching**: https://learn.microsoft.com/entra/msal/dotnet/how-to/token-cache-serialization
- **ASP.NET Core Data Protection**: https://learn.microsoft.com/aspnet/core/security/data-protection/

---

## Investigation Questions

1. **OAuth Flow Implementation**
   - How does TS implement the `OAuthClientProvider` interface?
   - What is the authorization code flow with PKCE?
   - How does the callback URL pattern work (`/mcp/{serverName}/callback`)?

2. **Token Storage**
   - How are tokens stored in `.mcp.oauth.json`?
   - What is the structure of `OAuthTokens` (access_token, refresh_token, expires_at)?
   - How should tokens be secured in .NET (Data Protection API, Key Vault)?

3. **Token Refresh**
   - How does the MCP SDK handle automatic token refresh?
   - What is the refresh logic when tokens expire?
   - How should .NET implement proactive token refresh?

4. **PKCE Support**
   - How is the code verifier stored and retrieved?
   - What is the relationship between code_verifier and code_challenge?
   - How does .NET implement PKCE (cryptographic methods)?

5. **Pending Authorization Flow**
   - How are pending auth URLs stored in-memory (`pendingAuths` Map)?
   - How does the API surface the auth URL to the user?
   - What is the `finishAuth` flow for completing OAuth?

---

## Code Patterns to Analyze

### OAuthClientProvider Implementation
```typescript
// From oauth.ts
export function createOAuthProvider(
  serverName: string,
  rootDir: string,
  callbackUrl: string,
): OAuthClientProvider {
  const _redirectUrl = new URL(callbackUrl)

  return {
    get redirectUrl() {
      return _redirectUrl
    },

    get clientMetadata(): OAuthClientMetadata {
      return {
        redirect_uris: [_redirectUrl.toString()],
        token_endpoint_auth_method: 'none',
        grant_types: ['authorization_code'],
        response_types: ['code'],
        client_name: `agent-mcp-${serverName}`,
        scope: 'read write',
      }
    },

    async clientInformation() {
      const data = await readServerData(rootDir, serverName)
      if (!data.clientId) return undefined
      return {
        client_id: data.clientId,
        ...(data.clientSecret && { client_secret: data.clientSecret }),
      }
    },

    async saveClientInformation(info) {
      await writeServerData(rootDir, serverName, {
        clientId: info.client_id,
        clientSecret: 'client_secret' in info ? (info.client_secret as string) : undefined,
      })
    },

    async tokens() {
      const data = await readServerData(rootDir, serverName)
      return data.tokens
    },

    async saveTokens(tokens: OAuthTokens) {
      await writeServerData(rootDir, serverName, { tokens })
    },

    async redirectToAuthorization(authorizationUrl: URL) {
      pendingAuths.set(serverName, authorizationUrl)
    },

    async saveCodeVerifier(codeVerifier: string) {
      await writeServerData(rootDir, serverName, { codeVerifier })
    },

    async codeVerifier() {
      const data = await readServerData(rootDir, serverName)
      if (!data.codeVerifier) throw new Error(`No code verifier for ${serverName}`)
      return data.codeVerifier
    },
  }
}
```

- How should this be translated to C# with proper async patterns?
- What interface should be defined for the .NET OAuth provider?

### Token Storage with File Locking
```typescript
// From oauth.ts
let lockChain = Promise.resolve()

async function withFileLock<T>(fn: () => Promise<T>): Promise<T> {
  let release!: () => void
  const gate = new Promise<void>(r => { release = r })
  const prev = lockChain
  lockChain = gate
  await prev
  try {
    return await fn()
  } finally {
    release()
  }
}

async function loadStore(rootDir: string): Promise<OAuthStore> {
  const path = resolve(rootDir, OAUTH_FILE)
  const raw = await readFile(path, 'utf-8').catch(() => '{}')
  try {
    return JSON.parse(raw) as OAuthStore
  } catch {
    return {}
  }
}

async function saveStore(rootDir: string, store: OAuthStore): Promise<void> {
  const path = resolve(rootDir, OAUTH_FILE)
  await writeFile(path, JSON.stringify(store, null, 2), 'utf-8')
}
```

- What is the .NET equivalent for file-based token storage?
- How should `SemaphoreSlim` or `AsyncLock` be used for thread safety?
- What encryption options exist for securing tokens at rest?

### OAuth Data Structure
```typescript
// From oauth.ts
interface ServerOAuthData {
  tokens?: OAuthTokens
  clientId?: string
  clientSecret?: string
  codeVerifier?: string
}

type OAuthStore = Record<string, ServerOAuthData>
```

- How should this be modeled in C#?
- What about token expiration tracking?

### Finish Auth Flow
```typescript
// From client.ts
async function finishAuth(serverName: string, authorizationCode: string): Promise<void> {
  const transport = transports.get(serverName)
  if (!transport || !(transport instanceof StreamableHTTPClientTransport)) {
    throw new Error(`No HTTP transport for server: ${serverName}`)
  }

  await transport.finishAuth(authorizationCode)

  // Reconnect with fresh client on existing transport
  const client = new Client(
    { name: 'agent-mcp-client', version: '1.0.0' },
    { capabilities: {} },
  )
  await client.connect(transport)
  clients.set(serverName, client)
  authRequired.delete(serverName)
}
```

- How does the authorization code get exchanged for tokens?
- What happens behind the scenes in `transport.finishAuth()`?

---

## Expected Deliverables

### 1. OAuth Flow Diagram
```
┌──────────────┐                              ┌──────────────┐
│   Agent      │                              │  MCP Server  │
│  (Client)    │                              │  (OAuth)     │
└──────┬───────┘                              └──────┬───────┘
       │                                             │
       │  1. Connect                                 │
       │────────────────────────────────────────────►│
       │                                             │
       │  2. 401 Unauthorized + WWW-Authenticate     │
       │◄────────────────────────────────────────────│
       │                                             │
       │  3. Discover OAuth metadata                 │
       │────────────────────────────────────────────►│
       │                                             │
       │  4. OAuth metadata (auth_url, token_url)    │
       │◄────────────────────────────────────────────│
       │                                             │
       │  5. Generate PKCE verifier & challenge      │
       │  6. Save verifier                           │
       │  7. Build authorization URL                 │
       │                                             │
       │  8. Return auth URL to user                 │
       │◄────────────────────────────────────────────│
       │                                             │
┌──────┴───────┐                              ┌──────┴───────┐
│    User      │                              │              │
│   (Browser)  │                              │              │
└──────┬───────┘                              └──────┬───────┘
       │  9. User navigates to auth URL              │
       │────────────────────────────────────────────►│
       │                                             │
       │  10. User authorizes                        │
       │◄────────────────────────────────────────────│
       │                                             │
       │  11. Redirect to callback with code         │
       │◄────────────────────────────────────────────│
       │                                             │
┌──────┴───────┐                              ┌──────┴───────┐
│   Agent      │                              │  MCP Server  │
│  (Callback)  │                              │  (Token EP)  │
└──────┬───────┘                              └──────┬───────┘
       │  12. Callback with authorization code       │
       │◄────────────────────────────────────────────│
       │                                             │
       │  13. Exchange code + verifier for tokens    │
       │────────────────────────────────────────────►│
       │                                             │
       │  14. Access token + refresh token           │
       │◄────────────────────────────────────────────│
       │                                             │
       │  15. Save tokens                            │
       │  16. Reconnect to MCP server                │
       │────────────────────────────────────────────►│
       │                                             │
       │  17. Connected (authenticated)              │
       │◄────────────────────────────────────────────│
```

### 2. C# OAuth Provider Interface
```csharp
// Proposed C# interface
public interface IMcpOAuthProvider
{
    Uri RedirectUrl { get; }
    OAuthClientMetadata ClientMetadata { get; }

    Task<ClientInformation?> GetClientInformationAsync(CancellationToken cancellationToken = default);
    Task SaveClientInformationAsync(ClientInformation info, CancellationToken cancellationToken = default);

    Task<OAuthTokens?> GetTokensAsync(CancellationToken cancellationToken = default);
    Task SaveTokensAsync(OAuthTokens tokens, CancellationToken cancellationToken = default);

    Task RedirectToAuthorizationAsync(Uri authorizationUrl, CancellationToken cancellationToken = default);
    Task SaveCodeVerifierAsync(string codeVerifier, CancellationToken cancellationToken = default);
    Task<string> GetCodeVerifierAsync(CancellationToken cancellationToken = default);
}

public record ClientInformation(string ClientId, string? ClientSecret = null);

public record OAuthTokens
{
    public string AccessToken { get; init; } = string.Empty;
    public string? RefreshToken { get; init; }
    public string? TokenType { get; init; }
    public int? ExpiresIn { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? Scope { get; init; }
}
```

### 3. Token Storage Service
```csharp
// Proposed C# token storage with encryption
public interface ITokenStorageService
{
    Task<ServerOAuthData?> LoadAsync(string serverName, CancellationToken cancellationToken = default);
    Task SaveAsync(string serverName, ServerOAuthData data, CancellationToken cancellationToken = default);
    Task ClearAsync(string serverName, CancellationToken cancellationToken = default);
}

public class DataProtectionTokenStorage : ITokenStorageService
{
    private readonly IDataProtector _protector;
    private readonly string _storagePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Implementation using ASP.NET Core Data Protection
}

public record ServerOAuthData
{
    public OAuthTokens? Tokens { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? CodeVerifier { get; init; }
}
```

### 4. Comparison Table

| Feature | TypeScript | .NET | Notes |
|---------|------------|------|-------|
| Token Storage | JSON file | Data Protection API / Key Vault | |
| Encryption | None (plaintext) | DPAPI or Azure Key Vault | |
| Concurrency | Promise chain lock | SemaphoreSlim / AsyncLock | |
| OAuth Library | MCP SDK built-in | MSAL.NET / IdentityModel | |
| PKCE | Built-in | Manual or via IdentityModel | |
| Token Refresh | SDK automatic | MSAL automatic or manual | |
| Callback | Express route | ASP.NET Core endpoint | |

---

## Additional Research Areas

- [ ] Investigate MSAL.NET integration for MCP OAuth
- [ ] Research ASP.NET Core Data Protection for token encryption
- [ ] Compare MSAL token cache serialization options
- [ ] Document Azure Key Vault integration for production
- [ ] Explore distributed token storage for multi-instance deployments
- [ ] Research OIDC discovery for automatic OAuth metadata

---

## Status

- [ ] TypeScript OAuth source reviewed
- [ ] MSAL.NET documentation consulted
- [ ] C# interfaces designed
- [ ] Token storage pattern documented
- [ ] OAuth flow diagram created
