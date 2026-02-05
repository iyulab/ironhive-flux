namespace IronHive.Flux.Agent.Options;

/// <summary>
/// Flux 에이전트 도구 옵션
/// </summary>
public class FluxAgentToolsOptions
{
    /// <summary>
    /// FileFlux 도구 활성화 여부
    /// </summary>
    public bool EnableFileFluxTools { get; set; } = true;

    /// <summary>
    /// WebFlux 도구 활성화 여부
    /// </summary>
    public bool EnableWebFluxTools { get; set; } = true;

    /// <summary>
    /// 기본 청킹 전략
    /// </summary>
    public string DefaultChunkingStrategy { get; set; } = "semantic";

    /// <summary>
    /// 기본 최대 청크 크기
    /// </summary>
    public int DefaultMaxChunkSize { get; set; } = 1000;

    /// <summary>
    /// 기본 청크 오버랩
    /// </summary>
    public int DefaultChunkOverlap { get; set; } = 100;

    /// <summary>
    /// 웹 크롤링 최대 깊이
    /// </summary>
    public int DefaultMaxCrawlDepth { get; set; } = 2;

    /// <summary>
    /// 웹 크롤링 시 이미지 추출 여부
    /// </summary>
    public bool DefaultExtractImages { get; set; } = false;

    /// <summary>
    /// 도구 실행 타임아웃 (초)
    /// </summary>
    public int ToolTimeout { get; set; } = 120;
}
