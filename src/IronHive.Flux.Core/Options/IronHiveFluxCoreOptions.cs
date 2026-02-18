namespace IronHive.Flux.Core.Options;

/// <summary>
/// IronHive.Flux.Core 어댑터 설정 옵션
/// </summary>
public class IronHiveFluxCoreOptions
{
    /// <summary>
    /// 임베딩 모델 ID (기본값: text-embedding-3-small)
    /// </summary>
    public string EmbeddingModelId { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// 텍스트 완성 모델 ID (기본값: gpt-4o)
    /// </summary>
    public string TextCompletionModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// 이미지-텍스트 변환 모델 ID (기본값: gpt-4o)
    /// </summary>
    public string ImageToTextModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// 임베딩 벡터 차원 수 (0이면 모델 기본값 사용)
    /// </summary>
    public int EmbeddingDimension { get; set; }

    /// <summary>
    /// 최대 토큰 수 (기본값: 8191)
    /// </summary>
    public int MaxTokens { get; set; } = 8191;

    /// <summary>
    /// 텍스트 완성 기본 Temperature (0.0 - 1.0)
    /// </summary>
    public float DefaultTemperature { get; set; } = 0.7f;

    /// <summary>
    /// 텍스트 완성 기본 MaxTokens
    /// </summary>
    public int DefaultCompletionMaxTokens { get; set; } = 500;
}
