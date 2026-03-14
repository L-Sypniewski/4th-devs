using System.Diagnostics;

namespace InteractionExample;

/// <summary>
/// Multi-turn conversation with the Responses API using full input history.
/// </summary>
public static class Program
{
    public static async Task<int> Main()
    {
        try
        {
            var config = Configuration.Load();
            using var chatClient = new ChatClient(config);

            const string firstQuestion = "What is 25 * 48?";
            var firstAnswer = await chatClient.ChatAsync(firstQuestion);

            var secondQuestionContext = new List<Message>
            {
                new(Role: "user", Content: firstQuestion),
                new(Role: "assistant", Content: firstAnswer.Text)
            };

            const string secondQuestion = "Divide that by 4.";
            var secondAnswer = await chatClient.ChatAsync(secondQuestion, secondQuestionContext);

            Console.WriteLine($"Q: {firstQuestion}");
            Console.WriteLine($"A: {firstAnswer.Text} ({firstAnswer.ReasoningTokens} reasoning tokens)");
            Console.WriteLine($"Q: {secondQuestion}");
            Console.WriteLine($"A: {secondAnswer.Text} ({secondAnswer.ReasoningTokens} reasoning tokens)");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
