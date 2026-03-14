using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoAgent;

public sealed class ResponsesApiClient(Configuration config, HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ResponsesResponse> ChatAsync(
        List<ApiMessage> messages,
        List<ToolDefinition>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = config.Model,
            ["input"] = messages
        };

        if (tools is { Count: > 0 })
            body["tools"] = tools;

        body["max_output_tokens"] = config.MaxOutputTokens;

        using var request = new HttpRequestMessage(HttpMethod.Post, config.ApiEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(json, JsonOptions);
            throw new Exception(error?.Error?.Message ?? $"API error ({response.StatusCode})");
        }

        var data = JsonSerializer.Deserialize<ResponsesResponse>(json, JsonOptions)
            ?? throw new Exception("Failed to parse response");

        if (data.Usage != null)
        {
            StatsTracker.Record(new UsageStats
            {
                InputTokens = data.Usage.InputTokens,
                OutputTokens = data.Usage.OutputTokens
            });
        }

        return data;
    }
}

// API models

public sealed class ApiMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }
    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

public sealed class FunctionCallOutput
{
    [JsonPropertyName("type")]
    public string Type { get; } = "function_call_output";
    [JsonPropertyName("call_id")]
    public required string CallId { get; init; }
    [JsonPropertyName("output")]
    public required string Output { get; init; }
}

public sealed class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "function";
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    [JsonPropertyName("description")]
    public string? Description { get; init; }
    [JsonPropertyName("parameters")]
    public object? Parameters { get; init; }
    [JsonPropertyName("strict")]
    public bool Strict { get; init; }
}

public sealed class ResponsesResponse
{
    [JsonPropertyName("output")]
    public List<OutputItem>? Output { get; init; }
    [JsonPropertyName("usage")]
    public UsageResponse? Usage { get; init; }
}

public sealed class OutputItem
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }
    [JsonPropertyName("id")]
    public string? Id { get; init; }
    [JsonPropertyName("call_id")]
    public string? CallId { get; init; }
    [JsonPropertyName("name")]
    public string? Name { get; init; }
    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }
    [JsonPropertyName("content")]
    public List<ContentPart>? Content { get; init; }
}

public sealed class ContentPart
{
    [JsonPropertyName("type")]
    public string? Type { get; init; }
    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed class UsageResponse
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }
    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

public sealed class ErrorResponse
{
    [JsonPropertyName("error")]
    public ErrorDetail? Error { get; init; }
}

public sealed class ErrorDetail
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
