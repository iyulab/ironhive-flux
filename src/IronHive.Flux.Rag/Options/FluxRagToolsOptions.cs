namespace IronHive.Flux.Rag.Options;

/// <summary>
/// FluxIndex RAG 도구 옵션
/// </summary>
public class FluxRagToolsOptions
{
    /// <summary>
    /// 기본 검색 결과 최대 수
    /// </summary>
    public int DefaultMaxResults { get; set; } = 5;

    /// <summary>
    /// 기본 검색 전략 (vector, hybrid, keyword)
    /// </summary>
    public string DefaultSearchStrategy { get; set; } = "hybrid";

    /// <summary>
    /// 기본 최소 관련성 점수 (0.0 - 1.0)
    /// </summary>
    public float DefaultMinScore { get; set; } = 0.5f;

    /// <summary>
    /// RAG 컨텍스트 최대 토큰 수
    /// </summary>
    public int MaxContextTokens { get; set; } = 4000;

    /// <summary>
    /// 청크 간 구분자
    /// </summary>
    public string ChunkSeparator { get; set; } = "\n\n---\n\n";

    /// <summary>
    /// 도구 실행 타임아웃 (초)
    /// </summary>
    public int ToolTimeout { get; set; } = 60;
}
