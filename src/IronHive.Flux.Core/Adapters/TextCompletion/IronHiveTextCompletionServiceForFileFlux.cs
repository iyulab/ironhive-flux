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
public partial class IronHiveTextCompletionServiceForFileFlux : FileFlux.ITextCompletionService
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
        if (_logger is not null)
            LogTextCompletionStarted(_logger, prompt.Length);

        var request = new MessageGenerationRequest
        {
            Model = _options.TextCompletionModelId,
            Messages = [new UserMessage { Content = [new TextMessageContent { Value = prompt }] }],
            Temperature = _options.DefaultTemperature,
            MaxTokens = _options.DefaultCompletionMaxTokens
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        if (_logger is not null)
            LogTextCompletionCompleted(_logger, result.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<StructureAnalysisResult> AnalyzeStructureAsync(
        string prompt,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogStructureAnalysisStarted(_logger, documentType);

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
            Confidence = EstimateConfidence(resultText, minExpectedLength: 20)
        };

        if (_logger is not null)
            LogStructureAnalysisCompleted(_logger, result.TokensUsed);
        return result;
    }

    /// <inheritdoc />
    public async Task<ContentSummary> SummarizeContentAsync(
        string prompt,
        int maxLength = 200,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogSummarizeStarted(_logger, maxLength);

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

        var summaryText = resultText.Length > maxLength ? resultText[..maxLength] : resultText;
        // Confidence: penalize if response was empty; boost if no truncation was needed
        var summaryConfidence = string.IsNullOrWhiteSpace(resultText) ? 0.3
            : resultText.Length <= maxLength ? 0.9
            : 0.75;

        var result = new ContentSummary
        {
            Summary = summaryText,
            OriginalLength = prompt.Length,
            TokensUsed = response.TokenUsage?.TotalTokens ?? 0,
            Confidence = summaryConfidence
        };

        if (_logger is not null)
            LogSummarizeCompleted(_logger, result.Summary.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<MetadataExtractionResult> ExtractMetadataAsync(
        string prompt,
        DocumentType documentType,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogMetadataExtractionStarted(_logger, documentType);

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
            Confidence = 0.3 // Default: no structured data extracted
        };

        try
        {
            var jsonStart = resultText.IndexOf('{');
            var jsonEnd = resultText.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = resultText[jsonStart..(jsonEnd + 1)];
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);
                var fieldsExtracted = 0;

                if (parsed?.TryGetValue("keywords", out var keywords) == true)
                {
                    result.Keywords = keywords.EnumerateArray().Select(k => k.GetString() ?? "").ToArray();
                    fieldsExtracted++;
                }

                if (parsed?.TryGetValue("language", out var language) == true)
                {
                    result.Language = language.GetString();
                    fieldsExtracted++;
                }

                if (parsed?.TryGetValue("categories", out var categories) == true)
                {
                    result.Categories = categories.EnumerateArray().Select(c => c.GetString() ?? "").ToArray();
                    fieldsExtracted++;
                }

                // Confidence based on fields extracted: 0/3 → 0.5, 1/3 → 0.65, 2/3 → 0.8, 3/3 → 0.95
                result.Confidence = 0.5 + (fieldsExtracted * 0.15);
            }
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogMetadataJsonParseFailed(_logger, ex);
            result.Confidence = 0.2;
        }

        if (_logger is not null)
            LogMetadataExtractionCompleted(_logger, result.Keywords.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<QualityAssessment> AssessQualityAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogQualityAssessmentStarted(_logger);

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

        var (confidence, completeness, consistency) = TryParseQualityScores(resultText);
        var result = new QualityAssessment
        {
            ConfidenceScore = confidence,
            CompletenessScore = completeness,
            ConsistencyScore = consistency,
            Explanation = resultText,
            TokensUsed = response.TokenUsage?.TotalTokens ?? 0
        };

        if (_logger is not null)
            LogQualityAssessmentCompleted(_logger, result.OverallScore);
        return result;
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }

    /// <summary>
    /// Estimates confidence based on response text quality.
    /// </summary>
    private static double EstimateConfidence(string responseText, int minExpectedLength = 10)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return 0.3;
        }

        return responseText.Length >= minExpectedLength ? 0.85 : 0.6;
    }

    /// <summary>
    /// Attempts to parse quality scores from LLM response text.
    /// Falls back to 0.7 for unparseable scores.
    /// </summary>
    private static (double confidence, double completeness, double consistency) TryParseQualityScores(string responseText)
    {
        const double fallback = 0.7;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return (0.3, 0.3, 0.3);
        }

        try
        {
            var jsonStart = responseText.IndexOf('{');
            var jsonEnd = responseText.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = responseText[jsonStart..(jsonEnd + 1)];
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonText);
                if (parsed is not null)
                {
                    var c = TryExtractScore(parsed, "confidence") ?? fallback;
                    var comp = TryExtractScore(parsed, "completeness") ?? fallback;
                    var cons = TryExtractScore(parsed, "consistency") ?? fallback;
                    return (c, comp, cons);
                }
            }
        }
        catch
        {
            // Fall through to defaults
        }

        return (fallback, fallback, fallback);
    }

    private static double? TryExtractScore(Dictionary<string, JsonElement> parsed, string key)
    {
        if (parsed.TryGetValue(key, out var element) &&
            (element.ValueKind == JsonValueKind.Number))
        {
            var value = element.GetDouble();
            return value is >= 0.0 and <= 1.0 ? value : null;
        }

        return null;
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 텍스트 완성 시작 - PromptLength: {Length}")]
    private static partial void LogTextCompletionStarted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 텍스트 완성 완료 - ResultLength: {Length}")]
    private static partial void LogTextCompletionCompleted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 구조 분석 시작 - DocumentType: {Type}")]
    private static partial void LogStructureAnalysisStarted(ILogger logger, DocumentType Type);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 구조 분석 완료 - TokensUsed: {Tokens}")]
    private static partial void LogStructureAnalysisCompleted(ILogger logger, int Tokens);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 요약 시작 - MaxLength: {MaxLength}")]
    private static partial void LogSummarizeStarted(ILogger logger, int MaxLength);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 요약 완료 - SummaryLength: {Length}")]
    private static partial void LogSummarizeCompleted(ILogger logger, int Length);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 메타데이터 추출 시작 - DocumentType: {Type}")]
    private static partial void LogMetadataExtractionStarted(ILogger logger, DocumentType Type);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 메타데이터 추출 완료 - Keywords: {Count}")]
    private static partial void LogMetadataExtractionCompleted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "메타데이터 JSON 파싱 실패")]
    private static partial void LogMetadataJsonParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 품질 평가 시작")]
    private static partial void LogQualityAssessmentStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 품질 평가 완료 - OverallScore: {Score}")]
    private static partial void LogQualityAssessmentCompleted(ILogger logger, double Score);

    #endregion
}
