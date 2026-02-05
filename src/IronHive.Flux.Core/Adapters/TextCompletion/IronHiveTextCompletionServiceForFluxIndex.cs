using FluxIndex.Core.Application.Interfaces;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IronHive.Flux.Core.Adapters.TextCompletion;

/// <summary>
/// IronHive IMessageGenerator를 FluxIndex ITextCompletionService로 어댑트
/// </summary>
public class IronHiveTextCompletionServiceForFluxIndex : FluxIndex.Core.Application.Interfaces.ITextCompletionService
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
    public async Task<string> GenerateCompletionAsync(
        string prompt,
        int maxTokens = 500,
        float temperature = 0.7f,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FluxIndex 텍스트 완성 시작 - PromptLength: {Length}, MaxTokens: {MaxTokens}",
            prompt.Length, maxTokens);

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = temperature,
            MaxTokens = maxTokens
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        _logger?.LogDebug("FluxIndex 텍스트 완성 완료 - ResultLength: {Length}", result.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<string> GenerateJsonCompletionAsync(
        string prompt,
        int maxTokens = 500,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FluxIndex JSON 완성 시작 - PromptLength: {Length}", prompt.Length);

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

        _logger?.LogDebug("FluxIndex JSON 완성 완료 - ResultLength: {Length}", result.Length);
        return result;
    }

    /// <inheritdoc />
    public int CountTokens(string text)
    {
        return text.Length / 4;
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
        if (text.StartsWith("```json"))
            text = text[7..];
        else if (text.StartsWith("```"))
            text = text[3..];

        if (text.EndsWith("```"))
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
}
