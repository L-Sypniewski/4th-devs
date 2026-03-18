# Investigation: Hallucination Management

**Status**: Pending
**Priority**: High
**Category**: Execution

---

## Source Files

### TypeScript Reference
- **Validation**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/validation/
- **Tool Output Validation**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/tools/

Note: The 01_05_agent codebase has basic output validation. This investigation defines comprehensive hallucination management patterns.

---

## Microsoft Documentation

### Primary References
- **Azure AI Content Safety - Groundedness**: https://learn.microsoft.com/azure/ai-services/content-safety/concepts/groundedness
- **Semantic Kernel - Validation**: https://learn.microsoft.com/semantic-kernel/
- **Prompt Engineering**: https://learn.microsoft.com/azure/ai-foundry/openai/concepts/prompt-engineering

### Related Patterns
- **Fact-Checking**: External knowledge verification
- **Confidence Scoring**: Model uncertainty quantification
- **Structured Output**: Enforcing output schemas

---

## Investigation Questions

### 1. Hallucination Fundamentals
- What types of hallucinations exist (factual, logical, consistency)?
- Why do LLMs produce hallucinations?
- What are the warning signs of hallucinated output?
- How to distinguish creative output from hallucination?

### 2. Detection Strategies
- **Groundedness Checking**: How to verify outputs against source material?
- **Factual Verification**: How to cross-reference with external APIs?
- **Consistency Analysis**: How to detect contradictions in responses?
- **Confidence Estimation**: How to interpret logprobs and uncertainty?

### 3. Prevention Strategies
- **System Prompts**: How to instruct models to be truthful?
- **Temperature Settings**: How does sampling affect hallucination rate?
- **Few-Shot Examples**: How to demonstrate truthful behavior?
- **Retrieval Augmentation**: How does RAG reduce hallucinations?

### 4. Mitigation Strategies
- **Structured Outputs**: How does JSON mode reduce errors?
- **Tool Validation**: How to validate tool call arguments?
- **Human Review**: When to require human verification?
- **Fallback Responses**: How to gracefully handle uncertainty?

### 5. .NET Implementation Patterns
- How to design `IHallucinationDetector` interface?
- How to integrate with the agent loop?
- How to implement confidence thresholds?
- How to log and track hallucination incidents?

---

## Code Patterns to Analyze

### .NET Hallucination Detection Interface

```csharp
/// <summary>
/// Detects potential hallucinations in AI outputs
/// </summary>
public interface IHallucinationDetector
{
    /// <summary>
    /// Analyzes text for potential hallucinations
    /// </summary>
    Task<HallucinationResult> AnalyzeAsync(
        string content,
        HallucinationContext? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if tool call arguments are valid
    /// </summary>
    Task<ToolValidationResult> ValidateToolCallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken ct = default);
}

public record HallucinationResult(
    bool HasHallucinationRisk,
    float ConfidenceScore,
    IReadOnlyList<HallucinationWarning> Warnings,
    HallucinationSeverity Severity
);

public record HallucinationWarning(
    HallucinationType Type,
    string Description,
    string? AffectedText,
    float Confidence
);

public enum HallucinationType
{
    UnverifiableClaim,
    LogicalInconsistency,
    TemporalError,
    EntityConfusion,
    NumericalError,
    SourceMisattribution,
    FabricatedReference
}

public enum HallucinationSeverity
{
    Low,       // Minor inaccuracy, acceptable
    Medium,    // Significant concern, review recommended
    High,      // Major issue, should be flagged
    Critical   // Dangerous misinformation, must block
}

public record HallucinationContext(
    string? UserQuery,
    IReadOnlyList<string>? SourceDocuments,
    IReadOnlyList<ChatMessage>? ConversationHistory
);
```

### .NET Groundedness Checker

```csharp
using Azure.AI.ContentSafety;

public class AzureGroundednessChecker : IHallucinationDetector
{
    private readonly ContentSafetyClient _client;
    private readonly ILogger<AzureGroundednessChecker> _logger;

    public AzureGroundednessChecker(
        ContentSafetyClient client,
        ILogger<AzureGroundednessChecker> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<HallucinationResult> AnalyzeAsync(
        string content,
        HallucinationContext? context = null,
        CancellationToken ct = default)
    {
        var warnings = new List<HallucinationWarning>();

        // If source documents are provided, check groundedness
        if (context?.SourceDocuments?.Count > 0)
        {
            var sources = string.Join("\n\n", context.SourceDocuments);
            var request = new GroundednessCheckOptions(content, sources);

            var response = await _client.CheckGroundednessAsync(request, ct);

            if (response.Value.UngroundedSegments?.Count > 0)
            {
                foreach (var segment in response.Value.UngroundedSegments)
                {
                    warnings.Add(new HallucinationWarning(
                        HallucinationType.UnverifiableClaim,
                        "Content not grounded in source material",
                        segment.Text,
                        0.8f
                    ));
                }
            }
        }

        // Calculate overall score
        var hasRisk = warnings.Count > 0;
        var confidence = hasRisk ? 1.0f - (warnings.Count * 0.2f) : 0.9f;
        var severity = DetermineSeverity(warnings);

        return new HallucinationResult(hasRisk, confidence, warnings, severity);
    }

    public async Task<ToolValidationResult> ValidateToolCallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken ct = default)
    {
        // Tool-specific validation logic
        var result = await ValidateArgumentsAgainstSchema(toolName, arguments, ct);
        return result;
    }

    private static HallucinationSeverity DetermineSeverity(List<HallucinationWarning> warnings)
    {
        if (warnings.Count == 0) return HallucinationSeverity.Low;
        if (warnings.Any(w => w.Confidence > 0.9f)) return HallucinationSeverity.Critical;
        if (warnings.Any(w => w.Confidence > 0.7f)) return HallucinationSeverity.High;
        if (warnings.Any(w => w.Confidence > 0.5f)) return HallucinationSeverity.Medium;
        return HallucinationSeverity.Low;
    }

    private Task<ToolValidationResult> ValidateArgumentsAgainstSchema(
        string toolName,
        JsonElement arguments,
        CancellationToken ct)
    {
        // Implementation would validate against tool schema
        // Check for fabricated parameters, invalid values, etc.
        return Task.FromResult(ToolValidationResult.Valid);
    }
}

public record ToolValidationResult(
    bool IsValid,
    IReadOnlyList<string>? Errors = null,
    IReadOnlyList<string>? Warnings = null
)
{
    public static ToolValidationResult Valid => new(true);
    public static ToolValidationResult Invalid(params string[] errors) => new(false, errors);
}
```

### .NET Confidence Scoring from Logprobs

```csharp
public class ConfidenceScorer
{
    private readonly float _lowThreshold;
    private readonly float _highThreshold;

    public ConfidenceScorer(float lowThreshold = 0.3f, float highThreshold = 0.7f)
    {
        _lowThreshold = lowThreshold;
        _highThreshold = highThreshold;
    }

    /// <summary>
    /// Calculates confidence from token log probabilities
    /// </summary>
    public ConfidenceScore CalculateFromLogprobs(IReadOnlyList<TokenLogProbability> logprobs)
    {
        if (logprobs.Count == 0)
            return new ConfidenceScore(0.5f, ConfidenceLevel.Unknown);

        // Convert log probabilities to probabilities
        var probabilities = logprobs
            .Select(lp => Math.Exp(lp.LogProbability))
            .ToList();

        // Calculate metrics
        var average = probabilities.Average();
        var minimum = probabilities.Min();
        var variance = probabilities.Select(p => Math.Pow(p - average, 2)).Average();

        // Determine confidence level
        var level = average switch
        {
            > 0.9f => ConfidenceLevel.VeryHigh,
            > 0.7f => ConfidenceLevel.High,
            > 0.5f => ConfidenceLevel.Medium,
            > 0.3f => ConfidenceLevel.Low,
            _ => ConfidenceLevel.VeryLow
        };

        return new ConfidenceScore(average, level, minimum, (float)Math.Sqrt(variance));
    }

    /// <summary>
    /// Identifies low-confidence segments in generated text
    /// </summary>
    public IReadOnlyList<LowConfidenceSegment> IdentifyLowConfidenceSegments(
        IReadOnlyList<TokenLogProbability> logprobs,
        float threshold = 0.5f)
    {
        var segments = new List<LowConfidenceSegment>();
        var currentStart = -1;
        var currentText = new StringBuilder();

        for (int i = 0; i < logprobs.Count; i++)
        {
            var prob = Math.Exp(logprobs[i].LogProbability);

            if (prob < threshold)
            {
                if (currentStart == -1)
                    currentStart = i;
                currentText.Append(logprobs[i].Token);
            }
            else if (currentStart != -1)
            {
                segments.Add(new LowConfidenceSegment(
                    currentStart,
                    i - 1,
                    currentText.ToString(),
                    (float)prob
                ));
                currentStart = -1;
                currentText.Clear();
            }
        }

        // Handle segment at end
        if (currentStart != -1)
        {
            segments.Add(new LowConfidenceSegment(
                currentStart,
                logprobs.Count - 1,
                currentText.ToString(),
                0 // Placeholder
            ));
        }

        return segments;
    }
}

public record TokenLogProbability(string Token, float LogProbability);

public record ConfidenceScore(
    float AverageProbability,
    ConfidenceLevel Level,
    float MinimumProbability,
    float StandardDeviation
);

public record LowConfidenceSegment(
    int StartIndex,
    int EndIndex,
    string Text,
    float Probability
);

public enum ConfidenceLevel
{
    VeryLow,
    Low,
    Medium,
    High,
    VeryHigh,
    Unknown
}
```

### .NET Fact-Checking Integration

```csharp
public interface IFactChecker
{
    Task<FactCheckResult> VerifyAsync(
        string claim,
        CancellationToken ct = default);
}

public record FactCheckResult(
    bool IsVerifiable,
    bool? IsTrue,
    float Confidence,
    string? Correction,
    IReadOnlyList<string>? Sources
);

public class CompositeFactChecker : IFactChecker
{
    private readonly IEnumerable<IFactChecker> _checkers;
    private readonly ILogger<CompositeFactChecker> _logger;

    public CompositeFactChecker(
        IEnumerable<IFactChecker> checkers,
        ILogger<CompositeFactChecker> logger)
    {
        _checkers = checkers;
        _logger = logger;
    }

    public async Task<FactCheckResult> VerifyAsync(string claim, CancellationToken ct = default)
    {
        var results = new List<FactCheckResult>();

        foreach (var checker in _checkers)
        {
            try
            {
                var result = await checker.VerifyAsync(claim, ct);
                results.Add(result);

                // If high confidence result, return early
                if (result.Confidence > 0.9f)
                    return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Fact checker {Checker} failed", checker.GetType().Name);
            }
        }

        // Aggregate results
        if (results.Count == 0)
            return new FactCheckResult(false, null, 0, null, null);

        var avgConfidence = results.Average(r => r.Confidence);
        var consensusTrue = results.Where(r => r.IsTrue.HasValue)
            .GroupBy(r => r.IsTrue)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        return new FactCheckResult(
            true,
            consensusTrue,
            avgConfidence,
            results.FirstOrDefault(r => !string.IsNullOrEmpty(r.Correction))?.Correction,
            results.SelectMany(r => r.Sources ?? Array.Empty<string>()).ToList()
        );
    }
}
```

### .NET Agent Integration Pattern

```csharp
public class HallucinationAwareAgentRunner
{
    private readonly IHallucinationDetector _detector;
    private readonly IFactChecker _factChecker;
    private readonly ILogger<HallucinationAwareAgentRunner> _logger;
    private readonly HallucinationOptions _options;

    public HallucinationAwareAgentRunner(
        IHallucinationDetector detector,
        IFactChecker factChecker,
        ILogger<HallucinationAwareAgentRunner> logger,
        HallucinationOptions options)
    {
        _detector = detector;
        _factChecker = factChecker;
        _logger = logger;
        _options = options;
    }

    public async Task<AgentResponse> RunWithValidationAsync(
        AgentRequest request,
        CancellationToken ct = default)
    {
        // Get model response
        var response = await GetModelResponseAsync(request, ct);

        // Analyze for hallucinations
        var analysis = await _detector.AnalyzeAsync(
            response.Content,
            new HallucinationContext(
                request.Query,
                request.SourceDocuments,
                request.ConversationHistory
            ),
            ct
        );

        // Handle based on severity
        switch (analysis.Severity)
        {
            case HallucinationSeverity.Critical:
                _logger.LogWarning("Critical hallucination detected, blocking response");
                return AgentResponse.Blocked(
                    "Response blocked due to accuracy concerns. Please rephrase your query."
                );

            case HallucinationSeverity.High:
                _logger.LogInformation("High hallucination risk detected, adding disclaimer");
                return response with
                {
                    Content = $"⚠️ Note: Some aspects of this response may be inaccurate.\n\n{response.Content}",
                    Flags = new[] { "hallucination_risk" }
                };

            case HallucinationSeverity.Medium:
                // Optionally fact-check claims
                if (_options.AutoFactCheck)
                {
                    var claims = ExtractClaims(response.Content);
                    foreach (var claim in claims)
                    {
                        var factResult = await _factChecker.VerifyAsync(claim, ct);
                        if (factResult.IsTrue == false && factResult.Correction != null)
                        {
                            response = response with
                            {
                                Content = response.Content.Replace(claim, factResult.Correction)
                            };
                        }
                    }
                }
                return response;

            default:
                return response;
        }
    }

    private IEnumerable<string> ExtractClaims(string content)
    {
        // Simple claim extraction - could use NLP library
        var sentences = content.Split('.', '!', '?');
        return sentences.Where(s => s.Trim().Length > 20); // Filter out fragments
    }

    private Task<AgentResponse> GetModelResponseAsync(AgentRequest request, CancellationToken ct)
    {
        // Placeholder - actual implementation would call LLM
        return Task.FromResult(new AgentResponse("Response content"));
    }
}

public record AgentRequest(
    string Query,
    IReadOnlyList<string>? SourceDocuments,
    IReadOnlyList<ChatMessage>? ConversationHistory
);

public record AgentResponse(
    string Content,
    IReadOnlyList<string>? Flags = null
)
{
    public static AgentResponse Blocked(string reason) => new(reason, new[] { "blocked" });
}

public record ChatMessage(string Role, string Content);

public class HallucinationOptions
{
    public bool AutoFactCheck { get; set; } = false;
    public float ConfidenceThreshold { get; set; } = 0.7f;
    public bool BlockCritical { get; set; } = true;
    public bool AddDisclaimers { get; set; } = true;
}
```

---

## Expected Deliverables

### 1. Core Interfaces

```csharp
public interface IHallucinationDetector
{
    Task<HallucinationResult> AnalyzeAsync(
        string content,
        HallucinationContext? context = null,
        CancellationToken ct = default);

    Task<ToolValidationResult> ValidateToolCallAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken ct = default);
}

public interface IFactChecker
{
    Task<FactCheckResult> VerifyAsync(string claim, CancellationToken ct = default);
}
```

### 2. Implementations
- Azure Groundedness Checker
- Confidence Scorer from logprobs
- Composite fact-checker
- Agent runner integration

### 3. Configuration

```csharp
public class HallucinationOptions
{
    public bool AutoFactCheck { get; set; } = false;
    public float ConfidenceThreshold { get; set; } = 0.7f;
    public bool BlockCritical { get; set; } = true;
    public bool AddDisclaimers { get; set; } = true;
    public bool UseLogprobs { get; set; } = true;
}
```

### 4. Metrics
- Hallucination detection rate
- False positive rate
- Fact-check accuracy
- Response blocking rate

---

## Prevention Best Practices

1. **System Prompts**
   - Instruct model to acknowledge uncertainty
   - Ask for citations when making claims
   - Encourage "I don't know" responses

2. **Temperature Settings**
   - Lower temperature for factual queries
   - Higher temperature for creative tasks
   - Dynamic adjustment based on query type

3. **Retrieval Augmentation**
   - Provide source documents for context
   - Enable groundedness checking
   - Cite sources in responses

4. **Structured Outputs**
   - Use JSON mode for structured data
   - Validate outputs against schemas
   - Enforce type constraints

---

## Validation Checklist

- [ ] Hallucination detector interface defined
- [ ] Azure groundedness integration implemented
- [ ] Confidence scorer from logprobs working
- [ ] Fact-checker integration created
- [ ] Agent runner integration complete
- [ ] Blocking and disclaimer logic tested
- [ ] Metrics and logging configured
- [ ] Configuration options documented

---

## Status

- [ ] TypeScript patterns analyzed
- [ ] .NET interfaces designed
- [ ] Groundedness checker implemented
- [ ] Confidence scorer created
- [ ] Agent integration complete
- [ ] Fact-checking integration added
- [ ] Testing performed
