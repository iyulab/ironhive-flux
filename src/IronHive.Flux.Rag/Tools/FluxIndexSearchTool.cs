using FluxIndex.Core.Application.Interfaces;
using FluxFeed.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 검색 도구 - IVault를 통한 벡터+키워드 하이브리드 검색
/// 선택적 IReranker 지원으로 검색 품질 향상
/// </summary>
public partial class FluxIndexSearchTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly RagContextBuilder _contextBuilder;
    private readonly IVault _vault;
    private readonly IReranker? _reranker;
    private readonly ILogger<FluxIndexSearchTool>? _logger;

    public FluxIndexSearchTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        RagContextBuilder contextBuilder,
        IReranker? reranker = null,
        ILogger<FluxIndexSearchTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _reranker = reranker;
        _logger = logger;
    }

    /// <summary>
    /// 지식 베이스에서 쿼리와 관련된 정보를 검색합니다.
    /// </summary>
    /// <param name="query">검색 쿼리</param>
    /// <param name="maxResults">최대 결과 수</param>
    /// <param name="minScore">최소 관련성 점수 (0.0 - 1.0)</param>
    /// <param name="pathScope">검색 범위 경로 (폴더 또는 파일)</param>
    /// <returns>검색 결과 및 RAG 컨텍스트 (JSON 문자열)</returns>
    [FunctionTool("search_knowledge_base")]
    [Description("지식 베이스에서 쿼리와 관련된 정보를 검색합니다. RAG 시스템의 핵심 검색 기능입니다.")]
    public async Task<string> SearchAsync(
        [Description("검색할 쿼리")] string query,
        [Description("최대 결과 수. 기본값: 5")] int? maxResults = null,
        [Description("최소 관련성 점수 (0.0 - 1.0). 기본값: 0.5")] float? minScore = null,
        [Description("검색 범위 경로 (폴더 또는 파일 경로)")] string? pathScope = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogSearchStarted(_logger, query);

        try
        {
            var topK = maxResults ?? _options.DefaultMaxResults;
            var min = minScore ?? _options.DefaultMinScore;

            // Over-fetch when reranking is enabled for better recall
            var fetchK = _reranker is not null ? topK * 2 : topK;

            var searchOptions = new VaultSearchOptions
            {
                TopK = fetchK,
                MinScore = min,
                IncludeContent = true,
                IncludeMetadata = true,
                PathScope = string.IsNullOrEmpty(pathScope) ? [] : [pathScope]
            };

            var vaultResult = await _vault.SearchAsync(query, searchOptions, cancellationToken);

            if (!vaultResult.IsSuccess)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    query,
                    error = vaultResult.ErrorMessage ?? "Search failed"
                }, s_indentedJsonOptions);
            }

            // VaultSearchResultItem → RagSearchResult 변환 (with rich metadata extraction)
            var searchResults = vaultResult.Items
                .Select(MapToSearchResult)
                .ToList();

            // Apply reranking if available
            if (_reranker is not null && searchResults.Count > 0)
            {
                searchResults = await RerankResultsAsync(query, searchResults, topK, cancellationToken);
            }
            else if (searchResults.Count > topK)
            {
                // Trim to topK when reranker is not available
                searchResults = searchResults.Take(topK).ToList();
            }

            // RAG 컨텍스트 빌드
            var contextOptions = new RagContextOptions
            {
                Query = query,
                MaxResults = topK,
                MinScore = min,
                MaxTokens = _options.MaxContextTokens
            };

            var context = _contextBuilder.BuildContext(searchResults, contextOptions);

            var result = new
            {
                success = true,
                query,
                resultCount = context.Sources.Count,
                totalFound = vaultResult.TotalCount,
                averageScore = context.AverageRelevance,
                tokenCount = context.TokenCount,
                searchDuration = vaultResult.Duration.TotalMilliseconds,
                reranked = _reranker is not null,
                context = context.ContextText,
                sources = context.Sources.Select(s => new
                {
                    documentId = s.DocumentId,
                    title = s.Title,
                    score = s.Score,
                    chunkIndex = s.ChunkIndex,
                    breadcrumb = s.Breadcrumb,
                    documentTopic = s.DocumentTopic,
                    keywords = s.Keywords,
                    qualityScore = s.QualityScore,
                    structuralRole = s.StructuralRole,
                    preview = s.Content.Length > 200 ? s.Content[..200] + "..." : s.Content
                })
            };

            if (_logger is not null)
                LogSearchCompleted(_logger, context.Sources.Count);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogSearchFailed(_logger, ex, query);
            return JsonSerializer.Serialize(new
            {
                success = false,
                query,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
    }

    #region Metadata Extraction

    internal static RagSearchResult MapToSearchResult(VaultSearchResultItem item)
    {
        var metadata = item.Metadata;

        return new RagSearchResult
        {
            DocumentId = item.SourcePath,
            Content = item.Content ?? string.Empty,
            Score = item.Score,
            Title = item.FileName,
            Metadata = item.Metadata,
            ChunkIndex = item.ChunkIndex,
            // Rich metadata from FileFlux enrichment pipeline
            Breadcrumb = GetString(metadata, "context.breadcrumb"),
            DocumentTopic = GetString(metadata, "document.topic"),
            Keywords = GetString(metadata, "document.keywords"),
            QualityScore = GetFloat(metadata, "quality.overall"),
            StructuralRole = GetString(metadata, "content.structuralRole"),
        };
    }

    internal static string? GetString(Dictionary<string, object>? metadata, string key)
        => metadata is not null && metadata.TryGetValue(key, out var val)
            ? val as string ?? val.ToString()
            : null;

    internal static float? GetFloat(Dictionary<string, object>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var val)) return null;
        return val switch
        {
            float f => f,
            double d => (float)d,
            int i => i,
            long l => l,
            string s when float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    #endregion

    #region Reranking

    private async Task<List<RagSearchResult>> RerankResultsAsync(
        string query,
        List<RagSearchResult> items,
        int topK,
        CancellationToken cancellationToken)
    {
        if (_logger is not null)
            LogReranking(_logger, items.Count, topK);

        var candidates = items.Select((item, index) => new RetrievalCandidate
        {
            Id = $"{item.DocumentId}:{item.ChunkIndex}",
            Content = item.Content,
            InitialScore = item.Score,
            InitialRank = index + 1
        });

        var rerankOptions = new RerankOptions { TopN = topK };
        var reranked = await _reranker!.RerankAsync(query, candidates, rerankOptions, cancellationToken);

        // Map back to RagSearchResult using the original items
        var itemLookup = items.ToDictionary(
            item => $"{item.DocumentId}:{item.ChunkIndex}",
            item => item);

        var result = new List<RagSearchResult>();
        foreach (var r in reranked)
        {
            if (itemLookup.TryGetValue(r.Id, out var original))
            {
                result.Add(original with { Score = r.RerankScore });
            }
        }

        if (_logger is not null)
            LogRerankCompleted(_logger, items.Count, result.Count);

        return result;
    }

    #endregion

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "Knowledge base search started - Query: {Query}")]
    private static partial void LogSearchStarted(ILogger logger, string Query);

    [LoggerMessage(Level = LogLevel.Information, Message = "Knowledge base search completed - ResultCount: {Count}")]
    private static partial void LogSearchCompleted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Knowledge base search failed - Query: {Query}")]
    private static partial void LogSearchFailed(ILogger logger, Exception ex, string Query);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Reranking {CandidateCount} candidates → topK={TopK}")]
    private static partial void LogReranking(ILogger logger, int CandidateCount, int TopK);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Reranking completed - {InputCount} → {OutputCount} results")]
    private static partial void LogRerankCompleted(ILogger logger, int InputCount, int OutputCount);

    #endregion
}
