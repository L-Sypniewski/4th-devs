using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InteractionExample;

/// <summary>
/// Configuration for the AI API client.
/// </summary>
public sealed class Configuration
{
    private static readonly string[] ValidProviders = ["openai", "openrouter"];

    public required string Provider { get; init; }
    public required string ApiKey { get; init; }
    public required string ApiEndpoint { get; init; }
    public Dictionary<string, string> ExtraHeaders { get; init; } = new();

    public string ResolveModelForProvider(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model, nameof(model));

        // OpenRouter requires provider prefix for OpenAI models
        if (Provider == "openrouter" && !model.Contains('/'))
        {
            return model.StartsWith("gpt-") ? $"openai/{model}" : model;
        }

        return model;
    }

    public static Configuration Load()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Configuration>()
            .AddEnvironmentVariables()
            .Build();

        var openAiKey = (config["OPENAI_API_KEY"] ?? "").Trim();
        var openRouterKey = (config["OPENROUTER_API_KEY"] ?? "").Trim();
        var requestedProvider = (config["AI_PROVIDER"] ?? "").Trim().ToLowerInvariant();

        var hasOpenAiKey = !string.IsNullOrEmpty(openAiKey);
        var hasOpenRouterKey = !string.IsNullOrEmpty(openRouterKey);

        if (!hasOpenAiKey && !hasOpenRouterKey)
        {
            throw new InvalidOperationException(
                "API key is not set. Set OPENAI_API_KEY or OPENROUTER_API_KEY via user secrets or environment variable.\n" +
                "Run: dotnet user-secrets set OPENAI_API_KEY your-key-here");
        }

        if (!string.IsNullOrEmpty(requestedProvider) && !ValidProviders.Contains(requestedProvider))
        {
            throw new InvalidOperationException("AI_PROVIDER must be one of: openai, openrouter");
        }

        var provider = ResolveProvider(requestedProvider, hasOpenAiKey, hasOpenRouterKey, openAiKey, openRouterKey);

        return provider switch
        {
            "openai" => new Configuration
            {
                Provider = "openai",
                ApiKey = openAiKey,
                ApiEndpoint = "https://api.openai.com/v1/responses",
                ExtraHeaders = new Dictionary<string, string>()
            },
            "openrouter" => new Configuration
            {
                Provider = "openrouter",
                ApiKey = openRouterKey,
                ApiEndpoint = "https://openrouter.ai/api/v1/responses",
                ExtraHeaders = BuildOpenRouterHeaders(config)
            },
            _ => throw new InvalidOperationException($"Unknown provider: {provider}")
        };
    }

    private static string ResolveProvider(
        string? requestedProvider,
        bool hasOpenAiKey,
        bool hasOpenRouterKey,
        string openAiKey,
        string openRouterKey)
    {
        if (!string.IsNullOrEmpty(requestedProvider))
        {
            if (requestedProvider == "openai" && !hasOpenAiKey)
            {
                throw new InvalidOperationException("AI_PROVIDER=openai requires OPENAI_API_KEY");
            }

            if (requestedProvider == "openrouter" && !hasOpenRouterKey)
            {
                throw new InvalidOperationException("AI_PROVIDER=openrouter requires OPENROUTER_API_KEY");
            }

            return requestedProvider;
        }

        return hasOpenAiKey ? "openai" : "openrouter";
    }

    private static Dictionary<string, string> BuildOpenRouterHeaders(IConfiguration config)
    {
        var headers = new Dictionary<string, string>();

        var httpReferer = config["OPENROUTER_HTTP_REFERER"];
        if (!string.IsNullOrEmpty(httpReferer))
        {
            headers["HTTP-Referer"] = httpReferer;
        }

        var appName = config["OPENROUTER_APP_NAME"];
        if (!string.IsNullOrEmpty(appName))
        {
            headers["X-Title"] = appName;
        }

        return headers;
    }
}

/// <summary>
/// Represents a message in the conversation.
/// </summary>
/// <param name="Role">The role of the message sender (user or assistant).</param>
/// <param name="Content">The content of the message.</param>
public sealed record Message(string Role, string Content);

/// <summary>
/// Represents a message with type for the API request.
/// </summary>
public sealed record ApiMessage
{
    [JsonPropertyName("type")]
    public string Type => "message";

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }
}

/// <summary>
/// Represents the response from the chat API.
/// </summary>
/// <param name="Text">The text response.</param>
/// <param name="ReasoningTokens">Number of reasoning tokens used.</param>
public sealed record ChatResponse(string Text, int ReasoningTokens);
