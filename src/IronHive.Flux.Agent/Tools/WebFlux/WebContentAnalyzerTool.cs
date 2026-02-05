using IronHive.Core.Tools;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using WebFlux.Core.Interfaces;

namespace IronHive.Flux.Agent.Tools.WebFlux;

/// <summary>
/// 웹 콘텐츠 분석 도구 - WebFlux를 사용하여 웹 콘텐츠 분석
/// </summary>
public class WebContentAnalyzerTool
{
    private readonly ITextCompletionService? _textCompletionService;
    private readonly ILogger<WebContentAnalyzerTool>? _logger;

    public WebContentAnalyzerTool(
        ITextCompletionService? textCompletionService = null,
        ILogger<WebContentAnalyzerTool>? logger = null)
    {
        _textCompletionService = textCompletionService;
        _logger = logger;
    }

    /// <summary>
    /// 웹 콘텐츠를 분석합니다.
    /// </summary>
    /// <param name="content">분석할 웹 콘텐츠</param>
    /// <param name="analysisType">분석 유형 (summary, sentiment, topics, entities)</param>
    /// <returns>분석 결과 (JSON 문자열)</returns>
    [FunctionTool("analyze_web_content")]
    [Description("웹 콘텐츠의 요약, 감성, 주제, 엔티티 등을 분석합니다.")]
    public async Task<string> AnalyzeAsync(
        [Description("분석할 웹 콘텐츠")] string content,
        [Description("분석 유형 (summary, sentiment, topics, entities, all). 기본값: all")] string? analysisType = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("웹 콘텐츠 분석 시작 - ContentLength: {Length}", content.Length);

        try
        {
            var type = (analysisType ?? "all").ToLower();

            if (_textCompletionService != null)
            {
                // LLM 기반 분석
                var prompt = BuildAnalysisPrompt(content, type);
                var result = await _textCompletionService.CompleteAsync(prompt, null, cancellationToken);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    analysisType = type,
                    contentLength = content.Length,
                    analysis = result
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                // 기본 분석 (LLM 없이)
                var basicAnalysis = PerformBasicAnalysis(content, type);

                return JsonSerializer.Serialize(new
                {
                    success = true,
                    analysisType = type,
                    contentLength = content.Length,
                    analysis = basicAnalysis,
                    message = "LLM 서비스 없이 기본 분석만 수행됨"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "웹 콘텐츠 분석 실패");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static string BuildAnalysisPrompt(string content, string analysisType)
    {
        var truncatedContent = content.Length > 5000 ? content[..5000] + "..." : content;

        return analysisType switch
        {
            "summary" => $"Provide a concise summary of the following web content:\n\n{truncatedContent}",
            "sentiment" => $"Analyze the sentiment (positive, negative, neutral) of the following web content:\n\n{truncatedContent}",
            "topics" => $"Identify the main topics discussed in the following web content:\n\n{truncatedContent}",
            "entities" => $"Extract named entities (people, organizations, locations, dates) from the following web content:\n\n{truncatedContent}",
            _ => $"""
                Analyze the following web content and provide:
                1. A brief summary (2-3 sentences)
                2. Main topics covered
                3. Overall sentiment
                4. Key entities mentioned

                Content:
                {truncatedContent}
                """
        };
    }

    private static object PerformBasicAnalysis(string content, string analysisType)
    {
        var words = content.Split([' ', '\n', '\t', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var sentences = content.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries);

        // 단어 빈도
        var wordFrequency = words
            .Where(w => w.Length > 4)
            .Select(w => w.ToLower().Trim(',', '.', '!', '?', '"', '\''))
            .Where(w => !string.IsNullOrEmpty(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { word = g.Key, count = g.Count() })
            .ToList();

        return new
        {
            wordCount = words.Length,
            sentenceCount = sentences.Length,
            averageWordLength = words.Length > 0 ? words.Average(w => w.Length) : 0,
            topWords = wordFrequency,
            readingTimeMinutes = Math.Ceiling(words.Length / 200.0) // 평균 읽기 속도 200 단어/분
        };
    }
}
