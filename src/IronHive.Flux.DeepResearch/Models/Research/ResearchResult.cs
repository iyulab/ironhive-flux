namespace IronHive.Flux.DeepResearch.Models.Research;

/// <summary>
/// 리서치 결과
/// </summary>
public record ResearchResult
{
    /// <summary>
    /// 세션 ID
    /// </summary>
    public required string SessionId { get; init; }

    /// <summary>
    /// 생성된 보고서
    /// </summary>
    public required string Report { get; init; }

    /// <summary>
    /// 보고서 섹션들 (구조화된 형태)
    /// </summary>
    public required IReadOnlyList<ReportSection> Sections { get; init; }

    /// <summary>
    /// 참조된 소스 목록
    /// </summary>
    public required IReadOnlyList<SourceDocument> Sources { get; init; }

    /// <summary>
    /// 인용 정보
    /// </summary>
    public required IReadOnlyList<Citation> Citations { get; init; }

    /// <summary>
    /// 메타데이터
    /// </summary>
    public required ResearchMetadata Metadata { get; init; }

    /// <summary>
    /// 발생한 오류 목록
    /// </summary>
    public IReadOnlyList<ResearchError> Errors { get; init; } = [];

    /// <summary>
    /// 부분 결과 여부
    /// </summary>
    public bool IsPartial { get; init; } = false;
}

/// <summary>
/// 리서치 메타데이터
/// </summary>
public record ResearchMetadata
{
    public required int IterationCount { get; init; }
    public required int TotalQueriesExecuted { get; init; }
    public required int TotalSourcesAnalyzed { get; init; }
    public required TimeSpan Duration { get; init; }
    public required TokenUsage TokenUsage { get; init; }
    public required decimal EstimatedCost { get; init; }
    public required SufficiencyScore FinalSufficiencyScore { get; init; }
}

/// <summary>
/// 토큰 사용량
/// </summary>
public record TokenUsage
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
}

/// <summary>
/// 보고서 섹션
/// </summary>
public record ReportSection
{
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required int Order { get; init; }
    public IReadOnlyList<string> RelatedFindings { get; init; } = [];
}

/// <summary>
/// 인용 정보
/// </summary>
public record Citation
{
    public required int Number { get; init; }
    public required string SourceId { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public string? Author { get; init; }
    public DateTimeOffset? PublishedDate { get; init; }
    public required DateTimeOffset AccessedDate { get; init; }
}

/// <summary>
/// 리서치 오류
/// </summary>
public record ResearchError
{
    public required ResearchErrorType Type { get; init; }
    public required string Message { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public string? Details { get; init; }
}

/// <summary>
/// 리서치 오류 유형
/// </summary>
public enum ResearchErrorType
{
    SearchProviderError,
    ContentExtractionError,
    LLMError,
    BudgetExceeded,
    TimeoutExceeded,
    InsufficientSources,
    Unknown
}
