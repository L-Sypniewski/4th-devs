# Investigation: Trusted Action Confirmation (Human-in-the-Loop)

## Objective
Investigate how TypeScript implements human-in-the-loop confirmation for sensitive/dangerous operations, then design equivalent patterns for .NET using authorization attributes, action classification, and approval workflows.

---

## Source Files

- **Confirmation Loop**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_confirmation/src/agent.js
- **Ask User Tool**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/definitions/ask-user.ts
- **Native Tools**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_confirmation/src/native/tools.js
- **Tool Types**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/types.ts
- **Tool Registry**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/registry.ts

---

## Investigation Questions

1. **Action Classification in TypeScript**
   - How does `TOOLS_REQUIRING_CONFIRMATION` identify dangerous operations?
   - What is the difference between pre-approval and runtime confirmation?
   - How are tools categorized by risk level (safe, moderate, dangerous)?
   - What metadata is associated with each confirmation request (toolName, args)?

2. **Confirmation Flow**
   - How does `confirmTool` callback integrate with the agent loop?
   - What happens when user rejects a confirmation (rejected response)?
   - How are confirmations sequenced for multiple tool calls (sequential vs parallel)?
   - What is returned to the agent after rejection (error message, retry)?

3. **Whitelist and Authorization**
   - How does `validateRecipients` enforce authorization rules?
   - What patterns exist for domain-based vs exact matching?
   - How are authorization failures communicated back to the agent?
   - Can whitelists be dynamically updated at runtime?

4. **Human Input Tool Pattern**
   - How does `ask_user` tool differ from inline confirmation?
   - What is the `type: 'human'` tool variant used for?
   - How does the agent transition to 'waiting' state for human input?
   - How is human input delivered back to the agent (HTTP endpoint)?

5. **.NET Authorization Patterns**
   - How can `[RequiresConfirmation]` attribute declare dangerous operations?
   - What role does `IActionAuthorizer` interface play in approval workflows?
   - How to implement `AuthorizationResult` with approval tracking?
   - What about `IAuthorizationService` from Microsoft.AspNetCore.Authorization?

6. **Audit Trail and Logging**
   - How should confirmation requests be logged for audit?
   - What metadata is needed for compliance (timestamp, user, action, reason)?
   - How to implement time-based approval windows?
   - What about two-factor confirmation for critical actions?

---

## Code Patterns to Analyze

### TypeScript Confirmation Hook

```javascript
// From agent.js - Tool execution with confirmation
const TOOLS_REQUIRING_CONFIRMATION = new Set(["send_email"]);

const runTool = async (mcpClient, toolCall, confirmTool) => {
  const args = JSON.parse(toolCall.arguments);
  const toolName = toolCall.name;

  // Check if tool requires confirmation
  if (TOOLS_REQUIRING_CONFIRMATION.has(toolName) && confirmTool) {
    const confirmed = await confirmTool(toolName, args);

    if (!confirmed) {
      const output = JSON.stringify({
        success: false,
        error: "User rejected the action",
        rejected: true
      });
      return { type: "function_call_output", call_id: toolCall.call_id, output };
    }
  }
  // ... proceed with execution
};
```

**Analysis Points:**
- Set-based lookup for dangerous tools
- Callback pattern for confirmation UI
- Rejected responses return error to agent
- Confirmation blocks sequential execution

### TypeScript Whitelist Authorization

```javascript
// From tools.js - Whitelist validation
const isEmailAllowed = (email, whitelist) => {
  const normalized = email.toLowerCase();
  const domain = normalized.split("@")[1];

  return whitelist.some(pattern => {
    const p = pattern.toLowerCase();
    if (p.startsWith("@")) {
      return domain === p.slice(1);
    }
    return normalized === p;
  });
};
```

**Analysis Points:**
- Supports exact matches (`user@example.com`)
- Supports domain wildcards (`@example.com`)
- Case-insensitive normalization
- Returns boolean without exposing why

### TypeScript Human Input Tool

```typescript
// From ask-user.ts - Human confirmation tool
export const askUserTool: Tool = {
  type: 'human',
  definition: {
    type: 'function',
    name: 'ask_user',
    description: 'Ask the user a question and wait for their response.',
    parameters: {
      type: 'object',
      properties: {
        question: { type: 'string' }
      },
      required: ['question'],
    },
  },
  handler: async (args) => {
    const { question } = args as unknown as AskUserArgs
    // Validation only — runner defers to waitingFor
    return { ok: true, output: question }
  },
}
```

**Analysis Points:**
- Tool type 'human' signals waiting behavior
- Handler validates but doesn't block
- Runner transitions agent to 'waiting' state
- Question stored in `waitingFor` metadata

### .NET Equivalent - Action Authorizer Interface

```csharp
// C# Pattern Template - Authorization interface
public interface IActionAuthorizer
{
    /// <summary>
    /// Requests approval for a potentially dangerous action.
    /// Returns result indicating approved/rejected with reason.
    /// </summary>
    Task<AuthorizationResult> RequestApprovalAsync<TAction>(
        TAction action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an action is pre-approved (e.g., whitelisted).
    /// </summary>
    Task<bool> IsPreApprovedAsync<TAction>(
        TAction action,
        CancellationToken cancellationToken = default);
}

public record AuthorizationResult(
    bool Approved,
    string? Reason,
    string? ConfirmationId,
    DateTimeOffset? ExpiresAt
);
```

### .NET Equivalent - Requires Confirmation Attribute

```csharp
// C# Pattern Template - Attribute-based declaration
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresConfirmationAttribute : Attribute
{
    public string Message { get; }
    public ConfirmationLevel Level { get; }
    public TimeSpan? ApprovalWindow { get; }

    public RequiresConfirmationAttribute(
        string message,
        ConfirmationLevel level = ConfirmationLevel.Moderate)
    {
        Message = message;
        Level = level;
    }
}

public enum ConfirmationLevel
{
    Safe,       // Read-only, no confirmation needed
    Moderate,   // Create/update non-critical data
    Dangerous   // Delete, send communications, financial
}
```

### .NET Equivalent - Tool Execution Interceptor

```csharp
// C# Pattern Template - Intercept tool execution
public class ToolExecutor
{
    private readonly IActionAuthorizer _authorizer;
    private readonly IAuditLogger _auditLogger;

    public async Task<ToolResult> ExecuteAsync(
        string toolName,
        ToolArguments args,
        CancellationToken cancellationToken = default)
    {
        var tool = _toolRegistry.Get(toolName);
        if (tool is null)
            return ToolResult.NotFound(toolName);

        // Check for confirmation attribute
        var requiresConfirmation = tool.RequiresConfirmation();
        if (requiresConfirmation is not null)
        {
            var result = await _authorizer.RequestApprovalAsync(
                new ToolAction(toolName, args),
                cancellationToken);

            await _auditLogger.LogAuthorizationAttemptAsync(
                toolName, args, result);

            if (!result.Approved)
            {
                return ToolResult.Rejected(
                    result.Reason ?? "Action not approved");
            }
        }

        return await tool.ExecuteAsync(args, cancellationToken);
    }
}
```

### .NET Equivalent - Whitelist Authorization Policy

```csharp
// C# Pattern Template - Policy-based authorization
public class WhitelistAuthorizationHandler
    : AuthorizationHandler<WhitelistRequirement>
{
    private readonly IWhitelistProvider _whitelistProvider;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WhitelistRequirement requirement)
    {
        var action = context.Resource as ToolAction;
        if (action is null)
        {
            context.Fail();
            return;
        }

        var whitelist = await _whitelistProvider.GetWhitelistAsync();
        var isAllowed = requirement.CheckAccess(action, whitelist);

        if (isAllowed)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
            await _auditLogger.LogDenialAsync(action, requirement);
        }
    }
}

public class WhitelistRequirement : IAuthorizationRequirement
{
    public string ResourceType { get; }
    public bool SupportDomainWildcard { get; }

    public bool CheckAccess(ToolAction action, Whitelist whitelist)
    {
        // Implementation similar to TypeScript pattern
        var identifier = action.GetResourceIdentifier();
        var normalized = identifier.ToLowerInvariant();

        return whitelist.AllowedEntries.Any(pattern =>
        {
            var p = pattern.ToLowerInvariant();
            if (SupportDomainWildcard && p.StartsWith("@"))
            {
                var domain = normalized.Split('@')[1];
                return domain == p[1..];
            }
            return normalized == p;
        });
    }
}
```

### .NET Equivalent - Audit Trail

```csharp
// C# Pattern Template - Audit logging
public interface IAuditLogger
{
    Task LogAuthorizationAttemptAsync(
        string toolName,
        ToolArguments args,
        AuthorizationResult result);

    Task LogDenialAsync(
        ToolAction action,
        IAuthorizationRequirement requirement);
}

public class AuditLogEntry
{
    public required string AuditId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string ToolName { get; init; }
    public required ToolArguments Arguments { get; init; }
    public required bool Approved { get; init; }
    public string? Reason { get; init; }
    public string? ConfirmationId { get; init; }
    public string? UserId { get; init; }
    public string? SessionId { get; init; }
}
```

---

## State Diagram: Confirmation Flow

```
                    +----------+
                    | Tool Call |
                    +----------+
                         |
                         v
                +------------------+
                | Requires Confirm? |
                +------------------+
                     |         |
                    Yes        No
                     |         |
                     v         v
            +-------------+   +--------+
            | Request     |   | Execute|
            | Approval    |   | Direct |
            +-------------+   +--------+
                     |
                     v
            +-------------+
            | User        |
            | Decision    |
            +-------------+
               |       |
            Approved   Rejected
               |       |
               v       v
         +--------+ +-------+
         |Execute | | Return|
         |Tool    | | Error |
         +--------+ +-------+
```

---

## Microsoft Documentation

- **Authorization Service**: https://learn.microsoft.com/aspnet/core/security/authorization/
- **Policy-Based Authorization**: https://learn.microsoft.com/aspnet/core/security/authorization/policies
- **Resource-Based Authorization**: https://learn.microsoft.com/aspnet/core/security/authorization/resourcebased
- **Audit Logging**: https://learn.microsoft.com/azure/defender-for-cloud/audit-log
- **Data Annotations**: https://learn.microsoft.com/dotnet/api/system.componentmodel.dataannotations

---

## Expected Deliverables

1. **Action Authorizer Interface**

```csharp
public interface IActionAuthorizer
{
    Task<AuthorizationResult> RequestApprovalAsync<TAction>(
        TAction action,
        CancellationToken cancellationToken = default);

    Task<bool> IsPreApprovedAsync<TAction>(
        TAction action,
        CancellationToken cancellationToken = default);

    Task<string?> CreateConfirmationRequestAsync<TAction>(
        TAction action,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}
```

2. **Confirmation Level Attribute**

```csharp
public enum ConfirmationLevel
{
    Safe,       // No confirmation needed
    Moderate,   // Simple confirmation dialog
    Dangerous,  // Two-factor or explicit approval
    Critical    // Multi-party approval required
}

[AttributeUsage(AttributeTargets.Method)]
public class RequiresConfirmationAttribute : Attribute
{
    public string Message { get; }
    public ConfirmationLevel Level { get; }
    public TimeSpan? ApprovalWindow { get; }
    public bool RequireTwoFactor { get; }
}
```

3. **Audit Logger Interface**

```csharp
public interface IConfirmationAuditLogger
{
    Task LogRequestAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken = default);

    Task LogResponseAsync(
        ConfirmationResponse response,
        CancellationToken cancellationToken = default);

    Task<AuditLogEntry[]> GetHistoryAsync(
        string toolName,
        DateTimeOffset? since = null,
        CancellationToken cancellationToken = default);
}
```

4. **Implementation Notes Document** covering:
   - Attribute-based vs policy-based authorization
   - Whitelist patterns and dynamic updates
   - Time-based approval windows
   - Two-factor confirmation for critical actions
   - Audit trail retention and query patterns
   - Integration with ASP.NET Core authorization

---

## Validation Checklist

- [ ] Confirmation levels defined (safe, moderate, dangerous, critical)
- [ ] Attribute-based declaration pattern implemented
- [ ] Whitelist authorization handler created
- [ ] Audit logging for all confirmation attempts
- [ ] Time-based approval windows supported
- [ ] Two-factor confirmation pattern documented
- [ ] Integration with ASP.NET Core authorization
- [ ] Tool registry integration for auto-confirmation

---

## Status

- [ ] Source code analyzed
- [ ] TypeScript confirmation flow documented
- [ ] C# authorizer interface implemented
- [ ] Whitelist authorization pattern designed
- [ ] Audit logging pattern created
- [ ] Integration tests written
