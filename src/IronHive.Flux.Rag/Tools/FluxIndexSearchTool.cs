using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 검색 도구 - IVault를 통한 벡터+키워드 하이브리드 검색
/// </summary>
public partial class FluxIndexSearchTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly RagContextBuilder _contextBuilder;
    private readonly IVault _vault;
    private readonly ILogger<FluxIndexSearchTool>? _logger;

    public FluxIndexSearchTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        RagContextBuilder contextBuilder,
        ILogger<FluxIndexSearchTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
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
            var max = maxResults ?? _options.DefaultMaxResults;
            var min = minScore ?? _options.DefaultMinScore;

            var searchOptions = new VaultSearchOptions
            {
                TopK = max,
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

            // VaultSearchResultItem → RagSearchResult 변환
            var searchResults = vaultResult.Items
                .Select(item => new RagSearchResult
                {
                    DocumentId = item.SourcePath,
                    Content = item.Content ?? string.Empty,
                    Score = item.Score,
                    Title = item.FileName,
                    Metadata = item.Metadata,
                    ChunkIndex = item.ChunkIndex
                })
                .ToList();

            // RAG 컨텍스트 빌드
            var contextOptions = new RagContextOptions
            {
                Query = query,
                MaxResults = max,
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
                context = context.ContextText,
                sources = context.Sources.Select(s => new
                {
                    documentId = s.DocumentId,
                    title = s.Title,
                    score = s.Score,
                    chunkIndex = s.ChunkIndex,
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

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "Knowledge base search started - Query: {Query}")]
    private static partial void LogSearchStarted(ILogger logger, string Query);

    [LoggerMessage(Level = LogLevel.Information, Message = "Knowledge base search completed - ResultCount: {Count}")]
    private static partial void LogSearchCompleted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Knowledge base search failed - Query: {Query}")]
    private static partial void LogSearchFailed(ILogger logger, Exception ex, string Query);

    #endregion
}
