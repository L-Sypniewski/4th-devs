using AudioAgent.Helpers;
using AudioAgent.Services;

namespace AudioAgent;

/// <summary>
/// Audio Processing Agent - C# / .NET 10
///
/// Transcribes, analyzes, and generates audio using Gemini API.
/// </summary>
class Program
{
    private static readonly string[] Examples =
    [
        "Transcribe the file from workspace/input/",
        "Generate audio: Welcome to our product demo",
        "Analyze the speech patterns in workspace/input/tech_briefing.wav",
        "What topics are discussed in this recording?"
    ];

    private static readonly CancellationTokenSource Cts = new();

    static async Task<int> Main(string[] args)
    {
        // Setup graceful shutdown
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Cts.Cancel();
        };

        try
        {
            await RunAsync();
            return 0;
        }
        catch (OperationCanceledException)
        {
            ConsoleLogger.Info("Shutdown requested");
            return 0;
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error("Startup error", ex.Message);
            return 1;
        }
    }

    private static async Task RunAsync()
    {
        // Load configuration
        var config = Configuration.Load();
        var projectRoot = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.FullName;
        var usageTracker = new UsageTracker();

        ConsoleLogger.Box("Audio Processing Agent");

        // Show available tools
        ConsoleLogger.Heading("TOOLS");
        foreach (var tool in AudioTools.GetToolDefinitions())
        {
            ConsoleLogger.Info($"{tool.Name.PadRight(18)} — {tool.Description.Split('.')[0]}");
        }

        // Setup HTTP client
        using var httpClient = new HttpClient();
        var geminiClient = new GeminiClient(config, httpClient, usageTracker);
        var audioTools = new AudioTools(geminiClient, projectRoot);
        var apiClient = new ResponsesApiClient(config, httpClient, usageTracker);
        var agent = new AgentLoop(apiClient, audioTools);

        PrintExamples();

        // Run REPL
        await RunReplAsync(agent, usageTracker, Cts.Token);
    }

    private static void PrintExamples()
    {
        ConsoleLogger.Heading("EXAMPLES", "For demo purposes, try these queries:");
        foreach (var example in Examples)
        {
            ConsoleLogger.Example(example);
        }
        ConsoleLogger.Hint("Type 'exit' to quit, 'clear' to reset conversation");
    }

    private static async Task RunReplAsync(AgentLoop agent, UsageTracker usageTracker, CancellationToken cancellationToken)
    {
        List<object>? history = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("You: ");
            var input = await Task.Run(() => Console.ReadLine(), cancellationToken);

            if (string.IsNullOrEmpty(input))
                continue;

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
            {
                history = null;
                usageTracker.Reset();
                ConsoleLogger.Success("Conversation cleared\n");
                continue;
            }

            try
            {
                var result = await agent.RunAsync(input, history, cancellationToken);
                history = result.ConversationHistory;
                Console.WriteLine($"\nAssistant: {result.Response}\n");
            }
            catch (Exception ex)
            {
                ConsoleLogger.Error("Error", ex.Message);
                Console.WriteLine();
            }
        }

        usageTracker.LogStats();
    }
}
