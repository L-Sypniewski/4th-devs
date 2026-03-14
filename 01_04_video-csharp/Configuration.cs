namespace VideoAgent;

/// <summary>
/// Application configuration loaded from user secrets and environment variables.
/// </summary>
public sealed class Configuration
{
    public required string OpenAiApiKey { get; init; }
    public string? OpenRouterApiKey { get; init; }
    public string AiProvider { get; init; } = "openai";
    public required string GeminiApiKey { get; init; }

    public string ApiKey => AiProvider.ToLowerInvariant() switch
    {
        "openai" => OpenAiApiKey,
        "openrouter" => OpenRouterApiKey ?? throw new InvalidOperationException("OpenRouter API key not set"),
        _ => throw new InvalidOperationException($"Unknown provider: {AiProvider}")
    };

    public string ApiEndpoint => AiProvider.ToLowerInvariant() switch
    {
        "openai" => "https://api.openai.com/v1/responses",
        "openrouter" => "https://openrouter.ai/api/v1/responses",
        _ => throw new InvalidOperationException($"Unknown provider: {AiProvider}")
    };

    public string Model => "gpt-4.1";
    public string GeminiModel => "gemini-2.5-flash";
    public int MaxOutputTokens => 16384;
}
