namespace IronHive.Flux.DeepResearch.Abstractions;

/// <summary>
/// 정보 충분성 평가 인터페이스 (LLM-as-Judge 패턴)
/// </summary>
public interface ISufficiencyEvaluator
{
    /// <summary>
    /// 현재 수집된 정보의 충분성 평가
    /// </summary>
    Task<SufficiencyScore> EvaluateAsync(
        ResearchState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 정보 갭 식별
    /// </summary>
    Task<IReadOnlyList<InformationGap>> IdentifyGapsAsync(
        ResearchState state,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 리서치 오케스트레이션 인터페이스
/// </summary>
public interface IResearchOrchestrator
{
    /// <summary>
    /// 리서치 워크플로우 실행
    /// </summary>
    IAsyncEnumerable<ResearchProgress> ExecuteAsync(
        ResearchState initialState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 체크포인트에서 재개
    /// </summary>
    IAsyncEnumerable<ResearchProgress> ResumeFromCheckpointAsync(
        ResearchCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 보고서 생성 인터페이스
/// </summary>
public interface IReportGenerator
{
    /// <summary>
    /// 보고서 생성 (스트리밍)
    /// </summary>
    IAsyncEnumerable<ResearchProgress> GenerateAsync(
        ResearchState state,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 콘텐츠 추출 인터페이스
/// </summary>
public interface IContentExtractor
{
    /// <summary>
    /// URL에서 콘텐츠 추출
    /// </summary>
    Task<ExtractedContent> ExtractAsync(
        string url,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 배치 콘텐츠 추출
    /// </summary>
    Task<IReadOnlyList<ExtractedContent>> ExtractBatchAsync(
        IEnumerable<string> urls,
        CancellationToken cancellationToken = default);
}
