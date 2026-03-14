using System.Text.Json.Serialization;

namespace AudioAgent.Models;

/// <summary>
/// API request and response models for Responses API.
/// </summary>
public sealed record ApiMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

public sealed record ApiFunctionCall
{
    [JsonPropertyName("type")]
    public string Type => "function_call";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("arguments")]
    public required string Arguments { get; init; }

    [JsonPropertyName("call_id")]
    public required string CallId { get; init; }
}

public sealed record ApiFunctionCallOutput
{
    [JsonPropertyName("type")]
    public string Type => "function_call_output";

    [JsonPropertyName("call_id")]
    public required string CallId { get; init; }

    [JsonPropertyName("output")]
    public required string Output { get; init; }
}

public sealed record ApiTool
{
    [JsonPropertyName("type")]
    public string Type => "function";

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("parameters")]
    public required object Parameters { get; init; }

    [JsonPropertyName("strict")]
    public bool Strict => false;
}

public sealed record ResponsesRequest
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required object Input { get; init; }

    [JsonPropertyName("instructions")]
    public string? Instructions { get; init; }

    [JsonPropertyName("tools")]
    public List<ApiTool>? Tools { get; init; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; init; }

    [JsonPropertyName("max_output_tokens")]
    public int? MaxOutputTokens { get; init; }
}

public sealed record ResponsesResponse
{
    [JsonPropertyName("output")]
    public List<OutputItem>? Output { get; init; }

    [JsonPropertyName("output_text")]
    public string? OutputText { get; init; }

    [JsonPropertyName("usage")]
    public ApiUsage? Usage { get; init; }

    [JsonPropertyName("error")]
    public ApiError? Error { get; init; }
}

public sealed record OutputItem
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }

    [JsonPropertyName("call_id")]
    public string? CallId { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public List<ContentPart>? Content { get; init; }
}

public sealed record ContentPart
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }
}

public sealed record ApiUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

public sealed record ApiError
{
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
