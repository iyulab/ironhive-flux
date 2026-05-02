using Flux.Abstractions;
using FluxIndex.Core.Application.Interfaces;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TokenMeter;

namespace IronHive.Flux.Core.Adapters.TextCompletion;

/// <summary>
/// IronHive IMessageGenerator를 FluxIndex ITextCompletionService로 어댑트
/// </summary>
public partial class IronHiveTextCompletionServiceForFluxIndex : FluxIndex.Core.Application.Interfaces.ITextCompletionService
{
    private readonly IMessageGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly TokenMeter.ITokenCounter? _tokenCounter;
    private readonly ILogger<IronHiveTextCompletionServiceForFluxIndex>? _logger;

    public IronHiveTextCompletionServiceForFluxIndex(
        IMessageGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        TokenMeter.ITokenCounter? tokenCounter = null,
        ILogger<IronHiveTextCompletionServiceForFluxIndex>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _tokenCounter = tokenCounter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> GenerateCompletionAsync(
        string prompt,
        int maxTokens = 500,
        float temperature = 0.7f,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogTextCompletionStarted(_logger, prompt.Length, maxTokens);

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = temperature,
            MaxTokens = maxTokens
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        if (_logger is not null)
            LogTextCompletionCompleted(_logger, result.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<string> GenerateJsonCompletionAsync(
        string prompt,
        int maxTokens = 500,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogJsonCompletionStarted(_logger, prompt.Length);

        var systemPrompt = "You are a JSON generator. Always respond with valid JSON only, no additional text or markdown.";

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            System = systemPrompt,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = 0.1f,
            MaxTokens = maxTokens
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        result = ExtractJsonFromText(result);

        if (_logger is not null)
            LogJsonCompletionCompleted(_logger, result.Length);
        return result;
    }

    /// <inheritdoc />
    public Task<string> CompleteAsync(
        string prompt,
        TextCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GenerateCompletionAsync(
            prompt,
            options?.MaxTokens ?? 500,
            options?.Temperature ?? 0.7f,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> CompleteJsonAsync(
        string prompt,
        TextCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return GenerateJsonCompletionAsync(
            prompt,
            options?.MaxTokens ?? 500,
            cancellationToken);
    }

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        // Use TokenMeter for accurate counting when available
        if (_tokenCounter is not null)
        {
            return _tokenCounter.CountTokens(text);
        }

        // CJK-aware heuristic fallback
        // Korean: ~1.5 tokens/char, English: ~0.25 tokens/char
        var koreanCount = text.Count(c => c >= 0xAC00 && c <= 0xD7A3);
        var otherCount = text.Length - koreanCount;
        return (int)(koreanCount * 1.5 + otherCount * 0.25);
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }

    private static string ExtractJsonFromText(string text)
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
        if (jsonStart < 0) return text;

        var jsonEndChar = text[jsonStart] == '{' ? '}' : ']';
        var jsonEnd = text.LastIndexOf(jsonEndChar);

        if (jsonEnd > jsonStart)
            return text[jsonStart..(jsonEnd + 1)];

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
