using Flux.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IronHive.Flux.Core.Adapters.TextCompletion;

/// <summary>
/// IronHive IMessageGenerator를 FluxIndex ITextCompletionService로 어댑트
/// </summary>
public partial class IronHiveTextCompletionServiceForFluxIndex : ITextCompletionService
{
    private readonly IMessageGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveTextCompletionServiceForFluxIndex>? _logger;

    public IronHiveTextCompletionServiceForFluxIndex(
        IMessageGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveTextCompletionServiceForFluxIndex>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(
        string prompt,
        TextCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
        {
            LogTextCompletionStarted(_logger, prompt.Length, options?.MaxTokens ?? 500);
        }

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            Messages = [Message.User(prompt)],
            Temperature = options?.Temperature ?? 0.7f,
            MaxTokens = options?.MaxTokens ?? 500
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        if (_logger is not null)
        {
            LogTextCompletionCompleted(_logger, result.Length);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<string> CompleteJsonAsync(
        string prompt,
        TextCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
        {
            LogJsonCompletionStarted(_logger, prompt.Length);
        }

        const string systemPrompt = "You are a JSON generator. Always respond with valid JSON only, no additional text or markdown.";

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            System = systemPrompt,
            Messages = [Message.User(prompt)],
            Temperature = 0.1f,
            MaxTokens = options?.MaxTokens ?? 500
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractJsonFromText(ExtractTextFromResponse(response));

        if (_logger is not null)
        {
            LogJsonCompletionCompleted(_logger, result.Length);
        }

        return result;
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message?.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }

    private static string ExtractJsonFromText(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```json", StringComparison.Ordinal))
        {
            text = text[7..];
        }
        else if (text.StartsWith("```", StringComparison.Ordinal))
        {
            text = text[3..];
        }

        if (text.EndsWith("```", StringComparison.Ordinal))
        {
            text = text[..^3];
        }

        text = text.Trim();

        var jsonStart = text.IndexOfAny(['{', '[']);
        if (jsonStart < 0)
        {
            return text;
        }

        var jsonEndChar = text[jsonStart] == '{' ? '}' : ']';
        var jsonEnd = text.LastIndexOf(jsonEndChar);

        if (jsonEnd > jsonStart)
        {
            return text[jsonStart..(jsonEnd + 1)];
        }

        return text;
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex text completion started - PromptLength: {Length}, MaxTokens: {MaxTokens}")]
    private static partial void LogTextCompletionStarted(ILogger logger, int Length, int MaxTokens);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex text completion completed - ResultLength: {Length}")]
    private static partial void LogTextCompletionCompleted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex JSON completion started - PromptLength: {Length}")]
    private static partial void LogJsonCompletionStarted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FluxIndex JSON completion completed - ResultLength: {Length}")]
    private static partial void LogJsonCompletionCompleted(ILogger logger, int Length);

    #endregion
}
