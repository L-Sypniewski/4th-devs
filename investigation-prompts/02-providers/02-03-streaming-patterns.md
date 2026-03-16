# Investigation: Streaming Response Patterns

**Status**: Pending
**Priority**: High
**Category**: Core Feature

---

## Source Files

### TypeScript Streaming Implementation

#### Runner Streaming
- [runtime/runner.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/runtime/runner.ts)
  - `executeTurnStream()` function (lines 664-722)
  - `runAgentStream()` function (lines 951-1077)

#### Provider Stream Types
- [providers/types.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/types.ts)
  - `ProviderStreamEvent` type (lines 46-58)

#### OpenAI Stream Adapter
- [providers/openai/adapter.ts](https://github.com/i-am-alice/4th-devs/blob/main/01_05_agent/src/providers/openai/adapter.ts)
  - Stream accumulation (lines 156-216)
  - Event mapping (lines 259-350)

---

## Microsoft Documentation

### Primary Reference
- [StreamingChatCompletionUpdate](https://learn.microsoft.com/dotnet/api/microsoft.extensions.ai.streamingchatcompletionupdate)

### Related Documentation
- [IAsyncEnumerable in C#](https://learn.microsoft.com/dotnet/csharp/asynchronous-programming/generate-consume-asynchronous-streams)
- [CancellationToken usage](https://learn.microsoft.com/dotnet/standard/threading/cancellation-in-managed-threads)

---

## TypeScript Streaming Patterns

### Provider Stream Event Types
```typescript
export type ProviderStreamEvent =
  // Text content streaming
  | { type: 'text_delta'; delta: string }
  | { type: 'text_done'; text: string }
  // Function call streaming
  | { type: 'function_call_delta'; callId: string; name: string; argumentsDelta: string }
  | { type: 'function_call_done'; callId: string; name: string; arguments: Record<string, unknown> }
  // Reasoning/thinking
  | { type: 'reasoning_delta'; delta: string }
  | { type: 'reasoning_done'; text: string }
  // Lifecycle
  | { type: 'done'; response: ProviderResponse }
  | { type: 'error'; error: string; code?: string }
```

### Async Generator Pattern
```typescript
async function* executeTurnStream(
  agent: Agent,
  runtime: RuntimeContext,
  exec: ExecutionContext,
  session: Session,
  signal?: AbortSignal
): AsyncGenerator<ProviderStreamEvent, TurnResult, unknown> {
  // Prepare input...
  const { provider, model, input } = prepared.data

  let response: ProviderResponse | undefined

  // Iterate over provider stream
  for await (const event of provider.stream({ model, input, signal })) {
    yield event  // Pass through to consumer

    if (event.type === 'done') {
      response = event.response
    }

    if (event.type === 'error') {
      return { continue: false, error: event.error }
    }
  }

  // Return final result
  return handleTurnResponse(response, agent, runtime, exec, turnNumber)
}
```

### Stream Accumulation (OpenAI)
```typescript
interface StreamState {
  output: OutputItem[]
  fnCallMeta: Map<number, { callId: string; name: string }>
}

function accumulate(state: StreamState, event: StreamEvent): void {
  switch (event.type) {
    case 'response.output_item.added':
      state.output[event.output_index] = event.item
      break
    case 'response.output_text.delta':
      // Append text to existing content
      content.text += event.delta
      break
    case 'response.function_call_arguments.delta':
      output.arguments += event.delta
      break
  }
}
```

---

## Investigation Questions

### 1. TypeScript Streaming (Async Generators)
- How does TS handle streaming responses?
- What is the `AsyncIterable<T>` interface?
- How are errors propagated in streams?

### 2. .NET Equivalent (IAsyncEnumerable)
- What is the .NET `IAsyncEnumerable<T>` pattern?
- How does `CompleteStreamingAsync()` return updates?
- How do `await foreach` and `ConfigureAwait` work?

### 3. Update Aggregation
- How are streaming updates aggregated into final response?
- What state needs to be accumulated?
- How to handle partial function call arguments?

### 4. Cancellation
- How to handle `CancellationToken` in streaming?
- What happens when cancellation is requested mid-stream?
- How does this compare to `AbortSignal` in TS?

---

## C# Streaming Patterns

### IAsyncEnumerable Pattern
```csharp
// Expected IChatClient streaming method
public interface IChatClient
{
    Task<ChatResponse> CompleteAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Consuming a Stream
```csharp
public async Task ProcessStreamAsync(
    IChatClient client,
    string prompt,
    CancellationToken cancellationToken = default)
{
    var messages = new List<ChatMessage>
    {
        new(ChatRole.User, prompt)
    };

    var fullResponse = new StringBuilder();

    await foreach (var update in client.CompleteStreamingAsync(messages, cancellationToken: cancellationToken))
    {
        if (update.Content is string text)
        {
            fullResponse.Append(text);
            Console.Write(text); // Real-time output
        }

        if (update.FunctionCallUpdate is { } fcUpdate)
        {
            // Handle streaming function call arguments
        }
    }

    Console.WriteLine("\nComplete!");
}
```

### Stream Aggregation
```csharp
public class StreamAggregator
{
    private readonly StringBuilder _textBuilder = new();
    private readonly Dictionary<int, FunctionCallBuilder> _functionCalls = new();
    private ChatResponse? _finalResponse;

    public void ProcessUpdate(StreamingChatCompletionUpdate update)
    {
        // Accumulate text content
        if (update.Content is string text)
        {
            _textBuilder.Append(text);
        }

        // Accumulate function call arguments
        if (update.FunctionCallUpdate is { } fc)
        {
            var builder = _functionCalls.GetOrAdd(fc.Index, _ => new FunctionCallBuilder(fc.Name, fc.Id));
            builder.AppendArguments(fc.ArgumentsDelta);
        }
    }

    public ChatResponse BuildResponse()
    {
        return new ChatResponse
        {
            Messages = new List<ChatMessage>
            {
                new(ChatRole.Assistant, _textBuilder.ToString())
                {
                    FunctionCalls = _functionCalls.Values.Select(b => b.Build()).ToList()
                }
            }
        };
    }
}
```

---

## Cancellation Patterns

### TypeScript (AbortSignal)
```typescript
// Provider request
interface ProviderRequest {
  signal?: AbortSignal
}

// Usage in stream
if (signal?.aborted) {
  return { continue: false, error: 'Operation aborted' }
}

// Check during iteration
for await (const event of provider.stream({ signal })) {
  // Stream respects AbortSignal
}
```

### C# (CancellationToken)
```csharp
public async IAsyncEnumerable<StreamingUpdate> StreamAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // Check cancellation at start
    cancellationToken.ThrowIfCancellationRequested();

    await foreach (var update in innerStream.WithCancellation(cancellationToken))
    {
        // Check before yielding
        cancellationToken.ThrowIfCancellationRequested();
        yield return update;
    }
}

// Consumer usage
try
{
    await foreach (var update in client.StreamAsync(cancellationToken: cts.Token))
    {
        // Process update
    }
}
catch (OperationCanceledException)
{
    // Handle cancellation gracefully
}
```

---

## Pattern Comparison

| Aspect | TypeScript | C# / .NET |
|--------|------------|-----------|
| Return type | `AsyncIterable<T>` | `IAsyncEnumerable<T>` |
| Iteration | `for await (const x of stream)` | `await foreach (var x in stream)` |
| Cancellation | `AbortSignal` | `CancellationToken` |
| Error propagation | Throw in generator | Throw from enumerator |
| Aggregation | Manual state tracking | Manual or SDK helper |
| Configuration | `ConfigureAwait(false)` | N/A in TS |

---

## Expected Deliverables

### 1. Streaming Pattern Comparison Document
- Complete mapping of TS patterns to C#
- Code examples for each pattern
- Best practices for both platforms

### 2. C# Async Enumerable Example
```csharp
public async IAsyncEnumerable<ProviderStreamEvent> StreamAsync(
    ChatRequest request,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var update in _client.CompleteStreamingAsync(
        request.Messages,
        request.Options,
        cancellationToken))
    {
        // Map .NET update to our event type
        yield return MapToUpdate(update);
    }
}
```

### 3. Stream Aggregation Helper
- Class to accumulate streaming updates
- Support for text, function calls, reasoning
- Build final `ProviderResponse` from accumulated state

### 4. Cancellation Wrapper
- Extension methods for cancellation-aware streaming
- Graceful shutdown patterns
- Timeout handling

---

## Notes

- `IAsyncEnumerable` requires `System.Runtime.CompilerServices` for `[EnumeratorCancellation]`
- Consider using `ConfigureAwait(false)` in library code
- May need to handle `OperationCanceledException` vs `TaskCanceledException`
- Test cancellation mid-stream for proper resource cleanup
