# Investigation Prompt: Conversation Summarization Patterns

**Created**: 2026-03-16
**Focus**: Conversation summarization, trigger conditions, and LLM integration

---

## Source Files

Investigate the following TypeScript source files for summarization patterns:

### Primary Files to Analyze

| File Path | Purpose |
|-----------|---------|
| `packages/core/src/summarization/` | Summarization service implementation |
| `packages/core/src/services/summary*` | Summary-related services |
| Any files containing `summarize`, `summary`, `compress` | Related functionality |

### Search Patterns

```bash
# Find summarization files
find . -type f -name "*.ts" | xargs grep -l "summar" | head -20

# Find compression/context reduction
grep -r "compress\|reduce\|summarize" --include="*.ts" | head -20

# Find summary triggers
grep -r "trigger\|threshold\|condition" --include="*.ts" | grep -i "summar" | head -20

# Find summary storage
grep -r "summary.*store\|save.*summary\|cache.*summary" --include="*.ts" | head -20
```

---

## Investigation Questions

### 1. How does TS generate conversation summaries?

- What LLM prompts are used for summarization?
- What information is included in summaries?
- How is the summary formatted (JSON, markdown, plain text)?
- Is summarization done in a single call or multiple steps?

### 2. When is summarization triggered?

| Trigger Type | Description | Implementation |
|--------------|-------------|----------------|
| Token Threshold | When tokens exceed limit | `currentTokens > maxTokens * 0.8` |
| Message Count | When message count exceeds limit | `messageCount > maxMessages` |
| Time-based | Periodic summarization | Scheduled task |
| Manual | User-initiated | Command/interface call |
| Context Window Pressure | Before context overflow | Proactive check |

### 3. How to implement LLM-based summarization in .NET?

```csharp
public interface IConversationSummarizer
{
    Task<SummaryResult> SummarizeAsync(
        IReadOnlyList<Message> messages,
        SummarizationOptions? options = null
    );
}

public record SummaryResult(
    string Summary,
    int OriginalTokenCount,
    int SummaryTokenCount,
    IReadOnlyList<Guid> SummarizedMessageIds
);

public record SummarizationOptions(
    int? MaxSummaryTokens = null,
    string? FocusArea = null,
    bool PreserveKeyPoints = true
);
```

### 4. How to store and retrieve summaries?

- Are summaries stored as special messages in the conversation?
- Is there a separate summary table/collection?
- How are summaries linked to original messages?
- Can summaries be regenerated?

---

## Code Patterns to Investigate

### Summary Generation Pattern

```typescript
// Look for patterns like:
async function generateSummary(messages: Message[]): Promise<string> {
  const prompt = buildSummaryPrompt(messages);
  const response = await llm.complete(prompt);
  return response.content;
}

// What prompts are used?
function buildSummaryPrompt(messages: Message[]): string {
  return `
    Summarize the following conversation, preserving:
    - Key decisions made
    - Important context
    - Open questions
    - Action items

    Conversation:
    ${formatMessages(messages)}
  `;
}
```

### Trigger Condition Pattern

```typescript
// Look for patterns like:
interface SummarizationTrigger {
  shouldTrigger(messages: Message[], currentTokens: number): boolean;
}

class TokenThresholdTrigger implements SummarizationTrigger {
  constructor(private threshold: number) {}

  shouldTrigger(messages: Message[], currentTokens: number): boolean {
    return currentTokens > this.threshold;
  }
}
```

### Summary Storage Pattern

```typescript
// Look for patterns like:
interface ConversationSummary {
  id: string;
  sessionId: string;
  content: string;
  messageRange: {
    startMessageId: string;
    endMessageId: string;
  };
  createdAt: Date;
  tokenCount: number;
}

// How are summaries stored?
async storeSummary(summary: ConversationSummary): Promise<void>
```

---

## Expected Deliverables

### 1. Summarization Service Interface

```csharp
public interface IConversationSummarizer
{
    Task<SummaryResult> SummarizeAsync(
        IReadOnlyList<Message> messages,
        SummarizationOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

public record SummarizationOptions(
    int MaxSummaryTokens = 500,
    string? CustomPrompt = null,
    bool IncludeActionItems = true,
    bool IncludeKeyDecisions = true
);

public record SummaryResult(
    string SummaryText,
    int OriginalTokenCount,
    int SummaryTokenCount,
    double CompressionRatio,
    IReadOnlyList<Guid> CoveredMessageIds,
    DateTime CreatedAt
);
```

### 2. LLM-Based Summarizer Implementation

```csharp
public class LlmConversationSummarizer : IConversationSummarizer
{
    private readonly ILlmClient _llmClient;
    private readonly ITokenCounter _tokenCounter;
    private readonly string _systemPrompt;

    public LlmConversationSummarizer(
        ILlmClient llmClient,
        ITokenCounter tokenCounter)
    {
        _llmClient = llmClient;
        _tokenCounter = tokenCounter;
        _systemPrompt = BuildDefaultSystemPrompt();
    }

    public async Task<SummaryResult> SummarizeAsync(
        IReadOnlyList<Message> messages,
        SummarizationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SummarizationOptions();

        var originalTokens = _tokenCounter.CountTokens(messages);
        var prompt = BuildSummarizationPrompt(messages, options);

        var response = await _llmClient.CompleteAsync(
            prompt,
            new CompletionOptions
            {
                SystemPrompt = _systemPrompt,
                MaxTokens = options.MaxSummaryTokens
            },
            cancellationToken
        );

        var summaryTokens = _tokenCounter.CountTokens(response.Content);

        return new SummaryResult(
            response.Content,
            originalTokens,
            summaryTokens,
            (double)summaryTokens / originalTokens,
            messages.Select(m => m.Id).ToList(),
            DateTime.UtcNow
        );
    }

    private string BuildDefaultSystemPrompt()
    {
        return """
            You are a conversation summarizer. Create concise summaries that:
            1. Preserve key decisions and their rationale
            2. Maintain important context for future reference
            3. List any open questions or action items
            4. Keep the summary focused and relevant

            Format the summary in clear sections.
            """;
    }

    private string BuildSummarizationPrompt(
        IReadOnlyList<Message> messages,
        SummarizationOptions options)
    {
        var formattedMessages = FormatMessagesForSummary(messages);
        var customInstructions = options.CustomPrompt ?? "";

        return $"""
            {customInstructions}

            Please summarize the following conversation:

            {formattedMessages}

            Provide a summary covering:
            - Main topics discussed
            - Key decisions made
            {(options.IncludeActionItems ? "- Action items identified" : "")}
            {(options.IncludeKeyDecisions ? "- Important context to preserve" : "")}
            """;
    }
}
```

### 3. Summarization Trigger Service

```csharp
public interface ISummarizationTrigger
{
    bool ShouldTrigger(SummarizationContext context);
}

public record SummarizationContext(
    IReadOnlyList<Message> Messages,
    int CurrentTokenCount,
    int MaxTokenCount,
    DateTime LastSummarization,
    int MessageCountSinceLastSummary
);

public class CompositeTrigger : ISummarizationTrigger
{
    private readonly IReadOnlyList<ISummarizationTrigger> _triggers;

    public CompositeTrigger(IEnumerable<ISummarizationTrigger> triggers)
    {
        _triggers = triggers.ToList();
    }

    public bool ShouldTrigger(SummarizationContext context)
    {
        return _triggers.Any(t => t.ShouldTrigger(context));
    }
}

public class TokenThresholdTrigger : ISummarizationTrigger
{
    private readonly double _thresholdPercentage;

    public TokenThresholdTrigger(double thresholdPercentage = 0.75)
    {
        _thresholdPercentage = thresholdPercentage;
    }

    public bool ShouldTrigger(SummarizationContext context)
    {
        return context.CurrentTokenCount >= context.MaxTokenCount * _thresholdPercentage;
    }
}

public class MessageCountTrigger : ISummarizationTrigger
{
    private readonly int _messageThreshold;

    public MessageCountTrigger(int messageThreshold = 50)
    {
        _messageThreshold = messageThreshold;
    }

    public bool ShouldTrigger(SummarizationContext context)
    {
        return context.MessageCountSinceLastSummary >= _messageThreshold;
    }
}
```

### 4. Summary Storage

```csharp
public interface ISummaryRepository
{
    Task<ConversationSummary?> GetLatestAsync(Guid sessionId);
    Task<IReadOnlyList<ConversationSummary>> GetAllAsync(Guid sessionId);
    Task SaveAsync(ConversationSummary summary);
    Task DeleteAsync(Guid summaryId);
}

public class ConversationSummary
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid StartMessageId { get; set; }
    public Guid EndMessageId { get; set; }
    public int OriginalTokenCount { get; set; }
    public int SummaryTokenCount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation properties
    public Session Session { get; set; } = null!;
}

// EF Core Configuration
public class ConversationSummaryConfiguration : IEntityTypeConfiguration<ConversationSummary>
{
    public void Configure(EntityTypeBuilder<ConversationSummary> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Session)
            .WithMany()
            .HasForeignKey(s => s.SessionId);

        builder.HasIndex(s => s.SessionId);
        builder.HasIndex(s => s.CreatedAt);
    }
}
```

### 5. Integrated Summarization Manager

```csharp
public interface ISummarizationManager
{
    Task<SummarizationCheckResult> CheckAndSummarizeAsync(
        Guid sessionId,
        IReadOnlyList<Message> messages,
        int maxTokens,
        CancellationToken cancellationToken = default
    );
}

public record SummarizationCheckResult(
    bool SummarizationOccurred,
    ConversationSummary? NewSummary,
    IReadOnlyList<Message> UpdatedMessages
);

public class SummarizationManager : ISummarizationManager
{
    private readonly ISummarizationTrigger _trigger;
    private readonly IConversationSummarizer _summarizer;
    private readonly ISummaryRepository _repository;
    private readonly ITokenCounter _tokenCounter;

    public async Task<SummarizationCheckResult> CheckAndSummarizeAsync(
        Guid sessionId,
        IReadOnlyList<Message> messages,
        int maxTokens,
        CancellationToken cancellationToken = default)
    {
        var lastSummary = await _repository.GetLatestAsync(sessionId);
        var currentTokens = _tokenCounter.CountTokens(messages);

        var context = new SummarizationContext(
            messages,
            currentTokens,
            maxTokens,
            lastSummary?.CreatedAt ?? DateTime.MinValue,
            CountMessagesSince(lastSummary, messages)
        );

        if (!_trigger.ShouldTrigger(context))
        {
            return new SummarizationCheckResult(false, null, messages);
        }

        var messagesToSummarize = GetMessagesToSummarize(lastSummary, messages);
        var result = await _summarizer.SummarizeAsync(
            messagesToSummarize,
            cancellationToken: cancellationToken
        );

        var summary = new ConversationSummary
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Content = result.SummaryText,
            StartMessageId = messagesToSummarize.First().Id,
            EndMessageId = messagesToSummarize.Last().Id,
            OriginalTokenCount = result.OriginalTokenCount,
            SummaryTokenCount = result.SummaryTokenCount,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.SaveAsync(summary);

        // Return messages with summary replacing summarized portion
        var updatedMessages = BuildUpdatedMessages(messages, summary, messagesToSummarize);

        return new SummarizationCheckResult(true, summary, updatedMessages);
    }
}
```

---

## Investigation Tasks

- [ ] Locate TypeScript summarization implementation
- [ ] Document summarization prompts used
- [ ] Identify trigger conditions and thresholds
- [ ] Note summary storage approach
- [ ] Document summary message format
- [ ] Design .NET summarization interfaces
- [ ] Implement LLM-based summarizer
- [ ] Create trigger system
- [ ] Implement summary repository
- [ ] Create summarization manager
- [ ] Add unit tests for all components
- [ ] Add integration tests with actual LLM

---

## Notes

- Consider using dependency injection for LLM client to allow different providers
- Summary quality depends on prompt engineering - consider making prompts configurable
- Consider caching summaries to avoid redundant LLM calls
- Evaluate trade-offs between summary detail and token savings
- Consider hierarchical summaries for very long conversations
