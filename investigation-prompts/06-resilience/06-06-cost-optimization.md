# Investigation: Cost Optimization

**Status**: Pending
**Priority**: High
**Category**: Resilience

---

## Source Files

### TypeScript Reference
- **Token Estimation**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/utils/tokens.ts
- **Context Pruning**: https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/context/pruning.ts

Note: The 01_05_agent codebase has basic token estimation. This investigation defines comprehensive cost optimization patterns.

---

## Microsoft Documentation

### Primary References
- **Azure OpenAI Pricing**: https://azure.microsoft.com/pricing/details/openai-service/
- **Token Usage Monitoring**: https://learn.microsoft.com/azure/ai-foundry/openai/how-to/monitoring
- **Semantic Kernel - Cost Management**: https://learn.microsoft.com/semantic-kernel/

### Related Patterns
- **Model Selection**: Choosing cost-effective models
- **Token Optimization**: Reducing token usage
- **Budget Enforcement**: Preventing cost overruns

---

## Investigation Questions

### 1. Cost Fundamentals
- What are the main cost drivers for AI agents?
- How do different models compare in pricing (GPT-4 vs GPT-3.5)?
- What is the cost breakdown of input vs output tokens?
- How do tool calls affect costs?

### 2. Token Optimization
- How to accurately estimate token counts before API calls?
- What is the impact of context window size on costs?
- How to implement effective context pruning?
- When to use summarization vs truncation?

### 3. Model Selection
- When to use cheaper models for simple tasks?
- How to implement dynamic model selection?
- What criteria determine model choice?
- How to handle model fallbacks?

### 4. Budget Management
- How to implement per-user cost limits?
- How to track cumulative costs in real-time?
- What to do when budget is exceeded?
- How to allocate budgets across sessions?

### 5. Monitoring and Reporting
- How to track token usage per request?
- How to aggregate costs per user/session?
- What metrics indicate cost anomalies?
- How to forecast future costs?

### 6. .NET Implementation Patterns
- How to design `ICostTracker` interface?
- How to integrate with dependency injection?
- How to implement budget enforcement middleware?
- How to expose metrics for monitoring?

---

## Code Patterns to Analyze

### .NET Cost Tracking Interface

```csharp
/// <summary>
/// Tracks and manages AI operation costs
/// </summary>
public interface ICostTracker
{
    /// <summary>
    /// Estimates the cost of an operation before execution
    /// </summary>
    Task<CostEstimate> EstimateAsync(
        string modelId,
        int inputTokens,
        int? maxOutputTokens = null,
        CancellationToken ct = default);

    /// <summary>
    /// Records actual usage after an operation
    /// </summary>
    Task<UsageRecord> RecordUsageAsync(
        string modelId,
        TokenUsage usage,
        string? sessionId = null,
        string? userId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets current spending for a user or session
    /// </summary>
    Task<SpendingSummary> GetSpendingAsync(
        string? userId = null,
        string? sessionId = null,
        TimeSpan? period = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a user/session has remaining budget
    /// </summary>
    Task<BudgetCheckResult> CheckBudgetAsync(
        string? userId = null,
        string? sessionId = null,
        CancellationToken ct = default);
}

public record CostEstimate(
    decimal EstimatedCost,
    int InputTokens,
    int EstimatedOutputTokens,
    string Currency,
    string ModelId
);

public record TokenUsage(
    int InputTokens,
    int OutputTokens,
    int? CachedTokens = null
);

public record UsageRecord(
    string RecordId,
    string ModelId,
    TokenUsage Usage,
    decimal ActualCost,
    string Currency,
    DateTimeOffset Timestamp,
    string? SessionId,
    string? UserId
);

public record SpendingSummary(
    decimal TotalCost,
    int TotalInputTokens,
    int TotalOutputTokens,
    int RequestCount,
    IReadOnlyDictionary<string, decimal> CostByModel
);

public record BudgetCheckResult(
    bool HasBudget,
    decimal RemainingBudget,
    decimal SpentAmount,
    decimal BudgetLimit,
    string? Message
);
```

### .NET Cost Tracker Implementation

```csharp
public class InMemoryCostTracker : ICostTracker
{
    private readonly ConcurrentDictionary<string, List<UsageRecord>> _recordsByUser;
    private readonly ConcurrentDictionary<string, List<UsageRecord>> _recordsBySession;
    private readonly ConcurrentBag<UsageRecord> _allRecords;
    private readonly IModelPricingProvider _pricingProvider;
    private readonly IBudgetProvider _budgetProvider;
    private readonly ILogger<InMemoryCostTracker> _logger;

    public InMemoryCostTracker(
        IModelPricingProvider pricingProvider,
        IBudgetProvider budgetProvider,
        ILogger<InMemoryCostTracker> logger)
    {
        _pricingProvider = pricingProvider;
        _budgetProvider = budgetProvider;
        _logger = logger;
        _recordsByUser = new ConcurrentDictionary<string, List<UsageRecord>>();
        _recordsBySession = new ConcurrentDictionary<string, List<UsageRecord>>();
        _allRecords = new ConcurrentBag<UsageRecord>();
    }

    public async Task<CostEstimate> EstimateAsync(
        string modelId,
        int inputTokens,
        int? maxOutputTokens = null,
        CancellationToken ct = default)
    {
        var pricing = await _pricingProvider.GetPricingAsync(modelId, ct);
        var estimatedOutput = maxOutputTokens ?? inputTokens; // Assume output ~ input

        var inputCost = inputTokens * pricing.InputCostPerToken;
        var outputCost = estimatedOutput * pricing.OutputCostPerToken;

        return new CostEstimate(
            inputCost + outputCost,
            inputTokens,
            estimatedOutput,
            pricing.Currency,
            modelId
        );
    }

    public async Task<UsageRecord> RecordUsageAsync(
        string modelId,
        TokenUsage usage,
        string? sessionId = null,
        string? userId = null,
        CancellationToken ct = default)
    {
        var pricing = await _pricingProvider.GetPricingAsync(modelId, ct);

        var inputCost = usage.InputTokens * pricing.InputCostPerToken;
        var outputCost = usage.OutputTokens * pricing.OutputCostPerToken;
        var cachedDiscount = (usage.CachedTokens ?? 0) * pricing.CachedCostPerToken;
        var totalCost = inputCost + outputCost - cachedDiscount;

        var record = new UsageRecord(
            $"usage_{Guid.NewGuid():N}",
            modelId,
            usage,
            totalCost,
            pricing.Currency,
            DateTimeOffset.UtcNow,
            sessionId,
            userId
        );

        _allRecords.Add(record);

        if (userId is not null)
        {
            _recordsByUser.AddOrUpdate(
                userId,
                _ => new List<UsageRecord> { record },
                (_, list) => { lock (list) { list.Add(record); } return list; }
            );
        }

        if (sessionId is not null)
        {
            _recordsBySession.AddOrUpdate(
                sessionId,
                _ => new List<UsageRecord> { record },
                (_, list) => { lock (list) { list.Add(record); } return list; }
            );
        }

        _logger.LogInformation(
            "Recorded usage: {ModelId}, Input={InputTokens}, Output={OutputTokens}, Cost={Cost}",
            modelId, usage.InputTokens, usage.OutputTokens, totalCost);

        return record;
    }

    public Task<SpendingSummary> GetSpendingAsync(
        string? userId = null,
        string? sessionId = null,
        TimeSpan? period = null,
        CancellationToken ct = default)
    {
        var records = GetRecordsForScope(userId, sessionId);
        var cutoff = period.HasValue ? DateTimeOffset.UtcNow - period.Value : DateTimeOffset.MinValue;

        var filteredRecords = records
            .Where(r => r.Timestamp >= cutoff)
            .ToList();

        if (!filteredRecords.Any())
            return Task.FromResult(new SpendingSummary(0, 0, 0, 0, new Dictionary<string, decimal>()));

        var summary = new SpendingSummary(
            filteredRecords.Sum(r => r.ActualCost),
            filteredRecords.Sum(r => r.Usage.InputTokens),
            filteredRecords.Sum(r => r.Usage.OutputTokens),
            filteredRecords.Count,
            filteredRecords
                .GroupBy(r => r.ModelId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.ActualCost))
        );

        return Task.FromResult(summary);
    }

    public async Task<BudgetCheckResult> CheckBudgetAsync(
        string? userId = null,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        var budget = await _budgetProvider.GetBudgetAsync(userId, sessionId, ct);

        if (budget == null || budget.Limit == 0)
            return new BudgetCheckResult(true, decimal.MaxValue, 0, 0, null);

        var spending = await GetSpendingAsync(userId, sessionId, budget.Period, ct);
        var remaining = budget.Limit - spending.TotalCost;

        var hasBudget = remaining > 0;
        var message = hasBudget
            ? null
            : $"Budget exceeded. Limit: {budget.Limit}, Spent: {spending.TotalCost}";

        return new BudgetCheckResult(hasBudget, remaining, spending.TotalCost, budget.Limit, message);
    }

    private List<UsageRecord> GetRecordsForScope(string? userId, string? sessionId)
    {
        if (userId is not null && _recordsByUser.TryGetValue(userId, out var userRecords))
            return userRecords;

        if (sessionId is not null && _recordsBySession.TryGetValue(sessionId, out var sessionRecords))
            return sessionRecords;

        return _allRecords.ToList();
    }
}
```

### .NET Model Pricing Provider

```csharp
public interface IModelPricingProvider
{
    Task<ModelPricing> GetPricingAsync(string modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelPricing>> GetAllPricingAsync(CancellationToken ct = default);
}

public record ModelPricing(
    string ModelId,
    decimal InputCostPerToken,
    decimal OutputCostPerToken,
    decimal CachedCostPerToken,
    string Currency,
    int MaxContextTokens
);

public class StaticModelPricingProvider : IModelPricingProvider
{
    private readonly Dictionary<string, ModelPricing> _pricing;

    public StaticModelPricingProvider()
    {
        // Pricing as of 2024 (example values - update with current rates)
        _pricing = new Dictionary<string, ModelPricing>
        {
            ["gpt-4o"] = new ModelPricing("gpt-4o", 0.000005m, 0.000015m, 0.0000025m, "USD", 128000),
            ["gpt-4o-mini"] = new ModelPricing("gpt-4o-mini", 0.00000015m, 0.0000006m, 0.000000075m, "USD", 128000),
            ["gpt-4-turbo"] = new ModelPricing("gpt-4-turbo", 0.00001m, 0.00003m, 0.000005m, "USD", 128000),
            ["gpt-35-turbo"] = new ModelPricing("gpt-35-turbo", 0.0000005m, 0.0000015m, 0.00000025m, "USD", 16385),
            ["claude-3-opus"] = new ModelPricing("claude-3-opus", 0.000015m, 0.000075m, 0, "USD", 200000),
            ["claude-3-sonnet"] = new ModelPricing("claude-3-sonnet", 0.000003m, 0.000015m, 0, "USD", 200000),
            ["claude-3-haiku"] = new ModelPricing("claude-3-haiku", 0.00000025m, 0.00000125m, 0, "USD", 200000),
        };
    }

    public Task<ModelPricing> GetPricingAsync(string modelId, CancellationToken ct = default)
    {
        // Normalize model ID
        var normalizedId = modelId.ToLowerInvariant().Trim();

        if (_pricing.TryGetValue(normalizedId, out var pricing))
            return Task.FromResult(pricing);

        // Return default pricing for unknown models
        return Task.FromResult(new ModelPricing(normalizedId, 0.00001m, 0.00003m, 0, "USD", 4096));
    }

    public Task<IReadOnlyList<ModelPricing>> GetAllPricingAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<ModelPricing>>(_pricing.Values.ToList());
    }
}
```

### .NET Budget Enforcement Middleware

```csharp
public class BudgetEnforcementMiddleware
{
    private readonly ICostTracker _costTracker;
    private readonly ILogger<BudgetEnforcementMiddleware> _logger;

    public BudgetEnforcementMiddleware(
        ICostTracker costTracker,
        ILogger<BudgetEnforcementMiddleware> logger)
    {
        _costTracker = costTracker;
        _logger = logger;
    }

    public async Task<ChatResponse> ExecuteAsync(
        ChatRequest request,
        Func<Task<ChatResponse>> next,
        CancellationToken ct = default)
    {
        // Check budget before making request
        var budgetCheck = await _costTracker.CheckBudgetAsync(
            request.UserId,
            request.SessionId,
            ct);

        if (!budgetCheck.HasBudget)
        {
            _logger.LogWarning(
                "Budget exceeded for user {UserId}: {Spent}/{Limit}",
                request.UserId, budgetCheck.SpentAmount, budgetCheck.BudgetLimit);

            return ChatResponse.BudgetExceeded(budgetCheck.Message ?? "Budget exceeded");
        }

        // Execute the request
        var response = await next();

        // Record actual usage
        if (response.Usage != null)
        {
            await _costTracker.RecordUsageAsync(
                response.ModelId,
                response.Usage,
                request.SessionId,
                request.UserId,
                ct);
        }

        // Include remaining budget in response metadata
        return response with
        {
            Metadata = new ResponseMetadata(
                RemainingBudget: budgetCheck.RemainingBudget,
                TotalCost: response.Usage != null
                    ? await CalculateCost(response.ModelId, response.Usage)
                    : 0
            )
        };
    }

    private async Task<decimal> CalculateCost(string modelId, TokenUsage usage)
    {
        var estimate = await _costTracker.EstimateAsync(modelId, usage.InputTokens);
        return estimate.EstimatedCost;
    }
}

public record ChatRequest(
    string Content,
    string? UserId = null,
    string? SessionId = null
);

public record ChatResponse(
    string Content,
    string ModelId,
    TokenUsage? Usage = null,
    ResponseMetadata? Metadata = null
)
{
    public static ChatResponse BudgetExceeded(string message) =>
        new(message, "none", null, new ResponseMetadata(0, 0, true));
}

public record ResponseMetadata(
    decimal RemainingBudget,
    decimal TotalCost,
    bool BudgetExceeded = false
);
```

### .NET Dynamic Model Selection

```csharp
public interface IModelSelector
{
    Task<string> SelectModelAsync(
        ModelSelectionCriteria criteria,
        CancellationToken ct = default);
}

public record ModelSelectionCriteria(
    string? Query,
    int EstimatedTokens,
    ComplexityLevel Complexity,
    bool RequiresTools,
    bool RequiresVision,
    decimal? BudgetRemaining
);

public enum ComplexityLevel
{
    Simple,     // Basic queries, summarization
    Moderate,   // Analysis, reasoning
    Complex     // Multi-step, complex reasoning
}

public class CostAwareModelSelector : IModelSelector
{
    private readonly IModelPricingProvider _pricingProvider;
    private readonly ICostTracker _costTracker;
    private readonly ILogger<CostAwareModelSelector> _logger;

    public CostAwareModelSelector(
        IModelPricingProvider pricingProvider,
        ICostTracker costTracker,
        ILogger<CostAwareModelSelector> logger)
    {
        _pricingProvider = pricingProvider;
        _costTracker = costTracker;
        _logger = logger;
    }

    public async Task<string> SelectModelAsync(
        ModelSelectionCriteria criteria,
        CancellationToken ct = default)
    {
        var allPricing = await _pricingProvider.GetAllPricingAsync(ct);

        // Filter by capabilities
        var candidates = allPricing
            .Where(p => criteria.RequiresVision || !p.ModelId.Contains("vision"))
            .Where(p => p.MaxContextTokens >= criteria.EstimatedTokens)
            .ToList();

        // If budget is tight, prefer cheaper models
        if (criteria.BudgetRemaining.HasValue && criteria.BudgetRemaining.Value < 0.01m)
        {
            var cheapest = candidates.OrderBy(p => p.InputCostPerToken).First();
            _logger.LogInformation("Selected cheapest model {ModelId} due to low budget", cheapest.ModelId);
            return cheapest.ModelId;
        }

        // Select based on complexity
        var selected = criteria.Complexity switch
        {
            ComplexityLevel.Simple => candidates
                .Where(p => p.InputCostPerToken < 0.000001m) // Cheap models
                .OrderByDescending(p => p.MaxContextTokens)
                .FirstOrDefault() ?? candidates.First(),

            ComplexityLevel.Moderate => candidates
                .Where(p => p.InputCostPerToken < 0.00001m) // Mid-range
                .OrderByDescending(p => p.MaxContextTokens)
                .FirstOrDefault() ?? candidates.First(),

            ComplexityLevel.Complex => candidates
                .OrderBy(p => p.InputCostPerToken) // Best available
                .Last(),

            _ => candidates.First()
        };

        _logger.LogInformation(
            "Selected model {ModelId} for complexity {Complexity}",
            selected.ModelId, criteria.Complexity);

        return selected.ModelId;
    }
}
```

---

## Architecture Diagram: Cost Management

```
+------------+     +----------------+     +------------------+
|  Request   |---->| Model Selector |--->| Budget Check     |
+------------+     +----------------+     +------------------+
                          |                       |
                          v                       v
                   +-------------+         +-------------+
                   | Model       |         | Budget      |
                   | Pricing     |         | Provider    |
                   +-------------+         +-------------+
                                                  |
                          +-----------------------+
                          |
                          v
+------------+     +----------------+     +------------------+
|   Model    |---->| Response with  |---->| Record Usage     |
|   API      |     | Token Usage    |     | to Cost Tracker  |
+------------+     +----------------+     +------------------+
                                                  |
                          +-----------------------+
                          |
                          v
                   +-------------+         +-------------+
                   | Usage       |<------->| Cost        |
                   | Reports     |         | Alerts      |
                   +-------------+         +-------------+
```

---

## Expected Deliverables

### 1. Core Interfaces

```csharp
public interface ICostTracker
{
    Task<CostEstimate> EstimateAsync(string modelId, int inputTokens, int? maxOutputTokens = null, CancellationToken ct = default);
    Task<UsageRecord> RecordUsageAsync(string modelId, TokenUsage usage, string? sessionId = null, string? userId = null, CancellationToken ct = default);
    Task<SpendingSummary> GetSpendingAsync(string? userId = null, string? sessionId = null, TimeSpan? period = null, CancellationToken ct = default);
    Task<BudgetCheckResult> CheckBudgetAsync(string? userId = null, string? sessionId = null, CancellationToken ct = default);
}

public interface IModelPricingProvider
{
    Task<ModelPricing> GetPricingAsync(string modelId, CancellationToken ct = default);
    Task<IReadOnlyList<ModelPricing>> GetAllPricingAsync(CancellationToken ct = default);
}

public interface IBudgetProvider
{
    Task<Budget?> GetBudgetAsync(string? userId, string? sessionId, CancellationToken ct = default);
}

public interface IModelSelector
{
    Task<string> SelectModelAsync(ModelSelectionCriteria criteria, CancellationToken ct = default);
}
```

### 2. Configuration

```csharp
public class CostOptions
{
    public decimal DefaultDailyBudget { get; set; } = 10.0m;
    public decimal DefaultSessionBudget { get; set; } = 1.0m;
    public bool EnableBudgetEnforcement { get; set; } = true;
    public bool EnableDynamicModelSelection { get; set; } = true;
    public bool TrackPerUserCosts { get; set; } = true;
    public string Currency { get; set; } = "USD";
}
```

### 3. Metrics
- Requests by model
- Tokens by model (input/output)
- Cost by model
- Budget utilization rate
- Model selection distribution

---

## Cost Optimization Strategies

1. **Context Pruning**
   - Remove redundant messages
   - Summarize old conversation
   - Use semantic caching

2. **Model Selection**
   - Use cheaper models for simple tasks
   - Cascade from cheap to expensive
   - Consider latency requirements

3. **Token Reduction**
   - Shorter system prompts
   - Compressed responses
   - Efficient tool schemas

4. **Caching**
   - Cache common responses
   - Embedding caching
   - Semantic similarity matching

---

## Validation Checklist

- [ ] Cost tracker interface defined
- [ ] Model pricing provider implemented
- [ ] Budget enforcement middleware created
- [ ] Dynamic model selector implemented
- [ ] Usage recording working
- [ ] Spending reports available
- [ ] Budget alerts configured
- [ ] Metrics exposed

---

## Status

- [ ] TypeScript patterns analyzed
- [ ] .NET interfaces designed
- [ ] Cost tracker implemented
- [ ] Pricing provider created
- [ ] Budget middleware integrated
- [ ] Model selector working
- [ ] Reporting endpoints added
- [ ] Load testing performed
