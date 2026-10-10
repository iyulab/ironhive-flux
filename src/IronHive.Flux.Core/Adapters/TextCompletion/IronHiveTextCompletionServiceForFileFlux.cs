using FileFlux;
using FileFlux.Core;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IronHive.Flux.Core.Adapters.TextCompletion;

/// <summary>
/// IronHive IMessageGenerator를 FileFlux IDocumentAnalysisService로 어댑트
/// </summary>
public partial class IronHiveTextCompletionServiceForFileFlux : FileFlux.IDocumentAnalysisService
{
    private readonly IMessageGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveTextCompletionServiceForFileFlux>? _logger;

    public IronHiveTextCompletionServiceForFileFlux(
        IMessageGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveTextCompletionServiceForFileFlux>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public DocumentAnalysisServiceInfo ProviderInfo => new()
    {
        Name = "IronHive",
        Type = DocumentAnalysisProviderType.Custom,
        SupportedModels = [_options.TextCompletionModelId],
        // 0 = not declared: the adapter does not know the configured model's context window, and a made-up
        // figure would make FileFlux's context check pass prompts the model cannot take.
        MaxContextLength = 0,
        ApiVersion = "v1"
    };

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new MessageGenerationRequest
            {
                Model = _options.TextCompletionModelId,
                Messages = [Message.User("ping")],
                MaxTokens = 1
            };
            await _generator.GenerateMessageAsync(request, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
        => GenerateAsync(prompt, GenerationSettings.Default, cancellationToken);

    /// <summary>
    /// Generates with the caller's sampling settings; an unset value falls back to
    /// <see cref="IronHiveFluxCoreOptions.DefaultTemperature"/> / <see cref="IronHiveFluxCoreOptions.DefaultCompletionMaxTokens"/>.
    /// </summary>
    /// <exception cref="GenerationTruncatedException">The model stopped at the output token limit.</exception>
    public async Task<string> GenerateAsync(
        string prompt,
        GenerationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_logger is not null)
            LogTextCompletionStarted(_logger, prompt.Length);

        var maxTokens = settings.MaxTokens is > 0 ? settings.MaxTokens.Value : _options.DefaultCompletionMaxTokens;
        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            Messages = [Message.User(prompt)],
            Temperature = settings.Temperature is { } temperature ? (float)temperature : _options.DefaultTemperature,
            MaxTokens = maxTokens
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        if (response.DoneReason == MessageDoneReason.MaxTokens)
            throw new GenerationTruncatedException(maxTokens);

        var result = ExtractTextFromResponse(response);

        if (_logger is not null)
            LogTextCompletionCompleted(_logger, result.Length);
        return result;
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message?.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux text completion started - PromptLength: {Length}")]
    private static partial void LogTextCompletionStarted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux text completion completed - ResultLength: {Length}")]
    private static partial void LogTextCompletionCompleted(ILogger logger, int Length);

    #endregion
}
