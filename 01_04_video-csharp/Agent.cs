using System.Text.Json;

namespace VideoAgent;

public sealed class Agent(ResponsesApiClient apiClient, GeminiVideoService videoService)
{
    private const int MaxSteps = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public async Task<AgentResponse> RunAsync(
        string query,
        List<object> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        NativeTools.Initialize(videoService);

        var messages = conversationHistory.ToList<object>();
        messages.Add(new ApiMessage { Role = "user", Content = query });

        ConsoleLogger.Info($"Processing: {query}");

        for (int step = 1; step <= MaxSteps; step++)
        {
            ConsoleLogger.Info($"Step {step}/{MaxSteps}");

            var response = await apiClient.ChatAsync(
                messages.Cast<ApiMessage>().ToList(),
                NativeTools.Definitions,
                cancellationToken);

            var usage = response.Usage;
            if (usage != null)
            {
                ConsoleLogger.Info($"Tokens: {usage.InputTokens} in / {usage.OutputTokens} out");
            }

            var toolCalls = response.Output?
                .Where(o => o.Type == "function_call")
                .ToList() ?? [];

            if (toolCalls.Count == 0)
            {
                var text = response.Output?
                    .Where(o => o.Type == "message")
                    .SelectMany(o => o.Content ?? [])
                    .Where(c => c.Type == "output_text")
                    .Select(c => c.Text)
                    .FirstOrDefault();

                messages.AddRange(response.Output ?? []);
                return new AgentResponse(text ?? "No response", messages);
            }

            messages.AddRange(response.Output ?? []);

            var results = await ExecuteToolsAsync(toolCalls, cancellationToken);
            messages.AddRange(results);
        }

        throw new Exception($"Max steps ({MaxSteps}) reached");
    }

    private async Task<List<object>> ExecuteToolsAsync(
        List<OutputItem> toolCalls,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();

        foreach (var toolCall in toolCalls)
        {
            if (toolCall.Name == null || toolCall.Arguments == null)
                continue;

            var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                toolCall.Arguments,
                JsonOptions) ?? [];

            try
            {
                var result = await NativeTools.ExecuteAsync(toolCall.Name, args, cancellationToken);
                var output = JsonSerializer.Serialize(result);

                results.Add(new FunctionCallOutput
                {
                    CallId = toolCall.CallId ?? "",
                    Output = output
                });
            }
            catch (Exception ex)
            {
                ConsoleLogger.Error($"Tool {toolCall.Name} failed", ex.Message);
                results.Add(new FunctionCallOutput
                {
                    CallId = toolCall.CallId ?? "",
                    Output = JsonSerializer.Serialize(new { error = ex.Message })
                });
            }
        }

        return results;
    }
}

public sealed record AgentResponse(string Response, List<object> ConversationHistory);
