using IronHive.Core.Tools;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 검색 도구 - 지식 베이스에서 관련 정보 검색
/// </summary>
public class FluxIndexSearchTool
{
    private readonly FluxRagToolsOptions _options;
    private readonly RagContextBuilder _contextBuilder;
    private readonly ILogger<FluxIndexSearchTool>? _logger;

    // 인메모리 간단 저장소 (실제로는 FluxIndex SDK 사용)
    private static readonly Dictionary<string, List<StoredDocument>> _storage = new();
    private static readonly object _storageLock = new();

    public FluxIndexSearchTool(
        IOptions<FluxRagToolsOptions> options,
        RagContextBuilder contextBuilder,
        ILogger<FluxIndexSearchTool>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        _logger = logger;
    }

    /// <summary>
    /// 지식 베이스에서 쿼리와 관련된 정보를 검색합니다.
    /// </summary>
    /// <param name="query">검색 쿼리</param>
    /// <param name="maxResults">최대 결과 수</param>
    /// <param name="strategy">검색 전략 (vector, hybrid, keyword)</param>
    /// <param name="minScore">최소 관련성 점수 (0.0 - 1.0)</param>
    /// <param name="indexName">인덱스 이름</param>
    /// <returns>검색 결과 및 RAG 컨텍스트 (JSON 문자열)</returns>
    [FunctionTool("search_knowledge_base")]
    [Description("지식 베이스에서 쿼리와 관련된 정보를 검색합니다. RAG 시스템의 핵심 검색 기능입니다.")]
    public Task<string> SearchAsync(
        [Description("검색할 쿼리")] string query,
        [Description("최대 결과 수. 기본값: 5")] int? maxResults = null,
        [Description("검색 전략 (vector, hybrid, keyword). 기본값: hybrid")] string? strategy = null,
        [Description("최소 관련성 점수 (0.0 - 1.0). 기본값: 0.5")] float? minScore = null,
        [Description("인덱스 이름")] string? indexName = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("지식 베이스 검색 시작 - Query: {Query}", query);

        try
        {
            var max = maxResults ?? _options.DefaultMaxResults;
            var searchStrategy = strategy ?? _options.DefaultSearchStrategy;
            var min = minScore ?? _options.DefaultMinScore;
            var index = indexName ?? _options.DefaultIndexName;

            // 검색 수행
            var searchResults = PerformSearch(query, max, searchStrategy, min, index);

            // RAG 컨텍스트 빌드
            var contextOptions = new RagContextOptions
            {
                Query = query,
                MaxResults = max,
                Strategy = searchStrategy,
                MinScore = min,
                IndexName = index
            };

            var context = _contextBuilder.BuildContext(searchResults, contextOptions);

            var result = new
            {
                success = true,
                query,
                strategy = searchStrategy,
                resultCount = context.Sources.Count,
                averageScore = context.AverageRelevance,
                tokenCount = context.TokenCount,
                context = context.ContextText,
                sources = context.Sources.Select(s => new
                {
                    documentId = s.DocumentId,
                    title = s.Title,
                    score = s.Score,
                    preview = s.Content.Length > 200 ? s.Content[..200] + "..." : s.Content
                })
            };

            _logger?.LogInformation("지식 베이스 검색 완료 - ResultCount: {Count}", context.Sources.Count);
            return Task.FromResult(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "지식 베이스 검색 실패 - Query: {Query}", query);
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                query,
                error = ex.Message
            }));
        }
    }

    private List<RagSearchResult> PerformSearch(string query, int maxResults, string strategy, float minScore, string indexName)
    {
        lock (_storageLock)
        {
            if (!_storage.TryGetValue(indexName, out var documents) || documents.Count == 0)
            {
                return [];
            }

            // 간단한 키워드 매칭 검색 (실제로는 벡터 검색 사용)
            var queryWords = query.ToLower().Split([' ', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries);

            var results = documents
                .Select(doc =>
                {
                    var contentLower = doc.Content.ToLower();
                    var matchCount = queryWords.Count(w => contentLower.Contains(w));
                    var score = queryWords.Length > 0 ? (float)matchCount / queryWords.Length : 0;

                    return new RagSearchResult
                    {
                        DocumentId = doc.Id,
                        Content = doc.Content,
                        Score = score,
                        Title = doc.Metadata?.GetValueOrDefault("title")?.ToString(),
                        Metadata = doc.Metadata,
                        ChunkIndex = doc.ChunkIndex
                    };
                })
                .Where(r => r.Score >= minScore)
                .OrderByDescending(r => r.Score)
                .Take(maxResults)
                .ToList();

            return results;
        }
    }

    // 내부 테스트용 저장소 접근
    internal static void AddDocument(string indexName, StoredDocument document)
    {
        lock (_storageLock)
        {
            if (!_storage.TryGetValue(indexName, out var documents))
            {
                documents = [];
                _storage[indexName] = documents;
            }
            documents.Add(document);
        }
    }

    internal static void ClearIndex(string indexName)
    {
        lock (_storageLock)
        {
            if (_storage.ContainsKey(indexName))
                _storage[indexName].Clear();
        }
    }
}

internal class StoredDocument
{
    public required string Id { get; set; }
    public required string Content { get; set; }
    public float[]? Embedding { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public int? ChunkIndex { get; set; }
}
