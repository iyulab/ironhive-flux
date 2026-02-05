using FileFlux;
using FileFlux.Core;
using IronHive.Core.Tools;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Agent.Tools.FileFlux;

/// <summary>
/// 문서 구조 분석 도구 - FileFlux를 사용하여 문서 구조 분석
/// </summary>
public class DocumentAnalyzerTool
{
    private readonly ITextCompletionService? _textCompletionService;
    private readonly ILogger<DocumentAnalyzerTool>? _logger;

    public DocumentAnalyzerTool(
        ITextCompletionService? textCompletionService = null,
        ILogger<DocumentAnalyzerTool>? logger = null)
    {
        _textCompletionService = textCompletionService;
        _logger = logger;
    }

    /// <summary>
    /// 문서 구조를 분석합니다.
    /// </summary>
    /// <param name="content">분석할 문서 내용</param>
    /// <param name="documentType">문서 타입 (article, code, manual, report 등)</param>
    /// <returns>구조 분석 결과 (JSON 문자열)</returns>
    [FunctionTool("analyze_document_structure")]
    [Description("문서의 구조를 분석하여 섹션, 제목, 계층 구조 등을 파악합니다.")]
    public async Task<string> AnalyzeStructureAsync(
        [Description("분석할 문서 내용")] string content,
        [Description("문서 타입 (article, code, manual, report, general). 기본값: general")] string? documentType = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("문서 구조 분석 시작 - ContentLength: {Length}", content.Length);

        try
        {
            var docType = ParseDocumentType(documentType ?? "general");

            if (_textCompletionService != null)
            {
                // LLM 기반 구조 분석
                var analysisPrompt = $"""
                    Analyze the structure of the following document and identify:
                    1. Main sections and their hierarchy
                    2. Key topics covered
                    3. Document organization pattern

                    Document content:
                    {content[..Math.Min(content.Length, 5000)]}
                    """;

                var result = await _textCompletionService.AnalyzeStructureAsync(
                    analysisPrompt, docType, cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    documentType = docType.ToString(),
                    confidence = result.Confidence,
                    sectionCount = result.Sections.Count,
                    sections = result.Sections.Select(s => new
                    {
                        title = s.Title,
                        type = s.Type.ToString(),
                        level = s.Level,
                        importance = s.Importance
                    }),
                    rawAnalysis = result.RawResponse,
                    tokensUsed = result.TokensUsed
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                // 기본 구조 분석 (LLM 없이)
                var sections = AnalyzeBasicStructure(content);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    documentType = docType.ToString(),
                    confidence = 0.6,
                    sectionCount = sections.Count,
                    sections,
                    message = "LLM 서비스 없이 기본 분석만 수행됨"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "문서 구조 분석 실패");
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

    private static List<object> AnalyzeBasicStructure(string content)
    {
        var sections = new List<object>();
        var lines = content.Split('\n');
        var currentSection = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Markdown 헤딩 감지
            if (trimmed.StartsWith('#'))
            {
                var level = trimmed.TakeWhile(c => c == '#').Count();
                var title = trimmed.TrimStart('#').Trim();
                sections.Add(new
                {
                    title,
                    type = "heading",
                    level,
                    index = currentSection++
                });
            }
            // 숫자 섹션 헤딩 감지 (1. Title, 1.1 Subtitle)
            else if (char.IsDigit(trimmed[0]) && trimmed.Contains('.'))
            {
                var parts = trimmed.Split([' ', '\t'], 2);
                if (parts.Length > 1 && parts[0].All(c => char.IsDigit(c) || c == '.'))
                {
                    var level = parts[0].Count(c => c == '.');
                    sections.Add(new
                    {
                        title = parts[1],
                        type = "numbered_section",
                        level = Math.Max(1, level),
                        index = currentSection++
                    });
                }
            }
        }

        return sections;
    }
}
