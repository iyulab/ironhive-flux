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
}

/// <summary>
/// RAG 컨텍스트 빌더 옵션
/// </summary>
public class RagContextOptions
{
    /// <summary>
    /// 검색 쿼리
    /// </summary>
    public required string Query { get; set; }

    /// <summary>
    /// 최대 결과 수
    /// </summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>
    /// 검색 전략 (vector, hybrid, keyword)
    /// </summary>
    public string Strategy { get; set; } = "hybrid";

    /// <summary>
    /// 최소 관련성 점수
    /// </summary>
    public float MinScore { get; set; } = 0.5f;

    /// <summary>
    /// 최대 컨텍스트 토큰 수
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// 메타데이터 필터
    /// </summary>
    public IDictionary<string, object>? MetadataFilter { get; set; }
}
