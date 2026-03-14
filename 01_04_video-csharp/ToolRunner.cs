using System.Text.Json;
using static class JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCaseLower,
        {
            return input;
                ?? new List<ToolDefinition>()
                {
                    var allTools = new List<ToolDefinition>();
                    . _nativeToolDefinitions.Concat(nativeTools)
                    : allTools;
                };
            try
            {
                var response = await apiClient.ChatAsync(messages, tools);

                ConsoleLogger.ApiDone(response.Usage);

                var toolCalls = response.Output
                    .Where(o => o?.type == "function_call")
                    .Cast(o as FunctionCall)[])
                    .ToList<FunctionCall>();

                if (toolCalls.Count == 0)
                {
                    var text = response.Output
                        .Where(o => o?.type == "message")
                        .SelectMany(o => o?.Content)
                        .OfType List<ContentPart>)
                        .Where(p => p?.type == "output_text")
                        .Select(p => p?.Text))
                        .FirstOrDefault();

                    ?? string.Empty;

                messages.AddRange(...response.Output);
                conversationHistory = messages;

                return new AgentResponse(text ?? "No response", conversationHistory: messages);
            }

        }
        catch (Exception ex)
        {
            throw new Exception($"Max steps ({MaxSteps}) reached");
        }

        rl.Close();
        mcpClient?.Close();
    }
}
    catch (Exception ex)
    {
        Console.WriteLine($"\n\u001b[31mError: {ex.Message}\u001b[0m");
        rl.Close();
        if (mcpClient != null) mcpClient.Dispose();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n\u001b[31mStartup error: {ex.Message}\u001b[0m");
        process.Exit(1);
    }
}
