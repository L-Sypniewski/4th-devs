# Async/Await Patterns in C# / .NET 10

## Basic Async Method

```javascript
// JavaScript
async function fetchData(url) {
  const response = await fetch(url);
  const data = await response.json();
  return data;
}
```

```csharp
// C#
public async Task<DataType> FetchDataAsync(string url)
{
    var data = await _httpClient.GetFromJsonAsync<DataType>(url);
    return data ?? throw new InvalidOperationException("Failed to deserialize response");
}
```

## Async with CancellationToken

```csharp
public async Task<ChatResponse> ChatAsync(
    string input,
    IReadOnlyList<Message>? history = null,
    CancellationToken cancellationToken = default)
{
    using var response = await _httpClient.SendAsync(request, cancellationToken);
    var data = await response.Content.ReadFromJsonAsync<ResponsesResponse>(_jsonOptions, cancellationToken);
    // ...
}
```

## Fire and Forget (Top-level)

```javascript
// JavaScript
main().catch(error => {
  console.error(`Error: ${error.message}`);
  process.exit(1);
});
```

```csharp
// C# - Program.cs
public static async Task<int> Main()
{
    try
    {
        await RunAsync();
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}
```

## IDisposable with Async

```csharp
public sealed class ChatClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public ChatClient()
    {
        _httpClient = new HttpClient();
    }

    public async Task<ChatResponse> ChatAsync(string input)
    {
        // Async operations using _httpClient
    }

    public void Dispose() => _httpClient.Dispose();
}

// Usage
using var client = new ChatClient(config);
var response = await client.ChatAsync("Hello");
```

## IAsyncDisposable (for async cleanup)

```csharp
public sealed class AsyncChatClient : IAsyncDisposable
{
    private readonly HttpClient _httpClient;

    public async ValueTask DisposeAsync()
    {
        // Async cleanup if needed
        _httpClient.Dispose();
    }
}

// Usage
await using var client = new AsyncChatClient(config);
var response = await client.ChatAsync("Hello");
```

## Parallel Async Operations

```javascript
// JavaScript - Promise.all
const [users, posts] = await Promise.all([
  fetchUsers(),
  fetchPosts()
]);
```

```csharp
// C# - Task.WhenAll
var usersTask = FetchUsersAsync();
var postsTask = FetchPostsAsync();
await Task.WhenAll(usersTask, postsTask);

var users = await usersTask;
var posts = await postsTask;

// Or with value tuples
var (users, posts) = await (
    FetchUsersAsync(),
    FetchPostsAsync()
);
```

## Async Streams

```csharp
// IAsyncEnumerable for streaming data
public async IAsyncEnumerable<ChatChunk> StreamChatAsync(
    string input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    await foreach (var chunk in _httpClient.GetFromJsonAsAsyncEnumerable<ChatChunk>(url, cancellationToken))
    {
        yield return chunk;
    }
}

// Usage
await foreach (var chunk in client.StreamChatAsync("Hello"))
{
    Console.WriteLine(chunk.Text);
}
```

## ConfigureAwait

For library code, use `ConfigureAwait(false)` to avoid deadlocks:

```csharp
public async Task<string> GetDataAsync()
{
    var response = await _httpClient.GetAsync(url).ConfigureAwait(false);
    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    return content;
}
```

**Note:** In .NET 10 modern apps, `ConfigureAwait(false)` is less critical but still recommended for library code.

## Common Pitfalls

### Don't use .Result or .Wait()

```csharp
// ❌ BAD - can cause deadlocks
var result = SomeAsyncMethod().Result;

// ✅ GOOD - use await
var result = await SomeAsyncMethod();
```

### Don't async void (except event handlers)

```csharp
// ❌ BAD - exceptions are lost
public async void DoSomething()
{
    await SomeAsyncMethod();
}

// ✅ GOOD - returns Task
public async Task DoSomethingAsync()
{
    await SomeAsyncMethod();
}
```

### Prefer ValueTask for sync-over-async

```csharp
// When method might complete synchronously
public ValueTask<string> GetDataAsync()
{
    if (_cachedValue is not null)
        return new ValueTask<string>(_cachedValue);  // No allocation

    return new ValueTask<string>(FetchFromNetworkAsync());
}
```
