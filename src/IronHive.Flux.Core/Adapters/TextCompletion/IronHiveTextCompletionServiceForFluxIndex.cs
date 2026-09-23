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

        var request = FluxCompletionRequestMapper.Create(
            _options.TextCompletionModelId, prompt, options, defaultTemperature: 0.7f, defaultMaxTokens: 500, json: false);

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = FluxCompletionRequestMapper.ExtractText(response);

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

        var request = FluxCompletionRequestMapper.Create(
            _options.TextCompletionModelId, prompt, options, defaultTemperature: FluxCompletionRequestMapper.JsonTemperature, defaultMaxTokens: 500, json: true);

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = FluxCompletionRequestMapper.ExtractJson(FluxCompletionRequestMapper.ExtractText(response));

        if (_logger is not null)
        {
            LogJsonCompletionCompleted(_logger, result.Length);
        }

        return result;
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
