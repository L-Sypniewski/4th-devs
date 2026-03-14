using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractionExample;

/// <summary>
/// Client for interacting with the AI Responses API.
/// </summary>
public sealed class ChatClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Configuration _config;
    private readonly string _model;
    private readonly JsonSerializerOptions _jsonOptions;

    public ChatClient(Configuration config)
    {
        _config = config;
        _model = config.ResolveModelForProvider("gpt-5.2");

        _httpClient = new HttpClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Sends a chat message to the API and returns the response.
    /// </summary>
    /// <param name="input">The user input.</param>
    /// <param name="history">Optional conversation history.</param>
    /// <returns>The chat response.</returns>
    public async Task<ChatResponse> ChatAsync(string input, IReadOnlyList<Message>? history = null)
    {
        var messages = (history ?? [])
            .Select(m => new ApiMessage { Role = m.Role, Content = m.Content })
            .ToList();

        messages.Add(new ApiMessage { Role = "user", Content = input });

        var requestBody = new ResponsesRequest
        {
            Model = _model,
            Input = messages,
            Reasoning = new ReasoningConfig { Effort = "medium" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _config.ApiEndpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _config.ApiKey);

        foreach (var (key, value) in _config.ExtraHeaders)
        {
            request.Headers.TryAddWithoutValidation(key, value);
        }

        var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = JsonSerializer.Deserialize<ErrorResponse>(responseContent, _jsonOptions);
            var message = errorResponse?.Error?.Message ?? $"Request failed with status {response.StatusCode}";
            throw new InvalidOperationException(message);
        }

        var data = JsonSerializer.Deserialize<ResponsesResponse>(responseContent, _jsonOptions);

        var text = ResponseHelpers.ExtractResponseText(data?.OutputText, data?.Output);
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException("Missing text output in API response");
        }

        var reasoningTokens = data?.Usage?.OutputTokensDetails?.ReasoningTokens ?? 0;

        return new ChatResponse(text, reasoningTokens);
    }

    public void Dispose() => _httpClient.Dispose();
}

// Request models
file sealed record ResponsesRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required List<ApiMessage> Input { get; init; }

    [JsonPropertyName("reasoning")]
    public ReasoningConfig? Reasoning { get; init; }
}

file sealed record ReasoningConfig
{
    [JsonPropertyName("effort")]
    public string? Effort { get; init; }
}

// Response models
file sealed record ResponsesResponse
{
    [JsonPropertyName("output_text")]
    public string? OutputText { get; init; }

    [JsonPropertyName("output")]
    public List<OutputItem>? Output { get; init; }

    [JsonPropertyName("usage")]
    public Usage? Usage { get; init; }
}

file sealed record Usage
{
    [JsonPropertyName("output_tokens_details")]
    public OutputTokensDetails? OutputTokensDetails { get; init; }
}

file sealed record OutputTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; init; }
}

file sealed record ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorInfo? Error { get; init; }
}

file sealed record ErrorInfo
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
