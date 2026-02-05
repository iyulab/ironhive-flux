using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;
using WebFlux.Core.Interfaces;
using WebFlux.Core.Models;
using WebFlux.Core.Options;

namespace IronHive.Flux.Core.Adapters.TextCompletion;

/// <summary>
/// IronHive IMessageGenerator를 WebFlux ITextCompletionService로 어댑트
/// </summary>
public class IronHiveTextCompletionServiceForWebFlux : WebFlux.Core.Interfaces.ITextCompletionService
{
    private readonly IMessageGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveTextCompletionServiceForWebFlux>? _logger;

    public IronHiveTextCompletionServiceForWebFlux(
        IMessageGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveTextCompletionServiceForWebFlux>? logger = null)
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
        _logger?.LogDebug("WebFlux 텍스트 완성 시작 - PromptLength: {Length}", prompt.Length);

        var request = CreateRequest(prompt, options);
        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        _logger?.LogDebug("WebFlux 텍스트 완성 완료 - ResultLength: {Length}", result.Length);
        return result;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<string> CompleteStreamAsync(
        string prompt,
        TextCompletionOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("WebFlux 스트리밍 텍스트 완성 시작 - PromptLength: {Length}", prompt.Length);

        var request = CreateRequest(prompt, options);

        await foreach (var chunk in _generator.GenerateStreamingMessageAsync(request, cancellationToken))
        {
            if (chunk is StreamingContentDeltaResponse deltaResponse &&
                deltaResponse.Delta is TextDeltaContent textDelta)
            {
                yield return textDelta.Value ?? string.Empty;
            }
        }

        _logger?.LogDebug("WebFlux 스트리밍 텍스트 완성 완료");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> CompleteBatchAsync(
        IEnumerable<string> prompts,
        TextCompletionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var promptList = prompts.ToList();
        _logger?.LogDebug("WebFlux 배치 텍스트 완성 시작 - Count: {Count}", promptList.Count);

        var results = new List<string>();
        foreach (var prompt in promptList)
        {
            var result = await CompleteAsync(prompt, options, cancellationToken);
            results.Add(result);
        }

        _logger?.LogDebug("WebFlux 배치 텍스트 완성 완료 - Count: {Count}", results.Count);
        return results;
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new MessageGenerationRequest
            {
                Model = _options.TextCompletionModelId,
                Messages = [new UserMessage { Content = [new TextMessageContent { Value = "ping" }] }],
                MaxTokens = 1
            };
            await _generator.GenerateMessageAsync(request, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public ServiceHealthInfo GetHealthInfo()
    {
        return new ServiceHealthInfo
        {
            ServiceName = "IronHive TextCompletionService for WebFlux"
        };
    }

    private MessageGenerationRequest CreateRequest(string prompt, TextCompletionOptions? options)
    {
        return new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = (float?)(options?.Temperature) ?? _options.DefaultTemperature,
            MaxTokens = options?.MaxTokens ?? _options.DefaultCompletionMaxTokens
        };
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }
}
