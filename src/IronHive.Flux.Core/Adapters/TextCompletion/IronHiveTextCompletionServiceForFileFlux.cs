using FileFlux;
using FileFlux.Core;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace IronHive.Flux.Core.Adapters.TextCompletion;

/// <summary>
/// IronHive IMessageGenerator를 FileFlux ITextCompletionService로 어댑트
/// </summary>
public class IronHiveTextCompletionServiceForFileFlux : FileFlux.ITextCompletionService
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
    public TextCompletionServiceInfo ProviderInfo => new()
    {
        Name = "IronHive",
        Type = TextCompletionProviderType.OpenAI,
        SupportedModels = [_options.TextCompletionModelId],
        MaxContextLength = 128000,
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
    public async Task<string> GenerateAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FileFlux 텍스트 완성 시작 - PromptLength: {Length}", prompt.Length);

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = _options.DefaultTemperature,
            MaxTokens = _options.DefaultCompletionMaxTokens
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        _logger?.LogDebug("FileFlux 텍스트 완성 완료 - ResultLength: {Length}", result.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<StructureAnalysisResult> AnalyzeStructureAsync(
        string prompt,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FileFlux 구조 분석 시작 - DocumentType: {Type}", documentType);

        var systemPrompt = $"You are a document structure analyzer. Analyze the given content and return structured information about sections, hierarchy, and document organization. Document type: {documentType}";

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            System = systemPrompt,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = 0.3f,
            MaxTokens = 2000
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var resultText = ExtractTextFromResponse(response);

        var result = new StructureAnalysisResult
        {
            DocumentType = documentType,
            RawResponse = resultText,
            TokensUsed = response.TokenUsage?.TotalTokens ?? 0,
            Confidence = 0.8
        };

        _logger?.LogDebug("FileFlux 구조 분석 완료 - TokensUsed: {Tokens}", result.TokensUsed);
        return result;
    }

    /// <inheritdoc />
    public async Task<ContentSummary> SummarizeContentAsync(
        string prompt,
        int maxLength = 200,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FileFlux 요약 시작 - MaxLength: {MaxLength}", maxLength);

        var systemPrompt = $"You are a content summarizer. Summarize the given content in no more than {maxLength} characters. Also extract key keywords.";

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            System = systemPrompt,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = 0.5f,
            MaxTokens = 500
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var resultText = ExtractTextFromResponse(response);

        var result = new ContentSummary
        {
            Summary = resultText.Length > maxLength ? resultText[..maxLength] : resultText,
            OriginalLength = prompt.Length,
            TokensUsed = response.TokenUsage?.TotalTokens ?? 0,
            Confidence = 0.85
        };

        _logger?.LogDebug("FileFlux 요약 완료 - SummaryLength: {Length}", result.Summary.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<MetadataExtractionResult> ExtractMetadataAsync(
        string prompt,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FileFlux 메타데이터 추출 시작 - DocumentType: {Type}", documentType);

        var systemPrompt = $"You are a metadata extractor. Extract keywords, language, categories, and entities from the given content. Document type: {documentType}. Return JSON format.";

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            System = systemPrompt,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = 0.3f,
            MaxTokens = 1000
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var resultText = ExtractTextFromResponse(response);

        var result = new MetadataExtractionResult
        {
            TokensUsed = response.TokenUsage?.TotalTokens ?? 0,
            Confidence = 0.8
        };

        try
        {
            var jsonStart = resultText.IndexOf('{');
            var jsonEnd = resultText.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = resultText[jsonStart..(jsonEnd + 1)];
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);

                if (parsed?.TryGetValue("keywords", out var keywords) == true)
                    result.Keywords = keywords.EnumerateArray().Select(k => k.GetString() ?? "").ToArray();

                if (parsed?.TryGetValue("language", out var language) == true)
                    result.Language = language.GetString();

                if (parsed?.TryGetValue("categories", out var categories) == true)
                    result.Categories = categories.EnumerateArray().Select(c => c.GetString() ?? "").ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "메타데이터 JSON 파싱 실패");
        }

        _logger?.LogDebug("FileFlux 메타데이터 추출 완료 - Keywords: {Count}", result.Keywords.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<QualityAssessment> AssessQualityAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("FileFlux 품질 평가 시작");

        var systemPrompt = "You are a content quality assessor. Evaluate the given content for confidence, completeness, and consistency. Score each from 0.0 to 1.0. Provide recommendations for improvement.";

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            System = systemPrompt,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = 0.3f,
            MaxTokens = 1000
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var resultText = ExtractTextFromResponse(response);

        var result = new QualityAssessment
        {
            ConfidenceScore = 0.8,
            CompletenessScore = 0.8,
            ConsistencyScore = 0.8,
            Explanation = resultText,
            TokensUsed = response.TokenUsage?.TotalTokens ?? 0
        };

        _logger?.LogDebug("FileFlux 품질 평가 완료 - OverallScore: {Score}", result.OverallScore);
        return result;
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }
}
