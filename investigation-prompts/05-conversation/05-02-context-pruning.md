# Investigation Prompt: Context Window Management and Pruning

**Created**: 2026-03-16
**Focus**: Context window management, token counting, and message pruning strategies

---

## Source Files

Investigate the following TypeScript source files for context management patterns:

### Primary Files to Analyze

| File Path | Purpose |
|-----------|---------|
| `packages/core/src/runner.ts` | Main execution runner with context handling |
| `packages/core/src/context.ts` | Context management implementation |
| `packages/core/src/utils/token-counter.ts` | Token counting utilities |
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

## Investigation Tasks

- [ ] Locate TypeScript context management implementation
- [ ] Document token counting approach used
- [ ] Identify pruning algorithm and thresholds
- [ ] Note how system messages are preserved
- [ ] Document any caching of token counts
- [ ] Design .NET token counter interface
- [ ] Implement TikToken-based token counter
- [ ] Create pruning strategy interfaces
- [ ] Implement sliding window pruner
- [ ] Implement priority-based pruner
- [ ] Add unit tests for all components

---

## Notes

- Consider using Microsoft.ML.Tokenizers or TikToken.NET for token counting
- Different models have different tokenizers - ensure model-aware counting
- Consider caching token counts for unchanged messages
- Document trade-offs between different pruning strategies
- Consider providing configuration for pruning behavior
