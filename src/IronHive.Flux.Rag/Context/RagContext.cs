namespace IronHive.Flux.Rag.Context;

/// <summary>
/// RAG 컨텍스트 결과
/// </summary>
public record RagContext
{
    /// <summary>
    /// 컨텍스트 텍스트 (LLM에 전달할 내용)
    /// </summary>
    public required string ContextText { get; init; }

    /// <summary>
    /// 검색된 소스 목록
    /// </summary>
    public IReadOnlyList<RagSearchResult> Sources { get; init; } = [];

    /// <summary>
    /// 총 토큰 수 (추정)
    /// </summary>
    public int TokenCount { get; init; }

    /// <summary>
    /// 평균 관련성 점수
    /// </summary>
    public float AverageRelevance { get; init; }

    /// <summary>
    /// 컨텍스트 생성 시간
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 사용된 검색 전략
    /// </summary>
    public string SearchStrategy { get; init; } = "hybrid";
}

/// <summary>
/// RAG 검색 결과 항목
/// </summary>
public record RagSearchResult
{
    /// <summary>
    /// 문서 ID
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// 청크 내용
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// 관련성 점수 (0.0 - 1.0)
    /// </summary>
    public float Score { get; init; }

    /// <summary>
    /// 메타데이터
    /// </summary>
    public IDictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// 청크 인덱스
    /// </summary>
    public int? ChunkIndex { get; init; }

    /// <summary>
    /// 원본 문서 제목
    /// </summary>
    public string? Title { get; init; }

    // === Where the chunk sits (from the vault's chunk metadata) ===

    /// <summary>
    /// What the chunk holds: <c>text</c>, <c>table</c> (rows of a table) or <c>image_description</c> (a description of a
    /// picture) — the vault's <c>chunk_kind</c>; <c>text</c> when the chunk carries none.
    /// </summary>
    public string Kind { get; init; } = "text";

    /// <summary>First page (1-based) the chunk covers, when the document has pages; null otherwise.</summary>
    public int? StartPage { get; init; }

    /// <summary>Last page (1-based) the chunk covers, when the document has pages; null otherwise.</summary>
    public int? EndPage { get; init; }

    /// <summary>
    /// The headings the chunk sits under, outermost first, joined with <c>" &gt; "</c> (<c>"Install &gt; Windows"</c>); null
    /// when the document has no headings above it.
    /// </summary>
    public string? HeadingPath { get; init; }
}

/// <summary>
/// RAG 컨텍스트 빌더 옵션 — 이미 검색된 결과에서 무엇을 컨텍스트에 실을지 정한다. 비워 둔(null) 항목은
/// <see cref="Options.FluxRagToolsOptions"/> 의 기본값을 따른다.
/// </summary>
public class RagContextOptions
{
    /// <summary>
    /// 컨텍스트에 싣는 최대 결과 수(점수순). null 이면 <see cref="Options.FluxRagToolsOptions.DefaultMaxResults"/>.
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// 결과를 낸 검색 전략(vector, hybrid, keyword) — <see cref="RagContext.SearchStrategy"/> 에 기록된다.
    /// null 이면 <see cref="Options.FluxRagToolsOptions.DefaultSearchStrategy"/>.
    /// </summary>
    public string? Strategy { get; set; }

    /// <summary>
    /// 최소 관련성 점수 — 이보다 낮은 결과는 싣지 않는다. null 이면 <see cref="Options.FluxRagToolsOptions.DefaultMinScore"/>.
    /// </summary>
    public float? MinScore { get; set; }

    /// <summary>
    /// 최대 컨텍스트 토큰 수. null 이면 <see cref="Options.FluxRagToolsOptions.MaxContextTokens"/>.
    /// </summary>
    public int? MaxTokens { get; set; }
}
