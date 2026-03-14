using Microsoft.Extensions.Configuration;

namespace VideoAgent;

public class Program
{
    private const string ExampleQuery = "List 4 big claims breakdown from this video https://www.youtube.com/watch?v=Iar4yweKGoI";

    public static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>()
            .AddEnvironmentVariables()
            .Build();

        var openAiKey = config["OPENAI_API_KEY"] ?? config["OPENROUTER_API_KEY"];
        var geminiKey = config["GEMINI_API_KEY"];

        if (string.IsNullOrEmpty(openAiKey))
        {
            ConsoleLogger.Error("API key not set", "Set OPENAI_API_KEY or OPENROUTER_API_KEY");
            return;
        }

        if (string.IsNullOrEmpty(geminiKey))
        {
            ConsoleLogger.Error("GEMINI_API_KEY not set");
            return;
        }

        var configuration = new Configuration
        {
            OpenAiApiKey = openAiKey,
            OpenRouterApiKey = config["OPENROUTER_API_KEY"],
            AiProvider = config["AI_PROVIDER"] ?? "openai",
            GeminiApiKey = geminiKey
        };

        using var httpClient = new HttpClient();
        var apiClient = new ResponsesApiClient(configuration, httpClient);
        var videoService = new GeminiVideoService(configuration, httpClient);
        var agent = new Agent(apiClient, videoService);

        // Warning
        ConsoleLogger.Box("Video Processing Agent\nType 'exit' to quit, 'clear' to reset");
        Console.WriteLine($"  Example: {ExampleQuery}");
        Console.WriteLine();
        Console.WriteLine("  WARNING: This will process videos and consume tokens.");
        Console.WriteLine();

        // Confirmation
        Console.Write("Continue? (yes/y): ");
        var answer = Console.ReadLine()?.Trim().ToLower();
        if (answer != "yes" && answer != "y")
        {
            Console.WriteLine("Cancelled.");
            return;
        }

        // REPL
        var conversation = new List<object>();

        while (true)
        {
            Console.Write("\nYou: ");
            var input = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                conversation = [];
                StatsTracker.Reset();
                ConsoleLogger.Success("Conversation cleared");
                continue;
            }

            try
            {
                var result = await agent.RunAsync(input, conversation);
                conversation = result.ConversationHistory;
                Console.WriteLine($"\nAssistant: {result.Response}\n");
            }
            catch (Exception ex)
            {
                ConsoleLogger.Error("Error", ex.Message);
            }
        }

        StatsTracker.Log();
    }
}
