# Investigation: Content Moderation API Integration

## Objective
Investigate content moderation patterns for AI agents, including safety checks, content filtering, PII detection, and prompt injection detection in both TypeScript and .NET frameworks.

---

## Source Files

### TypeScript Reference
- **Chat Service**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/routes/chat.service.ts
- **Chat Response Filter**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/routes/chat.response.ts
- **Tool Registry**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/registry.ts

Note: The 01_05_agent codebase currently does not implement explicit content moderation. This investigation will define patterns for adding moderation capabilities.

---

## Microsoft Documentation

### Azure AI Content Safety
- **Overview**: https://learn.microsoft.com/azure/ai-services/content-safety/overview
- **Quickstart (Text)**: https://learn.microsoft.com/azure/ai-services/content-safety/quickstart-text?pivots=programming-language-csharp
- **Harm Categories**: https://learn.microsoft.com/azure/ai-services/content-safety/concepts/harm-categories
- **Prompt Shields (Jailbreak Detection)**: https://learn.microsoft.com/azure/ai-services/content-safety/concepts/jailbreak-detection
- **Groundedness Detection**: https://learn.microsoft.com/azure/ai-services/content-safety/concepts/groundedness
- **Blocklists**: https://learn.microsoft.com/azure/ai-services/content-safety/how-to/use-blocklist

### Azure OpenAI Content Filtering
- **Default Safety Policies**: https://learn.microsoft.com/azure/foundry/openai/concepts/default-safety-policies
- **Content Filtering**: https://learn.microsoft.com/azure/ai-foundry/openai/concepts/content-filter
- **Configuring Filters**: https://learn.microsoft.com/azure/ai-foundry/openai/how-to/content-filters

### Responsible AI Patterns
- **Responsible AI in Azure Workloads**: https://learn.microsoft.com/azure/well-architected/ai/responsible-ai

---

## Investigation Questions

### 1. Content Moderation Fundamentals
- Why is content moderation needed for AI agents?
- What are the different types of content risks (violence, hate speech, sexual content, self-harm, PII)?
- When should moderation be applied (input validation, output filtering, both)?

### 2. Moderation Types and Categories
- **Content Safety**: How to detect violence, hate speech, sexual content, self-harm?
- **PII Detection**: How to identify and redact personally identifiable information?
- **Prompt Injection**: How to detect jailbreak attempts and prompt attacks?
- **Hallucination Detection**: How to verify AI outputs are grounded in source material?
- **Protected Material**: How to detect copyrighted content (song lyrics, articles, recipes)?

### 3. Provider Integrations
- **OpenAI Moderation API**: What is the endpoint, response format, and usage?
- **Azure Content Safety**: How does it differ from OpenAI's built-in moderation?
- **Azure AI Language PII**: How to use PII detection and redaction?
- **Custom Rules**: When to build custom moderation logic vs using APIs?

### 4. Integration Patterns
- **Pre-moderation**: Validate and filter user input before sending to LLM
- **Post-moderation**: Filter and sanitize LLM output before returning to user
- **Real-time Streaming**: How to moderate streaming responses without breaking flow?
- **Gateway Pattern**: Centralizing moderation at the API gateway level
- **Selective Moderation**: When to apply moderation based on context/user tier?

### 5. C# / .NET Implementation Patterns
- How to design a generic `IContentModerator` interface?
- How to handle moderation results and apply thresholds?
- How to integrate with dependency injection and middleware?
- How to implement caching for moderation results?
- How to handle moderation failures (fallback strategies)?

### 6. TypeScript Patterns (for Reference)
- Does 01_05_agent have any existing moderation or input validation?
- How are tool inputs currently validated?
- What patterns exist for filtering response items?

---

## Code Patterns to Analyze

### C# Content Moderator Interface
```csharp
public interface IContentModerator
{
    Task<ModerationResult> ModerateAsync(string content, CancellationToken ct);
}

public record ModerationResult(
    bool IsSafe,
    IReadOnlyList<FlaggedContent> Flags,
    float? OverallScore
);

public record FlaggedContent(
    ModerationCategory Category,
    float Confidence,
    string? MatchedText
);

public enum ModerationCategory
{
    Violence,
    HateSpeech,
    SexualContent,
    SelfHarm,
    Pii,
    PromptInjection,
    ProtectedMaterial
}
```

### Azure Content Safety Example
```csharp
using Azure.AI.ContentSafety;

public class AzureContentModerator : IContentModerator
{
    private readonly ContentSafetyClient _client;

    public AzureContentModerator(ContentSafetyClient client)
    {
        _client = client;
    }

    public async Task<ModerationResult> ModerateAsync(string content, CancellationToken ct)
    {
        var request = new AnalyzeTextOptions(content);

        Response<AnalyzeTextResult> response = await _client.AnalyzeTextAsync(request, ct);

        var flags = new List<FlaggedContent>();
        foreach (var analysis in response.Value.CategoriesAnalysis)
        {
            if (analysis.Severity > 0)
            {
                flags.Add(new FlaggedContent(
                    MapCategory(analysis.Category),
                    analysis.Severity / 7.0f,  // Normalize to 0-1
                    null
                ));
            }
        }

        bool isSafe = flags.All(f => f.Confidence < 0.5f);  // Threshold
        float? overallScore = flags.Count > 0 ? flags.Max(f => f.Confidence) : null;

        return new ModerationResult(isSafe, flags, overallScore);
    }

    private static ModerationCategory MapCategory(TextCategory category) =>
        category switch
        {
            TextCategory.Violence => ModerationCategory.Violence,
            TextCategory.Hate => ModerationCategory.HateSpeech,
            TextCategory.Sexual => ModerationCategory.SexualContent,
            TextCategory.SelfHarm => ModerationCategory.SelfHarm,
            _ => throw new ArgumentException($"Unknown category: {category}")
        };
}
```

### Pre-Moderation Middleware Pattern
```csharp
public class ModerationMiddleware
{
    private readonly IContentModerator _moderator;
    private readonly RequestDelegate _next;

    public ModerationMiddleware(IContentModerator moderator, RequestDelegate next)
    {
        _moderator = moderator;
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract user input from request
        var userInput = await ExtractUserInputAsync(context);

        // Pre-moderation check
        var result = await _moderator.ModerateAsync(userInput, context.RequestAborted);

        if (!result.IsSafe)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Content moderation failed",
                flags = result.Flags
            });
            return;
        }

        await _next(context);
    }
}
```

### Streaming Moderation Pattern
```csharp
public async IAsyncEnumerable<string> StreamWithModeration(
    IAsyncEnumerable<string> source,
    [EnumeratorCancellation] CancellationToken ct)
{
    var buffer = new StringBuilder();
    var lastCheckedPosition = 0;

    await foreach (var chunk in source.WithCancellation(ct))
    {
        buffer.Append(chunk);

        // Check moderation every N characters or on sentence boundaries
        if (buffer.Length - lastCheckedPosition > 100)
        {
            var toCheck = buffer.ToString();
            var result = await _moderator.ModerateAsync(toCheck, ct);

            if (!result.IsSafe)
            {
                yield return "[Content filtered]";
                yield break;
            }

            lastCheckedPosition = buffer.Length;
        }

        yield return chunk;
    }
}
```

### PII Detection Pattern
```csharp
using Azure.AI.TextAnalytics;

public class PiiModerator : IContentModerator
{
    private readonly TextAnalyticsClient _client;

    public async Task<ModerationResult> ModerateAsync(string content, CancellationToken ct)
    {
        var result = await _client.RecognizePiiEntitiesAsync(content, cancellationToken: ct);

        if (result.Value.HasPii)
        {
            var redacted = result.Value.RedactedText;
            var entities = result.Value.Entities.Select(e => new FlaggedContent(
                ModerationCategory.Pii,
                1.0f,
                e.Text
            ));

            return new ModerationResult(false, entities.ToList(), 1.0f)
            {
                RedactedContent = redacted
            };
        }

        return ModerationResult.Safe;
    }
}
```

---

## Expected Deliverables

### 1. C# Moderation Abstraction
Document the `IContentModerator` interface and how different providers implement it:
- Azure Content Safety implementation
- OpenAI Moderation API implementation
- PII detection with Azure AI Language
- Custom rule-based moderator

### 2. Integration Architecture
Document where moderation fits in the request pipeline:
- Middleware vs service-level integration
- Pre-moderation vs post-moderation vs both
- Streaming moderation patterns
- Configuration and threshold management

### 3. Provider Comparison Table

| Feature | OpenAI Moderation | Azure Content Safety | Azure AI Language PII |
|---------|-------------------|---------------------|----------------------|
| Endpoint | /moderations | Text analysis API | PiiEntities API |
| Categories | 6 (hate, violence, etc.) | 4 main categories | PII entity types |
| Jailbreak Detection | No | Yes (Prompt Shields) | No |
| PII Redaction | No | No | Yes |
| Streaming Support | No | No | No |
| Pricing | Per 1K tokens | Per 1K text units | Per 1K text records |

### 4. TypeScript Comparison
Document whether 01_05_agent has any existing moderation patterns:
- Input validation in `chat.service.ts`
- Response filtering in `chat.response.ts`
- Opportunities for adding moderation hooks

### 5. Best Practices Checklist
- [ ] Define clear severity thresholds
- [ ] Implement fallback for moderation failures
- [ ] Log moderation results for monitoring
- [ ] Cache moderation results to reduce cost
- [ ] Handle false positives gracefully
- [ ] Implement user feedback loop
- [ ] Configure different policies per user tier
- [ ] Consider cultural and linguistic differences

---

## Additional Research Areas

- [ ] Investigate Prompt Shields API for jailbreak detection
- [ ] Research Groundedness Detection for hallucination prevention
- [ ] Compare cost/performance of Azure vs OpenAI moderation
- [ ] Document blocklist management patterns
- [ ] Explore custom moderation category training
- [ ] Research rate limiting for moderation APIs
- [ ] Implement moderation result caching strategy

---

## Implementation Patterns

### Middleware Integration

Integrate content moderation into the request pipeline:

```csharp
public static class ModerationServiceExtensions
{
    public static IServiceCollection AddContentModeration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ModerationOptions>(configuration.GetSection("Moderation"));

        services.AddSingleton<IContentModerator, CompositeModerator>();
        services.AddSingleton<IPreModerator, AzureContentModerator>();
        services.AddSingleton<IPostModerator, OutputFilterModerator>();

        // Register provider-specific moderators
        services.AddAzureContentSafety(configuration);
        services.AddOpenAIModeration(configuration);

        return services;
    }

    public static IApplicationBuilder UseContentModeration(
        this IApplicationBuilder app)
    {
        return app.UseMiddleware<PreModerationMiddleware>()
                  .UseMiddleware<PostModerationMiddleware>();
    }
}
```

### Pre-Moderation Middleware

```csharp
public class PreModerationMiddleware
{
    private readonly IPreModerator _moderator;
    private readonly ILogger<PreModerationMiddleware> _logger;
    private readonly ModerationOptions _options;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!_options.EnablePreModeration)
        {
            await next(context);
            return;
        }

        var request = await ReadChatRequestAsync(context);
        var result = await _moderator.ModerateAsync(
            request.Content,
            context.RequestAborted);

        if (!result.IsSafe)
        {
            _logger.LogWarning(
                "Pre-moderation blocked request: {Categories}",
                string.Join(", ", result.Flags.Select(f => f.Category)));

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Content not allowed",
                categories = result.Flags.Select(f => f.Category.ToString()),
                severity = result.OverallScore
            });
            return;
        }

        // Store sanitized content if PII was redacted
        if (result.RedactedContent is not null)
        {
            context.Items["RedactedContent"] = result.RedactedContent;
        }

        await next(context);
    }
}
```

### Post-Moderation Middleware

```csharp
public class PostModerationMiddleware
{
    private readonly IPostModerator _moderator;
    private readonly ILogger<PostModerationMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var originalBodyStream = context.Response.Body;
        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        await next(context);

        memoryStream.Position = 0;
        var content = await new StreamReader(memoryStream).ReadToEndAsync();

        var result = await _moderator.ModerateAsync(content, context.RequestAborted);

        if (!result.IsSafe)
        {
            _logger.LogWarning("Post-moderation filtered response content");

            // Replace flagged content with safe alternative
            var safeContent = FilterContent(content, result.Flags);

            context.Response.Body = originalBodyStream;
            await context.Response.WriteAsync(safeContent);
        }
        else
        {
            context.Response.Body = originalBodyStream;
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);
        }
    }

    private string FilterContent(string content, IReadOnlyList<FlaggedContent> flags)
    {
        var filtered = content;
        foreach (var flag in flags.OrderByDescending(f => f.MatchedText?.Length ?? 0))
        {
            if (!string.IsNullOrEmpty(flag.MatchedText))
            {
                filtered = filtered.Replace(flag.MatchedText, "[Content filtered]");
            }
        }
        return filtered;
    }
}
```

### Configuration Schema

```csharp
public class ModerationOptions
{
    /// <summary>
    /// Enable pre-moderation of user input
    /// </summary>
    public bool EnablePreModeration { get; set; } = true;

    /// <summary>
    /// Enable post-moderation of AI output
    /// </summary>
    public bool EnablePostModeration { get; set; } = true;

    /// <summary>
    /// Block requests that fail moderation
    /// </summary>
    public bool BlockOnFailure { get; set; } = true;

    /// <summary>
    /// Severity threshold for blocking (0-1)
    /// </summary>
    public float SeverityThreshold { get; set; } = 0.5f;

    /// <summary>
    /// Categories to check
    /// </summary>
    public ModerationCategory[] Categories { get; set; } = Enum.GetValues<ModerationCategory>();

    /// <summary>
    /// Cache moderation results for identical content
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache duration for moderation results
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromHours(1);
}
```

---

## Status

- [ ] TypeScript sources reviewed
- [ ] Microsoft docs consulted
- [ ] C# moderation interface designed
- [ ] Azure Content Safety implementation documented
- [ ] Provider comparison completed
- [ ] Integration patterns documented
- [ ] Best practices checklist created
- [ ] Pre-moderation middleware implemented
- [ ] Post-moderation middleware implemented
- [ ] Configuration schema defined
