using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IronHive.Flux.Rag.Context;

/// <summary>
/// RAG 컨텍스트 빌더 - FluxIndex 검색 결과를 LLM 컨텍스트로 변환
/// </summary>
public class RagContextBuilder
{
    private readonly FluxRagToolsOptions _options;
    private readonly ILogger<RagContextBuilder>? _logger;

    public RagContextBuilder(
        IOptions<FluxRagToolsOptions> options,
        ILogger<RagContextBuilder>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <summary>
    /// 검색 결과로부터 RAG 컨텍스트를 구축합니다.
    /// </summary>
    /// <param name="searchResults">검색 결과 목록</param>
    /// <param name="options">컨텍스트 빌더 옵션</param>
    /// <returns>구축된 RAG 컨텍스트</returns>
    public RagContext BuildContext(
        IEnumerable<RagSearchResult> searchResults,
        RagContextOptions? options = null)
    {
        var results = searchResults.ToList();
        _logger?.LogDebug("RAG 컨텍스트 빌드 시작 - ResultCount: {Count}", results.Count);

        var maxTokens = options?.MaxTokens ?? _options.MaxContextTokens;
        var minScore = options?.MinScore ?? _options.DefaultMinScore;

        // 점수 필터링 및 정렬
        var filteredResults = results
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score)
            .ToList();

        // 토큰 제한에 맞춰 결과 선택
        var selectedResults = new List<RagSearchResult>();
        var currentTokens = 0;

        foreach (var result in filteredResults)
        {
            var resultTokens = EstimateTokens(result.Content);
            if (currentTokens + resultTokens > maxTokens && selectedResults.Count > 0)
                break;

            selectedResults.Add(result);
            currentTokens += resultTokens;
        }

        // 컨텍스트 텍스트 생성
        var contextText = BuildContextText(selectedResults);

        var context = new RagContext
        {
            ContextText = contextText,
            Sources = selectedResults,
            TokenCount = currentTokens,
            AverageRelevance = selectedResults.Count > 0 ? selectedResults.Average(r => r.Score) : 0,
            SearchStrategy = options?.Strategy ?? _options.DefaultSearchStrategy
        };

        _logger?.LogDebug("RAG 컨텍스트 빌드 완료 - SelectedCount: {Count}, Tokens: {Tokens}",
            selectedResults.Count, currentTokens);

        return context;
    }

    /// <summary>
    /// 비동기 검색 및 컨텍스트 구축
    /// </summary>
    /// <param name="searchFunc">검색 함수</param>
    /// <param name="options">컨텍스트 빌더 옵션</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>구축된 RAG 컨텍스트</returns>
    public async Task<RagContext> BuildContextAsync(
        Func<Task<IEnumerable<RagSearchResult>>> searchFunc,
        RagContextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var searchResults = await searchFunc();
        return BuildContext(searchResults, options);
    }

    private string BuildContextText(IReadOnlyList<RagSearchResult> results)
    {
        if (results.Count == 0)
            return "관련 정보를 찾을 수 없습니다.";

        var contextParts = new List<string>();

        for (var i = 0; i < results.Count; i++)
        {
            var result = results[i];
            var header = result.Title != null
                ? $"[Source {i + 1}: {result.Title}]"
                : $"[Source {i + 1}]";

            contextParts.Add($"{header}\n{result.Content}");
        }

        return string.Join(_options.ChunkSeparator, contextParts);
    }

    private static int EstimateTokens(string text)
    {
        // 대략적인 토큰 추정: 1 토큰 ≈ 4 문자 (영어 기준)
        // 한국어의 경우 더 많은 토큰이 사용됨
        var koreanCount = text.Count(c => c >= 0xAC00 && c <= 0xD7A3);
        var otherCount = text.Length - koreanCount;

        // 한국어: 약 1.5 토큰/문자, 영어: 약 0.25 토큰/문자
        return (int)(koreanCount * 1.5 + otherCount * 0.25);
    }
}
