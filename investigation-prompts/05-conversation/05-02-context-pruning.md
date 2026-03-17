# Investigation Prompt: Context Window Management and Pruning

**Created**: 2026-03-16
**Focus**: Context window management, token counting, and message pruning strategies

---

## Source Files

Investigate the following TypeScript source files for context management patterns:

### Primary Files to Analyze

| File Path | Purpose |
|-----------|---------|
| `01_05_agent/src/runtime/runner.ts` | Main execution runner with context handling |
| `01_05_agent/src/runtime/context.ts` | Context management implementation |
| `01_05_agent/src/utils/tokens.ts` | Token counting utilities |
| `01_05_agent/src/utils/pruning.ts` | Pruning logic implementation |
| Any files containing `prune`, `truncate`, `context` | Related functionality |

### Search Patterns

```bash
# Find context management files
find . -type f -name "*.ts" | xargs grep -l "context" | head -20

# Find pruning/truncation logic
grep -r "prune\|truncate" --include="*.ts" | head -20

# Find token counting
grep -r "token\|count" --include="*.ts" | grep -i "context\|message" | head -20

# Find context window limits
grep -r "max.*token\|context.*limit\|window.*size" --include="*.ts" | head -20
```

---

## Investigation Questions

### 1. How does TS handle context window limits?

- What is the maximum context window size?
- How are messages counted toward the limit?
- Is token counting done locally or via API?
- What happens when the limit is exceeded?

### 2. What strategies exist for pruning conversation history?

| Strategy | Description | Trade-offs |
|----------|-------------|------------|
| Sliding Window | Keep last N messages | Simple but loses early context |
| Importance-based | Prioritize important messages | Complex but preserves key info |
| Turn-based | Keep last N turns | Balances user/assistant pairs |
| Semantic | Keep semantically relevant messages | Requires embeddings |
| Summary-based | Replace old messages with summary | Requires LLM calls |

### 3. How to implement sliding window or importance-based pruning in .NET?

```csharp
// Sliding Window Implementation
public class SlidingWindowPruner : IContextPruner
{
    private readonly int _maxMessages;

    public IReadOnlyList<Message> Prune(IReadOnlyList<Message> messages)
    {
        if (messages.Count <= _maxMessages)
            return messages;

        return messages.Skip(messages.Count - _maxMessages).ToList();
    }
}

// Importance-based Implementation
public class ImportanceBasedPruner : IContextPruner
{
    private readonly int _maxTokens;
    private readonly ITokenCounter _tokenCounter;

    public IReadOnlyList<Message> Prune(IReadOnlyList<Message> messages)
    {
        // Score messages by importance
        // Keep system messages, recent messages, and high-scoring messages
        // Fit within token budget
    }
}
```

### 4. How are system prompts preserved during pruning?

- Are system messages marked as non-removable?
- Is there a priority system for message types?
- How are tool definitions and system instructions handled?

---

## Context Window Limits

### Input/Output Token Split

Models have **separate limits** for input (prompt) tokens vs output (completion) tokens. This is a critical distinction often misunderstood:

| Model | Max Input Tokens | Max Output Tokens |
|-------|------------------|-------------------|
| Claude 3.5 Sonnet | 200,000 | 8,192 |
| Claude 3.5 Haiku | 200,000 | 8,192 |
| GPT-4o | 128,000 | 4,096 (varies) |
| GPT-4o-mini | 128,000 | 16,384 |

**Key Insight:** The available context for your prompt is calculated as:

```
available_context = max_input - current_tokens - reserved_output
```

You must **reserve space** for the model's response. If you send a prompt that uses all 200K input tokens, the model has no room to generate a response.

### Context Window Calculation

```csharp
// Example: Claude 3.5 Sonnet
int maxInputTokens = 200_000;
int maxOutputTokens = 8_192;
int reservedOutput = 4_000; // Reserve space for expected response

int CalculateAvailableTokens(int currentPromptTokens)
{
    return maxInputTokens - currentPromptTokens - reservedOutput;
}
```

**Why reserve output space?**
- Prevents request failures when the model needs to generate a long response
- Ensures the model has enough context to complete multi-step reasoning
- Avoids truncation of code blocks, JSON responses, or detailed explanations

### C# Interface Pattern

```csharp
/// <summary>
/// Calculates available context window space accounting for input/output limits.
/// </summary>
public interface IContextWindowCalculator
{
    /// <summary>
    /// Maximum tokens allowed in the input (prompt).
    /// </summary>
    int MaxInputTokens { get; }

    /// <summary>
    /// Maximum tokens allowed in the output (completion).
    /// </summary>
    int MaxOutputTokens { get; }

    /// <summary>
    /// Calculates remaining tokens available for additional content.
    /// </summary>
    /// <param name="currentTokens">Current number of tokens in the prompt.</param>
    /// <param name="reservedOutput">Optional output reservation. Defaults to sensible default.</param>
    /// <returns>Number of tokens available before hitting the input limit.</returns>
    int CalculateAvailableTokens(int currentTokens, int? reservedOutput = null);

    /// <summary>
    /// Determines if the current context exceeds available space.
    /// </summary>
    bool IsOverLimit(int currentTokens, int? reservedOutput = null);
}

public record ModelContextLimits(int MaxInput, int MaxOutput);

public class ContextWindowCalculator : IContextWindowCalculator
{
    private readonly ModelContextLimits _limits;
    private readonly int _defaultReservedOutput;

    public ContextWindowCalculator(ModelContextLimits limits, int defaultReservedOutput = 4000)
    {
        _limits = limits;
        _defaultReservedOutput = defaultReservedOutput;
    }

    public int MaxInputTokens => _limits.MaxInput;
    public int MaxOutputTokens => _limits.MaxOutput;

    public int CalculateAvailableTokens(int currentTokens, int? reservedOutput = null)
    {
        var reserved = reservedOutput ?? _defaultReservedOutput;
        var available = MaxInputTokens - currentTokens - reserved;
        return Math.Max(0, available);
    }

    public bool IsOverLimit(int currentTokens, int? reservedOutput = null)
    {
        var reserved = reservedOutput ?? _defaultReservedOutput;
        return (currentTokens + reserved) > MaxInputTokens;
    }
}

// Predefined limits for common models
public static class ModelLimits
{
    public static readonly ModelContextLimits Claude35Sonnet = new(200_000, 8_192);
    public static readonly ModelContextLimits Claude35Haiku = new(200_000, 8_192);
    public static readonly ModelContextLimits GPT4o = new(128_000, 4_096);
    public static readonly ModelContextLimits GPT4oMini = new(128_000, 16_384);
}
```

### Usage Example

```csharp
// Setup
var calculator = new ContextWindowCalculator(
    ModelLimits.Claude35Sonnet,
    defaultReservedOutput: 4000
);

// Check before adding content
var currentTokens = tokenCounter.CountTokens(messages);
var available = calculator.CalculateAvailableTokens(currentTokens);

if (available < estimatedNewTokens)
{
    // Need to prune context
    var pruneResult = pruner.Prune(messages, calculator.MaxInputTokens - 4000);
}
```

---

## Code Patterns to Investigate

### Token Counting Pattern

```typescript
// Look for patterns like:
function countTokens(messages: Message[]): number {
  return messages.reduce((total, msg) => {
    return total + estimateTokens(msg.content);
  }, 0);
}

// How are tokens estimated?
// - Local tokenizer library?
// - Approximation based on character count?
// - API-based counting?
```

### Token Estimation Constants (from S01E05)

The TypeScript implementation uses these constants for character-based token estimation:

```typescript
const CHARS_PER_TOKEN = 3.5  // Conservative multiplier (estimates run slightly high)

// Per-item overhead for role tags, separators, etc.
chars += 20  // overhead per item
```

**Key Points:**
- ~4 chars per token is typical for English text
- Using 3.5 as a conservative multiplier provides a safety margin
- 20 characters overhead per message item (accounts for role markers, JSON structure)
- No explicit 20% buffer mentioned in code, but the conservative multiplier implicitly provides margin

### Message Pruning Pattern

```typescript
// Look for patterns like:
function pruneMessages(
  messages: Message[],
  maxTokens: number
): Message[] {
  // What algorithm is used?
  // How are system messages handled?
  // What is the pruning threshold?
}
```

### Priority-Based Retention Pattern

```typescript
// Look for patterns like:
interface PrioritizedMessage extends Message {
  priority: 'high' | 'medium' | 'low';
  retain: boolean; // Always keep this message
}

// How is priority determined?
// - Message role (system = high)?
// - Contains code/commands?
// - User-marked as important?
```

---

## Expected Deliverables

### 1. Token Counter Interface and Implementation

```csharp
public interface ITokenCounter
{
    int CountTokens(string text);
    int CountTokens(IReadOnlyList<Message> messages);
}

public class TikTokenCounter : ITokenCounter
{
    private readonly string _modelName;

    public TikTokenCounter(string modelName)
    {
        _modelName = modelName;
    }

    public int CountTokens(string text)
    {
        // Use TikToken or similar library
    }

    public int CountTokens(IReadOnlyList<Message> messages)
    {
        return messages.Sum(m => CountTokens(m.Content) + MessageOverhead);
    }
}
```

### 2. Context Pruner Interface and Implementations

```csharp
public interface IContextPruner
{
    PruneResult Prune(IReadOnlyList<Message> messages, int maxTokens);
}

public record PruneResult(
    IReadOnlyList<Message> RetainedMessages,
    int OriginalTokenCount,
    int PrunedTokenCount,
    int MessagesRemoved
);

public class SlidingWindowContextPruner : IContextPruner
{
    private readonly ITokenCounter _tokenCounter;
    private readonly bool _preserveSystemMessages;

    public PruneResult Prune(IReadOnlyList<Message> messages, int maxTokens)
    {
        // Implementation
    }
}

public class PriorityContextPruner : IContextPruner
{
    private readonly ITokenCounter _tokenCounter;
    private readonly IMessageScorer _scorer;

    public PruneResult Prune(IReadOnlyList<Message> messages, int maxTokens)
    {
        // Score messages by importance
        // Keep system messages and high-priority messages
        // Fit within token budget
    }
}
```

### 3. Message Priority/Scoring System

```csharp
public enum MessagePriority
{
    Critical,   // System messages, never removed
    High,       // Important context, rarely removed
    Medium,     // Normal messages
    Low         // Old or less relevant messages
}

public interface IMessageScorer
{
    MessagePriority Score(Message message, IReadOnlyList<Message> context);
}

public class DefaultMessageScorer : IMessageScorer
{
    public MessagePriority Score(Message message, IReadOnlyList<Message> context)
    {
        return message.Role switch
        {
            MessageRole.System => MessagePriority.Critical,
            MessageRole.User when IsRecent(message) => MessagePriority.High,
            MessageRole.Assistant when ContainsCode(message) => MessagePriority.High,
            _ => MessagePriority.Medium
        };
    }
}
```

### 4. Context Management Service

```csharp
public interface IContextManager
{
    ContextManagementResult ManageContext(
        IReadOnlyList<Message> messages,
        int maxTokens
    );
}

public record ContextManagementResult(
    IReadOnlyList<Message> OptimizedMessages,
    bool WasPruned,
    int TokensSaved
);

public class ContextManager : IContextManager
{
    private readonly ITokenCounter _tokenCounter;
    private readonly IContextPruner _pruner;

    public ContextManagementResult ManageContext(
        IReadOnlyList<Message> messages,
        int maxTokens)
    {
        var currentTokens = _tokenCounter.CountTokens(messages);

        if (currentTokens <= maxTokens)
        {
            return new ContextManagementResult(messages, false, 0);
        }

        var result = _pruner.Prune(messages, maxTokens);
        return new ContextManagementResult(
            result.RetainedMessages,
            true,
            result.OriginalTokenCount - result.PrunedTokenCount
        );
    }
}
```

---

## Architecture Validation: 4-Step Pruning Algorithm

The TypeScript implementation uses a specific 4-step pruning algorithm documented in the architecture (`src/utils/pruning.ts:100-159`):

### Step 1: Truncate Large Outputs
```typescript
const { items: truncated, truncatedCount } = truncateLargeOutputs(items, config.maxToolOutputChars)
```
**Purpose:** Reduce individual tool output sizes before token estimation.
**Default:** `maxToolOutputChars: 10_000`

### Step 2: Estimate Tokens
```typescript
let estimate = estimateConversationTokens(truncated, systemPrompt)
if (estimate <= targetTokens) {
  return { items: truncated, estimatedTokens: estimate, droppedCount: 0, truncatedCount, droppedItems: [] }
}
```
**Purpose:** Calculate total token count using character-based estimation.
**Formula:** `chars / 3.5 + 20 per item overhead`

### Step 3: Identify Turns
```typescript
const turns = identifyTurns(truncated)
const keepFirst = turns[0]
const keepRecent = turns.slice(-config.minRecentTurns)
const droppable = turns.slice(1, -config.minRecentTurns)
```
**Purpose:** Group items into turns and identify which can be dropped.
**Strategy:** Always keep first turn + last N turns, others are droppable.

### Step 4: Drop Oldest Droppable
```typescript
while (estimate > targetTokens && droppable.length > 0) {
  // Drop oldest droppable turn
  // Re-estimate
}
```
**Purpose:** Iteratively drop oldest turns until under budget.

### Configuration: PruningThresholds
```typescript
interface PruningThresholds {
  threshold: number           // Trigger at 85% context usage
  targetUtilization: number   // Aim for 50% after pruning
  minRecentTurns: number      // Always keep 3-5 recent turns
  maxToolOutputChars: number  // Truncate tool outputs > 10000 chars
  enableSummarization: boolean // Optional: summarize dropped content
}

const DEFAULT_PRUNING: PruningThresholds = {
  threshold: 0.85,
  targetUtilization: 0.50,
  minRecentTurns: 3,
  maxToolOutputChars: 10_000,
  enableSummarization: true,
}
```

### C# Implementation of 4-Step Algorithm
```csharp
public class ArchitectureValidPruner : IContextPruner
{
    private readonly PruningConfig _config;

    public PruneResult Prune(IReadOnlyList<Item> items, string? systemPrompt, int contextWindow)
    {
        var targetTokens = (int)(contextWindow * _config.TargetUtilization);

        // Step 1: Truncate large outputs
        var (truncated, truncatedCount) = TruncateLargeOutputs(items, _config.MaxToolOutputChars);

        // Step 2: Estimate tokens
        var estimate = EstimateTokens(truncated, systemPrompt);
        if (estimate <= targetTokens)
            return new(truncated, estimate, 0, truncatedCount, []);

        // Step 3: Identify turns
        var turns = IdentifyTurns(truncated);
        var keepFirst = turns[0];
        var keepRecent = turns.TakeLast(_config.MinRecentTurns);
        var droppable = turns.Skip(1).SkipLast(_config.MinRecentTurns);

        // Step 4: Drop oldest droppable
        var dropped = new List<Turn>();
        while (estimate > targetTokens && droppable.Any())
        {
            var oldest = droppable.First();
            droppable = droppable.Skip(1);
            dropped.Add(oldest);
            estimate = ReEstimate(truncated, dropped);
        }

        var result = BuildResult(truncated, dropped);
        return new(result.Items, estimate, dropped.Count, truncatedCount, dropped);
    }
}

public record PruningConfig(
    double Threshold = 0.85,
    double TargetUtilization = 0.50,
    int MinRecentTurns = 3,
    int MaxToolOutputChars = 10_000,
    bool EnableSummarization = true
);
```

**Validation against Architecture:**
- [x] Step 1 (truncateLargeOutputs): Documented with C# implementation
- [x] Step 2 (estimateTokens): Documented with character-based estimation
- [x] Step 3 (identifyTurns): Documented with turn grouping strategy
- [x] Step 4 (drop oldest): Documented with iterative loop
- [x] PruningThresholds: Documented with C# config record

---

## Investigation Tasks

- [x] Locate TypeScript context management implementation
- [x] Document token counting approach used
- [x] Identify pruning algorithm and thresholds
- [x] Note how system messages are preserved
- [ ] Document any caching of token counts
- [x] Design .NET token counter interface
- [ ] Implement TikToken-based token counter
- [x] Create pruning strategy interfaces
- [x] Implement sliding window pruner
- [x] Implement priority-based pruner
- [ ] Add unit tests for all components

---

## Notes

- Consider using Microsoft.ML.Tokenizers or TikToken.NET for token counting
- Different models have different tokenizers - ensure model-aware counting
- Consider caching token counts for unchanged messages
- Document trade-offs between different pruning strategies
- Consider providing configuration for pruning behavior
- **Architecture Validation**: The 4-step algorithm from `src/utils/pruning.ts` has been validated and documented
