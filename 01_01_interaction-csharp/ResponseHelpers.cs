namespace InteractionExample;

/// <summary>
/// Helper methods for processing API responses.
/// </summary>
public static class ResponseHelpers
{
    /// <summary>
    /// Extracts the text content from a responses API response.
    /// </summary>
    public static string ExtractResponseText(string? outputText, IEnumerable<OutputItem>? output)
    {
        // Check for direct output_text field
        if (!string.IsNullOrWhiteSpace(outputText))
        {
            return outputText;
        }

        // Extract from output array
        var messages = output?
            .Where(item => item?.Type == "message")
            .ToList() ?? [];

        foreach (var message in messages)
        {
            if (message.Content is null) continue;

            var textPart = message.Content
                .FirstOrDefault(part => part?.Type == "output_text" && !string.IsNullOrEmpty(part.Text));

            if (textPart is not null)
            {
                return textPart.Text ?? string.Empty;
            }
        }

        return string.Empty;
    }
}

/// <summary>
/// Represents an output item from the API response.
/// </summary>
public sealed record OutputItem
{
    public string? Type { get; init; }
    public List<ContentPart>? Content { get; init; }
}

/// <summary>
/// Represents a content part in an output item.
/// </summary>
public sealed record ContentPart
{
    public string? Type { get; init; }
    public string? Text { get; init; }
}
