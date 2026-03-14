# JSON Serialization Patterns for C# / .NET 10

## JsonSerializerOptions Setup

```csharp
private readonly JsonSerializerOptions _jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true
};
```

## Common Patterns

### Simple Object → Record

```javascript
// JavaScript
const message = { role: "user", content: "Hello" };
```

```csharp
// C#
public sealed record Message(string Role, string Content);

// Usage
var message = new Message(Role: "user", Content: "Hello");
var json = JsonSerializer.Serialize(message, _jsonOptions);
// {"role":"user","content":"Hello"}
```

### Object with Computed Property

```javascript
// JavaScript
const apiMessage = { type: "message", role: "user", content: "Hello" };
```

```csharp
// C#
public sealed record ApiMessage
{
    [JsonPropertyName("type")]
    public string Type => "message";  // Computed, always "message"

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}
```

### Nested Objects

```javascript
// JavaScript
const request = {
  model: "gpt-4",
  input: [{ type: "message", role: "user", content: "Hi" }],
  reasoning: { effort: "medium" }
};
```

```csharp
// C#
public sealed record ResponsesRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required List<ApiMessage> Input { get; init; }

    [JsonPropertyName("reasoning")]
    public ReasoningConfig? Reasoning { get; init; }
}

public sealed record ReasoningConfig
{
    [JsonPropertyName("effort")]
    public string? Effort { get; init; }
}
```

### Optional Properties (nullable)

```javascript
// JavaScript - optional fields
const config = {
  searchContextSize: maybeValue,  // might be undefined
  engine: anotherMaybeValue
};
```

```csharp
// C# - nullable properties with JsonIgnoreCondition
public sealed record WebSearchConfig
{
    [JsonPropertyName("search_context_size")]
    public string? SearchContextSize { get; init; }

    [JsonPropertyName("engine")]
    public string? Engine { get; init; }
}
// With DefaultIgnoreCondition.WhenWritingNull, null values won't be serialized
```

### Array of Mixed Types

```javascript
// JavaScript
const output = [
  { type: "message", content: [...] },
  { type: "other", data: "..." }
];
```

```csharp
// C# - use base class or union types
[JsonPolymorphism(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MessageOutput), "message")]
[JsonDerivedType(typeof(OtherOutput), "other")]
public abstract record OutputItem();

public sealed record MessageOutput : OutputItem
{
    [JsonPropertyName("content")]
    public List<ContentPart>? Content { get; init; }
}

public sealed record OtherOutput : OutputItem
{
    [JsonPropertyName("data")]
    public string? Data { get; init; }
}
```

### Deserializing Responses

```csharp
// Deserialize to typed object
var response = await httpClient.GetStringAsync(url);
var data = JsonSerializer.Deserialize<ResponsesResponse>(response, _jsonOptions);

// Safe navigation with null-conditional
var text = data?.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text;
```

### Error Response Pattern

```csharp
public sealed record ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; init; }
}

public sealed record ErrorInfo
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }
}

// Usage
if (!response.IsSuccessStatusCode)
{
    var error = JsonSerializer.Deserialize<ErrorResponse>(content, _jsonOptions);
    var message = error?.Error?.Message ?? $"Request failed: {response.StatusCode}";
    throw new InvalidOperationException(message);
}
```

## HttpClient JSON Extensions

```csharp
// POST with JSON body
using var response = await _httpClient.PostAsJsonAsync(url, requestBody, _jsonOptions);

// Read as JSON
var data = await response.Content.ReadFromJsonAsync<ResponseType>(_jsonOptions);

// Manual approach (more control)
var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
using var content = new StringContent(json, Encoding.UTF8, "application/json");
using var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = content });
```

## Property Naming Policies

| Policy | C# Property | JSON Key |
|--------|-------------|----------|
| `SnakeCaseLower` | `OutputText` | `output_text` |
| `SnakeCaseUpper` | `OutputText` | `OUTPUT_TEXT` |
| `CamelCase` | `OutputText` | `outputText` |
| `null` (default) | `OutputText` | `OutputText` |

For API compatibility with OpenAI/OpenRouter, use `SnakeCaseLower`.
