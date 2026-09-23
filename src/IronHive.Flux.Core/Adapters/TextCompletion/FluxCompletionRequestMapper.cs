using Flux.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;

namespace IronHive.Flux.Core.Adapters.TextCompletion;

/// <summary>
/// Maps a <see cref="TextCompletionOptions"/> request onto an IronHive <see cref="MessageGenerationRequest"/>, shared by
/// the adapters that implement <see cref="ITextCompletionService"/>. Every option IronHive can carry is carried:
/// temperature, output budget, top-p, stop sequences, system prompt, and the response format / schema.
/// <see cref="TextCompletionOptions.FrequencyPenalty"/> and <see cref="TextCompletionOptions.PresencePenalty"/> have no
/// IronHive counterpart and are not sent.
/// </summary>
internal static class FluxCompletionRequestMapper
{
    internal const float JsonTemperature = 0.1f;

    internal const string JsonInstruction =
        "You are a JSON generator. Always respond with valid JSON only, no additional text or markdown.";

    /// <param name="json">True for <see cref="ITextCompletionService.CompleteJsonAsync"/>: the JSON instruction is added to
    /// the system prompt and the temperature is <see cref="JsonTemperature"/>.</param>
    public static MessageGenerationRequest Create(
        string modelId,
        string prompt,
        TextCompletionOptions? options,
        float defaultTemperature,
        int defaultMaxTokens,
        bool json)
    {
        var system = options?.SystemPrompt;
        if (json)
            system = string.IsNullOrWhiteSpace(system) ? JsonInstruction : system + "\n\n" + JsonInstruction;

        return new MessageGenerationRequest
        {
            Model = modelId,
            System = system,
            Messages = [Message.User(prompt)],
            // TextCompletionOptions.Temperature is not nullable (0.7 unless set), so "the caller chose 0.7" and "the
            // caller did not choose" look the same. A JSON request keeps the low, fixed temperature it always had.
            Temperature = json ? JsonTemperature : options?.Temperature ?? defaultTemperature,
            MaxTokens = options?.MaxTokens ?? defaultMaxTokens,
            TopP = options?.TopP,
            StopSequences = options?.StopSequences is { Count: > 0 } stops ? stops.ToList() : null,
            OutputFormat = ResolveOutputFormat(options),
        };
    }

    private static OutputFormat? ResolveOutputFormat(TextCompletionOptions? options)
    {
        // The schema is the more specific request (TextCompletionOptions.ResponseSchema remarks).
        if (!string.IsNullOrWhiteSpace(options?.ResponseSchema))
            return OutputFormat.For(options.ResponseSchema);
        // Provider JSON mode only when the caller asked for it: CompleteJsonAsync alone keeps the instruction-and-extract
        // behaviour it always had, because some providers' JSON mode admits only a top-level object and a prompt may ask
        // for an array.
        if (string.Equals(options?.ResponseFormat, "json", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(options?.ResponseFormat, "json_object", StringComparison.OrdinalIgnoreCase))
            return OutputFormat.Json;
        return null;
    }

    /// <summary>
    /// Throws <see cref="TextCompletionTruncatedException"/> when the caller set
    /// <see cref="TextCompletionOptions.ThrowOnTruncation"/> and the model stopped at the request's output budget.
    /// </summary>
    public static void ThrowIfTruncated(MessageGenerationRequest request, MessageResponse response, TextCompletionOptions? options)
    {
        if (options?.ThrowOnTruncation == true && response.DoneReason == MessageDoneReason.MaxTokens)
            throw request.MaxTokens is { } maxTokens
                ? new TextCompletionTruncatedException(maxTokens)
                : new TextCompletionTruncatedException();
    }

    public static string ExtractText(MessageResponse response)
    {
        var textContents = response.Message?.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }

    /// <summary>Strips a Markdown code fence and any prose around the outermost JSON object or array.</summary>
    public static string ExtractJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.Ordinal))
            text = text[7..];
        else if (text.StartsWith("```", StringComparison.Ordinal))
            text = text[3..];

        if (text.EndsWith("```", StringComparison.Ordinal))
            text = text[..^3];

        text = text.Trim();

        var jsonStart = text.IndexOfAny(['{', '[']);
        if (jsonStart < 0)
            return text;

        var jsonEndChar = text[jsonStart] == '{' ? '}' : ']';
        var jsonEnd = text.LastIndexOf(jsonEndChar);
        return jsonEnd > jsonStart ? text[jsonStart..(jsonEnd + 1)] : text;
    }
}
