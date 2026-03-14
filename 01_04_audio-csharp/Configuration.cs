using Microsoft.Extensions.Configuration;

namespace AudioAgent;

/// <summary>
/// Central configuration for API keys and settings.
/// Loads from user secrets and environment variables.
/// </summary>
public sealed class Configuration
{
    public string OpenAiApiKey { get; init; } = "";
    public string OpenRouterApiKey { get; init; } = "";
    public string GeminiApiKey { get; init; } = "";
    public string AiProvider { get; init; } = "";

    public static Configuration Load()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Configuration>()
            .AddEnvironmentVariables()
            .Build();

        var openAiKey = (config["OPENAI_API_KEY"] ?? "").Trim();
        var openRouterKey = (config["OPENROUTER_API_KEY"] ?? "").Trim();
        var geminiKey = (config["GEMINI_API_KEY"] ?? "").Trim();
        var provider = (config["AI_PROVIDER"] ?? "").Trim().ToLowerInvariant();

        // Validate at least one AI key is present
        if (string.IsNullOrEmpty(openAiKey) && string.IsNullOrEmpty(openRouterKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Error: API key is not set");
            Console.Error.WriteLine("       Set via user secrets: dotnet user-secrets set OPENAI_API_KEY your-key");
            Console.Error.WriteLine("       Or environment variable: OPENAI_API_KEY=sk-...");
            Console.ResetColor();
            Environment.Exit(1);
        }

        // Validate Gemini key
        if (string.IsNullOrEmpty(geminiKey))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Error: GEMINI_API_KEY environment variable is not set");
            Console.Error.WriteLine("       Add it via user secrets: dotnet user-secrets set GEMINI_API_KEY your-key");
            Console.ResetColor();
            Environment.Exit(1);
        }

        // Resolve provider
        var resolvedProvider = ResolveProvider(provider, openAiKey, openRouterKey);

        return new Configuration
        {
            OpenAiApiKey = openAiKey,
            OpenRouterApiKey = openRouterKey,
            GeminiApiKey = geminiKey,
            AiProvider = resolvedProvider
        };
    }

    private static string ResolveProvider(string requested, string openAiKey, string openRouterKey)
    {
        var hasOpenAi = !string.IsNullOrEmpty(openAiKey);
        var hasOpenRouter = !string.IsNullOrEmpty(openRouterKey);

        if (!string.IsNullOrEmpty(requested))
        {
            if (requested == "openai" && !hasOpenAi)
            {
                Console.Error.WriteLine("Error: AI_PROVIDER=openai requires OPENAI_API_KEY");
                Environment.Exit(1);
            }
            if (requested == "openrouter" && !hasOpenRouter)
            {
                Console.Error.WriteLine("Error: AI_PROVIDER=openrouter requires OPENROUTER_API_KEY");
                Environment.Exit(1);
            }
            return requested;
        }

        return hasOpenAi ? "openai" : "openrouter";
    }

    public string GetApiKey() => AiProvider == "openai" ? OpenAiApiKey : OpenRouterApiKey;

    public string GetResponsesEndpoint() => AiProvider == "openai"
        ? "https://api.openai.com/v1/responses"
        : "https://openrouter.ai/api/v1/responses";

    public string ResolveModelForProvider(string model)
    {
        if (AiProvider != "openrouter" || model.Contains('/'))
            return model;

        return model.StartsWith("gpt-") ? $"openai/{model}" : model;
    }
}
