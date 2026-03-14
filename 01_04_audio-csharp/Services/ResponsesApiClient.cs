using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AudioAgent.Helpers;
using AudioAgent.Models;

namespace AudioAgent.Services;

/// <summary>
/// Responses API client for chat completions with tool support.
/// </summary>
public sealed class ResponsesApiClient(Configuration config, HttpClient httpClient, UsageTracker usageTracker)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public async Task<ResponsesResponse> ChatAsync(
        object input,
        List<ApiTool>? tools = null,
        string? instructions = null,
        int? maxOutputTokens = null,
        CancellationToken cancellationToken = default)
    {
        var model = config.ResolveModelForProvider("gpt-4.1");

        var request = new ResponsesRequest
        {
            Model = model,
            Input = input,
            Instructions = instructions,
            Tools = tools,
            ToolChoice = tools?.Count > 0 ? "auto" : null,
            MaxOutputTokens = maxOutputTokens
        };

        var body = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, config.GetResponsesEndpoint());
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.GetApiKey());
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");

        // Add OpenRouter headers if needed
        if (config.AiProvider == "openrouter")
        {
            var referer = Environment.GetEnvironmentVariable("OPENROUTER_HTTP_REFERER");
            var appName = Environment.GetEnvironmentVariable("OPENROUTER_APP_NAME");
            if (!string.IsNullOrEmpty(referer))
                httpRequest.Headers.Add("HTTP-Referer", referer);
            if (!string.IsNullOrEmpty(appName))
                httpRequest.Headers.Add("X-Title", appName);
        }

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var data = JsonSerializer.Deserialize<ResponsesResponse>(responseBody, JsonOptions);

        if (!response.IsSuccessStatusCode || data?.Error != null)
        {
            throw new Exception(data?.Error?.Message ?? $"Responses API request failed ({response.StatusCode})");
        }

        if (data?.Usage != null)
        {
            usageTracker.RecordUsage(new UsageStats
            {
                InputTokens = data.Usage.InputTokens,
                OutputTokens = data.Usage.OutputTokens
            });
        }

        return data ?? throw new Exception("Empty response from API");
    }

    public List<ApiFunctionCall> ExtractToolCalls(ResponsesResponse response)
    {
        return response.Output?
            .Where(item => item.Type == "function_call")
            .Select(item => new ApiFunctionCall
            {
                Name = item.Name ?? "",
                Arguments = item.Arguments ?? "{}",
                CallId = item.CallId ?? ""
            })
            .ToList() ?? [];
    }

    public string? ExtractText(ResponsesResponse response)
    {
        // Check for direct output_text
        if (!string.IsNullOrEmpty(response.OutputText))
            return response.OutputText;

        // Extract from output items
        var messages = response.Output?
            .Where(item => item.Type == "message") ?? [];

        foreach (var message in messages)
        {
            var textPart = message.Content?
                .FirstOrDefault(part => part.Type == "output_text" && !string.IsNullOrEmpty(part.Text));

            if (textPart != null)
                return textPart.Text;
        }

        return null;
    }
}
