using FileFlux;
using FileFlux.Core;
using IronHive.Core.Tools;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Agent.Tools.FileFlux;

/// <summary>
/// 메타데이터 추출 도구 - FileFlux를 사용하여 문서 메타데이터 추출
/// </summary>
public class MetadataExtractorTool
{
    private readonly ITextCompletionService? _textCompletionService;
    private readonly ILogger<MetadataExtractorTool>? _logger;

    public MetadataExtractorTool(
        ITextCompletionService? textCompletionService = null,
        ILogger<MetadataExtractorTool>? logger = null)
    {
        _textCompletionService = textCompletionService;
        _logger = logger;
    }

    /// <summary>
    /// 문서에서 메타데이터를 추출합니다.
    /// </summary>
    /// <param name="content">메타데이터를 추출할 문서 내용</param>
    /// <param name="documentType">문서 타입</param>
    /// <returns>추출된 메타데이터 (JSON 문자열)</returns>
    [FunctionTool("extract_document_metadata")]
    [Description("문서에서 키워드, 언어, 카테고리, 엔티티 등 메타데이터를 추출합니다.")]
    public async Task<string> ExtractMetadataAsync(
        [Description("메타데이터를 추출할 문서 내용")] string content,
        [Description("문서 타입 (article, code, manual, report, general). 기본값: general")] string? documentType = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("메타데이터 추출 시작 - ContentLength: {Length}", content.Length);

        try
        {
            var docType = ParseDocumentType(documentType ?? "general");

            if (_textCompletionService != null)
            {
                // LLM 기반 메타데이터 추출
                var extractionPrompt = $"""
                    Extract metadata from the following document:
                    1. Keywords (top 10)
                    2. Language
                    3. Categories
                    4. Named entities (people, places, organizations)
                    5. Technical terms (if any)

                    Return as JSON format.

                    Document content:
                    {content[..Math.Min(content.Length, 5000)]}
                    """;

                var result = await _textCompletionService.ExtractMetadataAsync(
                    extractionPrompt, docType, cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    documentType = docType.ToString(),
                    confidence = result.Confidence,
                    keywords = result.Keywords,
                    language = result.Language,
                    categories = result.Categories,
                    entities = result.Entities,
                    technicalMetadata = result.TechnicalMetadata,
                    tokensUsed = result.TokensUsed
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                // 기본 메타데이터 추출 (LLM 없이)
                var basicMetadata = ExtractBasicMetadata(content);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    documentType = docType.ToString(),
                    confidence = 0.5,
                    keywords = basicMetadata.Keywords,
                    language = basicMetadata.Language,
                    wordCount = basicMetadata.WordCount,
                    characterCount = content.Length,
                    message = "LLM 서비스 없이 기본 분석만 수행됨"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "메타데이터 추출 실패");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static DocumentType ParseDocumentType(string type)
    {
        // FileFlux DocumentType은 파일 형식 기반 (Pdf, Word, Excel 등)
        // 콘텐츠 유형 힌트는 Unknown을 사용하고 프롬프트에서 처리
        return type.ToLower() switch
        {
            "pdf" => DocumentType.Pdf,
            "word" or "docx" => DocumentType.Word,
            "excel" or "xlsx" => DocumentType.Excel,
            "powerpoint" or "pptx" => DocumentType.PowerPoint,
            "text" or "txt" => DocumentType.Text,
            "markdown" or "md" => DocumentType.Markdown,
            "html" => DocumentType.Html,
            "csv" => DocumentType.Csv,
            "json" => DocumentType.Json,
            _ => DocumentType.Unknown
        };
    }

    private static (string[] Keywords, string Language, int WordCount) ExtractBasicMetadata(string content)
    {
        // 단어 빈도 기반 키워드 추출
        var words = content
            .ToLower()
            .Split([' ', '\n', '\t', '\r', '.', ',', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}'],
                StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToArray();

        // 간단한 언어 감지 (한글/영어)
        var koreanCount = content.Count(c => c >= 0xAC00 && c <= 0xD7A3);
        var totalChars = content.Count(c => char.IsLetter(c));
        var language = koreanCount > totalChars * 0.3 ? "ko" : "en";

        var wordCount = content.Split([' ', '\n', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;

        return (words, language, wordCount);
    }
}
