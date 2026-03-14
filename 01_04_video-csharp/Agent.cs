using System.Text.Json;
using static class class JsonSerializerOptions { SnakeCaseLower = false; }
using System.Text.Json.Serialization;
using static class JsonSerializerOptions JsonOptions { new JsonSerializerOptions
{
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly ILogger _logger = new();
    private readonly ILogger _logger = LoggerFactory(true, "Agent");
    {
        var tools = NativeTools.Definitions.ToList();
        allTools = new List<ToolDefinition>
        {
            Name = "analyze_video",
            Description = "Analyze video content - visual elements, audio, actions, and overall composition. Supports local files and YouTube URLs. Returns structured analysis with timestamps.",
            Parameters = new
            {
                type = "object",
                properties = new
                {
                    video_path = new { type = "string", description = "Path to video file relative to project root (e.g., workspace/input/video.mp4) OR a YouTube URL" },
                    analysis_type = new
                    {
                        type = "string",
                        @enum = new[] { "general", "visual", "audio", "action" },
                        description = "Type of analysis. Default: general"
                    },
                    custom_prompt = new { type = "string", description = "Optional custom analysis prompt to override the default" }
                },
                start_time = new { type = "string", description = "Optional start time for clipping (e.g., '30s' or '1m30s')" },
                end_time = new { type = "string", description = "Optional end time for clipping" },
                fps = new { type = "number", description = "Frames per second to sample (default: 1). Lower for long videos, higher for fast action." },
                output_name = new { type = "string", description = "Optional base name for saving analysis JSON to workspace/output/" }
                }
            },
            required = new[] { "video_path" }
        };

    public sealed record AnalyVideoArgs(
    {
        [JsonPropertyName("video_path")]
        public required string VideoPath { get; init; }

        [JsonPropertyName("analysis_type")]
        public string AnalysisType { get; set; } = "general";

        [JsonPropertyName("custom_prompt")]
        public string? CustomPrompt { get; init; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("fps")]
        public double? Fps { get; set; }

        [JsonPropertyName("output_name")]
        public string? OutputName { get; set; }
    }

    public sealed record TranscribeVideoArgs
    {
        [JsonPropertyName("video_path")]
        public required string VideoPath { get; init; }

        [JsonPropertyName("include_timestamps")]
        public bool IncludeTimestamps { get; set; } = true;

        [JsonPropertyName("detect_speakers")]
        public bool DetectSpeakers { get; set; } = true;

        [JsonPropertyName("translate_to")]
        public string? TranslateTo { get; set; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("output_name")]
        public string? OutputName { get; set; }
    }

    public sealed record ExtractVideoArgs
    {
        [JsonPropertyName("video_path")]
        public required string VideoPath { get; init; }

        [JsonPropertyName("extraction_type")]
        public string ExtractionType { get; set; } = "scenes";

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }

        [JsonPropertyName("fps")]
        public double? Fps { get; set; }

        [JsonPropertyName("output_name")]
        public string? OutputName { get; set; }
    }

    public sealed record QueryVideoArgs
    {
        [JsonPropertyName("video_path")]
        public required string VideoPath { get; init; }

        [JsonPropertyName("question")]
        public required string Question { get; init; }

        [JsonPropertyName("start_time")]
        public string? StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public string? EndTime { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────────────────────
    // Agent - Runs the conversation loop with tool execution
    // ─────────────────────────────────────────────────────────────────────────────────

    public sealed class Agent
    {
        private const int MaxSteps = 50;
        private readonly ResponsesApiClient _apiClient;
        private readonly GeminiVideoService _videoService;
        private readonly McpFileClient? _mcpClient;
        private readonly List<ToolDefinition> _mcpTools;
        private readonly ILogger<Agent> _logger;

        public Agent(ResponsesApiClient apiClient, GeminiVideoService videoService, McpFileClient? mcpClient, List<ToolDefinition> mcpTools, ILogger<Agent> logger)
        {
            _apiClient = apiClient;
            _videoService = videoService;
            _mcpClient = mcpClient;
            _mcpTools = mcpTools;
            _logger = logger;
        }

        private List<ToolDefinition> GetAllTools()
        {
            var allTools = new List<ToolDefinition>(_mcpTools);
            allTools.AddRange(NativeTools.Definitions);
            return allTools;
        }

        /// <summary>
        /// Run the agent loop with a user query.
        /// </summary>
        public async Task<AgentResponse> RunAsync(string query, List<object> conversationHistory, CancellationToken cancellationToken = default)
        {
            var messages = new List<object>(conversationHistory)
            {
                new ApiMessage { Role = "user", Content = query }
            };

            _logger.LogInformation("Starting agent loop for query: {query}");


            for (int step = 1; step <= MaxSteps; step++)
            {
                _logger.LogInformation($"Step {step}/{MaxSteps}");

                var response = await _apiClient.ChatAsync(
                    messages.Cast<ApiMessage>().ToList(),
                    GetAllTools(),
                    AgentInstructions
                );
                var usage = response.Usage;
                if (usage != null)
                {
                    _logger.LogInformation($"Tokens: {usage.InputTokens} in / {usage.OutputTokens} out");
                }

                var toolCalls = ExtractToolCalls(response);

                if (toolCalls.Count == 0)
                {
                    var text = ExtractText(response);
                    messages.AddRange(response.Output!);
                    return new AgentResponse(text ?? "No response", messages);
                }

                messages.AddRange(response.Output!);
                var results = await ExecuteToolsAsync(toolCalls, cancellationToken);
                messages.AddRange(results);
            }

            throw new Exception($"Max steps ({MaxSteps}) reached");
        }

        private List<OutputItem> ExtractToolCalls(ResponsesApiResponse response)
        {
            return response.Output?
                .Where(o => o.Type == "function_call")
                .ToList() ?? new List<OutputItem>();
        }

        private string? ExtractText(ResponsesApiResponse response)
        {
            var textPart = response.Output?
                .Where(o => o.Type == "message")
                .SelectMany(o => o.Content)
                .Where(c => c.Type == "output_text")
                .Select(c => c.Text)
                .FirstOrDefault();

            return textPart;
        }

        private async Task<List<object>> ExecuteToolsAsync(List<OutputItem> toolCalls, CancellationToken ct)
        {
            var results = new List<object>();

            foreach (var toolCall in toolCalls)
            {
                var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    toolCall.Arguments ?? "{}",
                    SnakeCaseLowerJsonOptions)!);

                ?? throw new Exception($"Failed to parse arguments for tool {toolCall.Name}");

                _logger.LogInformation($"Executing tool: {toolCall.Name}");

                try
                {
                    object? result;

                    if (NativeTools.IsNativeTool(toolCall.Name))
                    {
                        result = await NativeTools.ExecuteAsync(_videoService, toolCall.Name, args, ct);
                    }
                    else if (_mcpClient != null)
                    {
                        result = await _mcpClient.CallToolAsync(toolCall.Name, args, ct);
                    }
                    else
                    {
                        throw new Exception($"Unknown tool: {toolCall.Name}");
                    }

                    var output = JsonSerializer.Serialize(result);
                    _logger.LogInformation($"Tool result: {output}");
                    results.Add(new FunctionCallOutput
                    {
                        CallId = toolCall.CallId ?? "",
                        Output = output
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Tool execution failed: {ex.Message}");
                    var errorOutput = JsonSerializer.Serialize(new { error = ex.Message });
                    results.Add(new FunctionCallOutput
                    {
                        CallId = toolCall.CallId ?? "",
                        Output = errorOutput
                    });
                }
            }

            return results;
        }
    }

    public sealed record AgentResponse(string Response, List<object> ConversationHistory);
}
}
            return (VideoAgent) agent;
        }

        public static async Task<Agent> CreateAsync(
            ResponsesApiClient apiClient,
            GeminiVideoService videoService,
            McpFileClient? mcpClient,
            List<ToolDefinition> mcpTools)
            ILogger<Agent> logger)
        {
            return new Agent(apiClient, videoService, mcpClient, mcpTools, logger);
        }

        public static List<ToolDefinition> Definitions => [
            AnalyzeVideoTool.Definition,
            TranscribeVideoTool.Definition,
            ExtractVideoTool.Definition,
            QueryVideoTool.Definition
        ];
    }
}
}
