# IronHive.Flux.DeepResearch 상세 설계서

**버전:** 1.0
**작성일:** 2026-02-05
**설계 수준:** Level 3 (상세 설계)

---

## 1. 개요

### 1.1 목적

IronHive.Flux.DeepResearch는 사용자 질의에 대해 자율적으로 웹 검색, 정보 수집, 분석, 후속 질문 생성을 반복 수행하여 종합 리서치 보고서를 생성하는 딥리서치 시스템입니다.

### 1.2 아키텍처 위치

```
┌─────────────────────────────────────────────────────────────┐
│  IronHive.Flux.DeepResearch (소비 레이어 - 구체적 시나리오)   │
└──────────────────────┬──────────────────────────────────────┘
                       │ 사용
┌──────────────────────▼──────────────────────────────────────┐
│  IronBees.Autonomous (로우레벨 - 범용 프리미티브)             │
│  - Executor/Oracle 패턴, 반복 실행, 에이전트 로딩            │
└──────────────────────┬──────────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────────┐
│  IronHive (LLM 통신) + WebFlux (콘텐츠 추출)                 │
└─────────────────────────────────────────────────────────────┘
```

### 1.3 참조 아키텍처

| 시스템 | 핵심 패턴 | 채택 요소 |
|--------|----------|----------|
| [OpenAI Deep Research](https://platform.openai.com/docs/guides/deep-research) | MCP 기반 search/fetch 인터페이스 | 검색 프로바이더 추상화 |
| [GPT Researcher](https://github.com/assafelovic/gpt-researcher) | 7-에이전트 협업 (LangGraph) | 역할 분담 패턴 |
| [STORM (Stanford)](https://github.com/stanford-oval/storm) | Perspective-Guided Question Asking | 다관점 질문 생성 |
| [Semantic Kernel Agent](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/) | Sequential/Concurrent/Handoff 오케스트레이션 | .NET 에이전트 패턴 |

---

## 2. 프로젝트 구조

```
src/IronHive.Flux.DeepResearch/
├── IronHive.Flux.DeepResearch.csproj
├── Extensions/
│   └── ServiceCollectionExtensions.cs
├── Options/
│   └── DeepResearchOptions.cs
│
├── Abstractions/
│   ├── IDeepResearcher.cs                    # 메인 인터페이스
│   ├── ISearchProvider.cs                    # 검색 프로바이더 인터페이스
│   ├── IContentExtractor.cs                  # 콘텐츠 추출 인터페이스
│   ├── IResearchOrchestrator.cs              # 오케스트레이터 인터페이스
│   ├── ISufficiencyEvaluator.cs              # 충분성 평가 인터페이스
│   └── IReportGenerator.cs                   # 보고서 생성 인터페이스
│
├── Models/
│   ├── Research/
│   │   ├── ResearchRequest.cs
│   │   ├── ResearchResult.cs
│   │   ├── ResearchProgress.cs
│   │   └── ResearchSession.cs
│   ├── Search/
│   │   ├── SearchQuery.cs
│   │   ├── SearchResult.cs
│   │   └── SearchSource.cs
│   ├── Content/
│   │   ├── ExtractedContent.cs
│   │   ├── ContentChunk.cs
│   │   └── SourceDocument.cs
│   ├── Analysis/
│   │   ├── Finding.cs
│   │   ├── InformationGap.cs
│   │   └── SufficiencyScore.cs
│   └── Report/
│       ├── ReportSection.cs
│       ├── Citation.cs
│       └── ReportMetadata.cs
│
├── Search/
│   ├── SearchProviderFactory.cs
│   ├── Providers/
│   │   ├── TavilySearchProvider.cs
│   │   ├── SerperSearchProvider.cs
│   │   ├── BraveSearchProvider.cs
│   │   └── SemanticScholarProvider.cs
│   └── QueryExpansion/
│       ├── IQueryExpander.cs
│       ├── LLMQueryExpander.cs
│       └── PerspectiveQueryExpander.cs       # STORM 패턴
│
├── Content/
│   ├── WebFluxContentExtractor.cs
│   ├── ContentProcessor.cs
│   └── ContentChunker.cs
│
├── Orchestration/
│   ├── ResearchOrchestrator.cs               # 메인 오케스트레이터
│   ├── State/
│   │   ├── ResearchState.cs
│   │   ├── ResearchCheckpoint.cs
│   │   └── StateManager.cs
│   ├── Agents/
│   │   ├── QueryPlannerAgent.cs
│   │   ├── SearcherAgent.cs
│   │   ├── AnalyzerAgent.cs
│   │   ├── ReviewerAgent.cs
│   │   └── SynthesizerAgent.cs
│   └── Workflows/
│       ├── IterativeResearchWorkflow.cs
│       └── ParallelSearchWorkflow.cs
│
├── Evaluation/
│   ├── LLMSufficiencyEvaluator.cs            # LLM-as-Judge 패턴
│   ├── CoverageScorer.cs
│   ├── SourceDiversityScorer.cs
│   └── StoppingCriteria.cs
│
├── Report/
│   ├── ReportGenerator.cs
│   ├── CitationManager.cs
│   ├── OutlineBuilder.cs
│   └── Formatters/
│       ├── MarkdownFormatter.cs
│       ├── HtmlFormatter.cs
│       └── PdfFormatter.cs
│
└── DeepResearcher.cs                         # 파사드 구현
```

---

## 3. 핵심 인터페이스

### 3.1 IDeepResearcher (메인 파사드)

```csharp
namespace IronHive.Flux.DeepResearch.Abstractions;

/// <summary>
/// 딥리서치 실행을 위한 메인 인터페이스
/// </summary>
public interface IDeepResearcher
{
    /// <summary>
    /// 동기적 리서치 실행 (완료까지 대기)
    /// </summary>
    Task<ResearchResult> ResearchAsync(
        ResearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 스트리밍 리서치 실행 (진행 상황 실시간 전달)
    /// </summary>
    IAsyncEnumerable<ResearchProgress> ResearchStreamAsync(
        ResearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 대화형 리서치 세션 시작 (Human-in-the-Loop)
    /// </summary>
    Task<IResearchSession> StartInteractiveAsync(
        ResearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 체크포인트에서 리서치 재개
    /// </summary>
    Task<ResearchResult> ResumeAsync(
        string sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 대화형 리서치 세션
/// </summary>
public interface IResearchSession : IAsyncDisposable
{
    string SessionId { get; }
    ResearchState CurrentState { get; }
    bool IsComplete { get; }

    Task<ResearchCheckpoint> GetCheckpointAsync();
    Task ContinueAsync();
    Task AddQueryAsync(string customQuery);
    Task<ResearchResult> FinalizeAsync();
}
```

### 3.2 ISearchProvider

```csharp
namespace IronHive.Flux.DeepResearch.Abstractions;

/// <summary>
/// 검색 프로바이더 인터페이스
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    /// 프로바이더 식별자
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// 지원하는 검색 유형
    /// </summary>
    SearchCapabilities Capabilities { get; }

    /// <summary>
    /// 검색 실행
    /// </summary>
    Task<SearchResult> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 배치 검색 실행
    /// </summary>
    Task<IReadOnlyList<SearchResult>> SearchBatchAsync(
        IEnumerable<SearchQuery> queries,
        CancellationToken cancellationToken = default);
}

[Flags]
public enum SearchCapabilities
{
    None = 0,
    WebSearch = 1,
    NewsSearch = 2,
    AcademicSearch = 4,
    ImageSearch = 8,
    ContentExtraction = 16,    // Tavily의 경우 검색+추출 통합
    SemanticSearch = 32        // Exa 등 시맨틱 검색
}
```

### 3.3 IResearchOrchestrator

```csharp
namespace IronHive.Flux.DeepResearch.Abstractions;

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
```

### 3.4 ISufficiencyEvaluator

```csharp
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
```

---

## 4. 데이터 모델

### 4.1 Research Models

```csharp
namespace IronHive.Flux.DeepResearch.Models.Research;

/// <summary>
/// 리서치 요청
/// </summary>
public record ResearchRequest
{
    /// <summary>
    /// 사용자 질의
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// 리서치 깊이
    /// </summary>
    public ResearchDepth Depth { get; init; } = ResearchDepth.Standard;

    /// <summary>
    /// 출력 형식
    /// </summary>
    public OutputFormat OutputFormat { get; init; } = OutputFormat.Markdown;

    /// <summary>
    /// 출력 언어
    /// </summary>
    public string Language { get; init; } = "ko";

    /// <summary>
    /// 최대 반복 횟수
    /// </summary>
    public int MaxIterations { get; init; } = 5;

    /// <summary>
    /// 반복당 최대 소스 수
    /// </summary>
    public int MaxSourcesPerIteration { get; init; } = 10;

    /// <summary>
    /// 비용 한도 (USD)
    /// </summary>
    public decimal? MaxBudget { get; init; }

    /// <summary>
    /// 검색 프로바이더 우선순위
    /// </summary>
    public IReadOnlyList<string>? PreferredProviders { get; init; }

    /// <summary>
    /// 학술 검색 포함 여부
    /// </summary>
    public bool IncludeAcademic { get; init; } = false;

    /// <summary>
    /// 뉴스 검색 포함 여부
    /// </summary>
    public bool IncludeNews { get; init; } = false;
}

public enum ResearchDepth
{
    /// <summary>
    /// Quick: 1-2분, 3회 이내 반복
    /// </summary>
    Quick,

    /// <summary>
    /// Standard: 3-5분, 5회 이내 반복
    /// </summary>
    Standard,

    /// <summary>
    /// Comprehensive: 10-15분, 10회 이내 반복
    /// </summary>
    Comprehensive
}

public enum OutputFormat
{
    Markdown,
    Html,
    Pdf,
    Json
}

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

public record TokenUsage
{
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public int TotalTokens => InputTokens + OutputTokens;
}
```

### 4.2 Progress Model

```csharp
namespace IronHive.Flux.DeepResearch.Models.Research;

/// <summary>
/// 리서치 진행 상황 (스트리밍용)
/// </summary>
public record ResearchProgress
{
    public required ProgressType Type { get; init; }
    public required int CurrentIteration { get; init; }
    public required int MaxIterations { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    // Type별 데이터 (하나만 설정됨)
    public PlanProgress? Plan { get; init; }
    public SearchProgress? Search { get; init; }
    public ContentProgress? Content { get; init; }
    public AnalysisProgress? Analysis { get; init; }
    public string? ReportChunk { get; init; }
    public ResearchResult? Result { get; init; }
    public ResearchError? Error { get; init; }
}

public enum ProgressType
{
    Started,
    PlanGenerated,
    SearchStarted,
    SearchCompleted,
    ContentExtractionStarted,
    ContentExtracted,
    AnalysisStarted,
    AnalysisCompleted,
    SufficiencyEvaluated,
    IterationCompleted,
    ReportGenerationStarted,
    ReportSection,
    ReportChunk,        // 스트리밍 청크
    Completed,
    Failed,
    Checkpoint          // 체크포인트 저장됨
}

public record PlanProgress
{
    public required IReadOnlyList<string> GeneratedQueries { get; init; }
    public required IReadOnlyList<string> ResearchAngles { get; init; }
}

public record SearchProgress
{
    public required string Query { get; init; }
    public required string Provider { get; init; }
    public required int ResultCount { get; init; }
}

public record ContentProgress
{
    public required string Url { get; init; }
    public required int ContentLength { get; init; }
    public required bool Success { get; init; }
}

public record AnalysisProgress
{
    public required int FindingsCount { get; init; }
    public required int GapsIdentified { get; init; }
    public required SufficiencyScore Score { get; init; }
}
```

### 4.3 State Model

```csharp
namespace IronHive.Flux.DeepResearch.Orchestration.State;

/// <summary>
/// 리서치 상태 (체크포인트 가능)
/// </summary>
public class ResearchState
{
    public required string SessionId { get; init; }
    public required ResearchRequest Request { get; init; }
    public required DateTimeOffset StartedAt { get; init; }

    // 현재 진행 상태
    public ResearchPhase CurrentPhase { get; set; } = ResearchPhase.Planning;
    public int CurrentIteration { get; set; } = 0;

    // 수집된 데이터
    public List<SearchQuery> ExecutedQueries { get; } = [];
    public List<SearchResult> SearchResults { get; } = [];
    public List<SourceDocument> CollectedSources { get; } = [];
    public List<Finding> Findings { get; } = [];
    public List<InformationGap> IdentifiedGaps { get; } = [];

    // 분석 결과
    public SufficiencyScore? LastSufficiencyScore { get; set; }
    public List<string> ResearchAngles { get; } = [];

    // 보고서 생성 상태
    public ReportOutline? Outline { get; set; }
    public List<ReportSection> GeneratedSections { get; } = [];

    // 비용 추적
    public TokenUsage AccumulatedTokenUsage { get; set; } = new();
    public decimal AccumulatedCost { get; set; } = 0;

    // 에러 추적
    public List<ResearchError> Errors { get; } = [];
}

public enum ResearchPhase
{
    Planning,
    Searching,
    ContentExtraction,
    Analysis,
    SufficiencyEvaluation,
    ReportGeneration,
    Completed,
    Failed
}

/// <summary>
/// 체크포인트 (직렬화 가능)
/// </summary>
public record ResearchCheckpoint
{
    public required string SessionId { get; init; }
    public required int CheckpointNumber { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required ResearchState State { get; init; }

    // Human-in-the-Loop 지원
    public IReadOnlyList<Finding>? KeyFindings { get; init; }
    public IReadOnlyList<string>? SuggestedQueries { get; init; }
    public string? Summary { get; init; }
}
```

---

## 5. 오케스트레이션 설계

### 5.1 전체 워크플로우

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        ResearchOrchestrator                              │
│                                                                          │
│  ┌──────────────┐                                                        │
│  │ 1. Planning  │ ← QueryPlannerAgent                                   │
│  │  - 질의 분석  │   - 질문 분해 (Self-Ask)                               │
│  │  - 쿼리 생성  │   - 관점 발견 (STORM 패턴)                             │
│  └──────┬───────┘   - 쿼리 확장                                          │
│         │                                                                │
│         ▼                                                                │
│  ┌──────────────────────────────────────────────────────┐               │
│  │ 2. Iterative Research Loop (3-10 iterations)        │               │
│  │                                                      │               │
│  │  ┌───────────┐   ┌───────────┐   ┌───────────┐     │               │
│  │  │ Searching │──▶│ Extracting│──▶│ Analyzing │     │               │
│  │  │           │   │           │   │           │     │               │
│  │  │ - Tavily  │   │ - WebFlux │   │ - 정보 통합│     │               │
│  │  │ - Serper  │   │ - 청킹    │   │ - 갭 식별 │     │               │
│  │  │ - Brave   │   │           │   │           │     │               │
│  │  └───────────┘   └───────────┘   └─────┬─────┘     │               │
│  │                                        │            │               │
│  │                                        ▼            │               │
│  │                              ┌─────────────────┐   │               │
│  │                              │ 3. Evaluation   │   │               │
│  │                              │ (LLM-as-Judge)  │   │               │
│  │                              │                 │   │               │
│  │                              │ - 충분성 평가   │   │               │
│  │                              │ - 갭 식별       │   │               │
│  │                              │ - 종료 판단    │   │               │
│  │                              └────────┬────────┘   │               │
│  │                                       │            │               │
│  │                    ┌──────────────────┴───────┐    │               │
│  │                    │                          │    │               │
│  │              충분하지 않음              충분함      │               │
│  │                    │                          │    │               │
│  │                    ▼                          │    │               │
│  │           ┌───────────────┐                   │    │               │
│  │           │ 후속 쿼리 생성 │                   │    │               │
│  │           │ (Reflexion)   │────▶ Loop         │    │               │
│  │           └───────────────┘                   │    │               │
│  │                                               │    │               │
│  └───────────────────────────────────────────────┼────┘               │
│                                                  │                     │
│                                                  ▼                     │
│  ┌──────────────────────────────────────────────────────┐             │
│  │ 4. Report Generation                                  │             │
│  │                                                       │             │
│  │  ┌───────────┐   ┌───────────┐   ┌───────────┐      │             │
│  │  │ Outline   │──▶│ Section   │──▶│ Citation  │      │             │
│  │  │ Building  │   │ Writing   │   │ Injection │      │             │
│  │  └───────────┘   └───────────┘   └───────────┘      │             │
│  │                                                       │             │
│  │  ┌───────────┐   ┌───────────┐                       │             │
│  │  │ Fact      │──▶│ Final     │──▶ ResearchResult    │             │
│  │  │ Checking  │   │ Assembly  │                       │             │
│  │  └───────────┘   └───────────┘                       │             │
│  └───────────────────────────────────────────────────────┘             │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

### 5.2 ResearchOrchestrator 구현

```csharp
namespace IronHive.Flux.DeepResearch.Orchestration;

public class ResearchOrchestrator : IResearchOrchestrator
{
    private readonly ISearchProvider[] _searchProviders;
    private readonly IContentExtractor _contentExtractor;
    private readonly ISufficiencyEvaluator _sufficiencyEvaluator;
    private readonly IReportGenerator _reportGenerator;
    private readonly StateManager _stateManager;
    private readonly ILogger<ResearchOrchestrator> _logger;

    // 에이전트들
    private readonly QueryPlannerAgent _queryPlanner;
    private readonly SearcherAgent _searcher;
    private readonly AnalyzerAgent _analyzer;
    private readonly ReviewerAgent _reviewer;
    private readonly SynthesizerAgent _synthesizer;

    public async IAsyncEnumerable<ResearchProgress> ExecuteAsync(
        ResearchState state,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return CreateProgress(ProgressType.Started, state);

        try
        {
            // Phase 1: Planning
            state.CurrentPhase = ResearchPhase.Planning;
            var planResult = await _queryPlanner.PlanAsync(state, cancellationToken);
            state.ResearchAngles.AddRange(planResult.Angles);
            state.ExecutedQueries.AddRange(planResult.InitialQueries.Select(q => new SearchQuery { Query = q }));

            yield return CreateProgress(ProgressType.PlanGenerated, state, new PlanProgress
            {
                GeneratedQueries = planResult.InitialQueries,
                ResearchAngles = planResult.Angles
            });

            // Phase 2: Iterative Research Loop
            var maxIterations = GetMaxIterations(state.Request.Depth);
            var shouldContinue = true;

            while (shouldContinue &&
                   state.CurrentIteration < maxIterations &&
                   !IsBudgetExceeded(state))
            {
                state.CurrentIteration++;
                cancellationToken.ThrowIfCancellationRequested();

                // 2a. Search
                state.CurrentPhase = ResearchPhase.Searching;
                var pendingQueries = GetPendingQueries(state);

                await foreach (var searchProgress in ExecuteSearchesAsync(state, pendingQueries, cancellationToken))
                {
                    yield return searchProgress;
                }

                // 2b. Content Extraction
                state.CurrentPhase = ResearchPhase.ContentExtraction;
                var newUrls = GetNewUrlsToExtract(state);

                await foreach (var contentProgress in ExtractContentsAsync(state, newUrls, cancellationToken))
                {
                    yield return contentProgress;
                }

                // 2c. Analysis
                state.CurrentPhase = ResearchPhase.Analysis;
                var analysisResult = await _analyzer.AnalyzeAsync(state, cancellationToken);
                state.Findings.AddRange(analysisResult.NewFindings);

                // 2d. Sufficiency Evaluation (LLM-as-Judge)
                state.CurrentPhase = ResearchPhase.SufficiencyEvaluation;
                var sufficiencyScore = await _sufficiencyEvaluator.EvaluateAsync(state, cancellationToken);
                state.LastSufficiencyScore = sufficiencyScore;

                var gaps = await _sufficiencyEvaluator.IdentifyGapsAsync(state, cancellationToken);
                state.IdentifiedGaps.Clear();
                state.IdentifiedGaps.AddRange(gaps);

                yield return CreateProgress(ProgressType.AnalysisCompleted, state, analysisProgress: new AnalysisProgress
                {
                    FindingsCount = state.Findings.Count,
                    GapsIdentified = gaps.Count,
                    Score = sufficiencyScore
                });

                // 종료 조건 평가
                shouldContinue = EvaluateStoppingCriteria(state, sufficiencyScore, gaps);

                if (shouldContinue && gaps.Count > 0)
                {
                    // 후속 쿼리 생성 (Reflexion 패턴)
                    var followUpQueries = await _queryPlanner.GenerateFollowUpQueriesAsync(
                        state, gaps, cancellationToken);
                    state.ExecutedQueries.AddRange(followUpQueries.Select(q => new SearchQuery { Query = q }));
                }

                // 체크포인트 저장
                await SaveCheckpointAsync(state, cancellationToken);
                yield return CreateProgress(ProgressType.Checkpoint, state);

                yield return CreateProgress(ProgressType.IterationCompleted, state);
            }

            // Phase 3: Report Generation
            state.CurrentPhase = ResearchPhase.ReportGeneration;
            yield return CreateProgress(ProgressType.ReportGenerationStarted, state);

            await foreach (var reportChunk in GenerateReportAsync(state, cancellationToken))
            {
                yield return reportChunk;
            }

            // Complete
            state.CurrentPhase = ResearchPhase.Completed;
            var result = BuildFinalResult(state);
            yield return CreateProgress(ProgressType.Completed, state, result: result);
        }
        catch (Exception ex)
        {
            state.CurrentPhase = ResearchPhase.Failed;
            state.Errors.Add(new ResearchError
            {
                Type = ClassifyError(ex),
                Message = ex.Message,
                OccurredAt = DateTimeOffset.UtcNow
            });

            yield return CreateProgress(ProgressType.Failed, state, error: state.Errors.Last());

            // 부분 결과 반환 시도
            if (state.Findings.Count > 0)
            {
                var partialResult = BuildPartialResult(state);
                yield return CreateProgress(ProgressType.Completed, state, result: partialResult);
            }
        }
    }

    private bool EvaluateStoppingCriteria(
        ResearchState state,
        SufficiencyScore score,
        IReadOnlyList<InformationGap> gaps)
    {
        // 다중 신호 기반 종료 판단
        // 1. 충분성 점수 임계값 (0.8 이상이면 종료)
        if (score.OverallScore >= 0.8m)
            return false;

        // 2. 정보 이득 감소 (marginal gain)
        if (state.CurrentIteration > 1)
        {
            var previousFindings = state.Findings.Count - score.NewFindingsCount;
            var marginalGain = (decimal)score.NewFindingsCount / Math.Max(1, previousFindings);
            if (marginalGain < 0.1m)  // 10% 미만 증가
                return false;
        }

        // 3. 모든 핵심 갭이 해결됨
        if (gaps.All(g => g.Priority == GapPriority.Low))
            return false;

        // 4. 최소 소스 다양성 확보
        if (score.SourceDiversityScore >= 0.7m && state.CollectedSources.Count >= 5)
            return false;

        return true;  // 계속 진행
    }

    private int GetMaxIterations(ResearchDepth depth) => depth switch
    {
        ResearchDepth.Quick => 3,
        ResearchDepth.Standard => 5,
        ResearchDepth.Comprehensive => 10,
        _ => 5
    };
}
```

### 5.3 에이전트 설계

#### QueryPlannerAgent

```csharp
namespace IronHive.Flux.DeepResearch.Orchestration.Agents;

/// <summary>
/// 쿼리 계획 에이전트 - Self-Ask + STORM 패턴 적용
/// </summary>
public class QueryPlannerAgent
{
    private readonly IMessageGenerator _llm;
    private readonly PerspectiveQueryExpander _perspectiveExpander;

    public async Task<PlanResult> PlanAsync(
        ResearchState state,
        CancellationToken cancellationToken)
    {
        // 1. 질문 분해 (Self-Ask 패턴)
        var subQuestions = await DecomposeQueryAsync(state.Request.Query, cancellationToken);

        // 2. 관점 발견 (STORM 패턴)
        var perspectives = await _perspectiveExpander.DiscoverPerspectivesAsync(
            state.Request.Query, cancellationToken);

        // 3. 쿼리 확장
        var expandedQueries = await ExpandQueriesAsync(subQuestions, perspectives, cancellationToken);

        return new PlanResult
        {
            InitialQueries = expandedQueries,
            Angles = perspectives
        };
    }

    public async Task<IReadOnlyList<string>> GenerateFollowUpQueriesAsync(
        ResearchState state,
        IReadOnlyList<InformationGap> gaps,
        CancellationToken cancellationToken)
    {
        // Reflexion 패턴: 식별된 갭을 해결하기 위한 후속 쿼리 생성
        var prompt = BuildFollowUpPrompt(state, gaps);
        var response = await _llm.GenerateAsync(prompt, cancellationToken);
        return ParseQueries(response);
    }

    private async Task<IReadOnlyList<string>> DecomposeQueryAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"
당신은 리서치 계획 전문가입니다. 복합 질문을 단일 주제 쿼리로 분해합니다.

## 지침
1. 복합 질문을 독립적인 하위 질문으로 분해
2. 각 하위 질문은 단일 검색으로 답변 가능해야 함
3. 중복 제거
4. 우선순위 순서로 정렬

## 출력
JSON 배열 형식: [""쿼리1"", ""쿼리2"", ...]";

        var response = await _llm.GenerateAsync(new[]
        {
            new SystemMessage(systemPrompt),
            new UserMessage($"질문: {query}")
        }, cancellationToken);

        return JsonSerializer.Deserialize<string[]>(response.Content) ?? [];
    }
}
```

#### AnalyzerAgent

```csharp
namespace IronHive.Flux.DeepResearch.Orchestration.Agents;

/// <summary>
/// 분석 에이전트 - Chain-of-Verification 패턴 적용
/// </summary>
public class AnalyzerAgent
{
    private readonly IMessageGenerator _llm;

    public async Task<AnalysisResult> AnalyzeAsync(
        ResearchState state,
        CancellationToken cancellationToken)
    {
        var newSources = state.CollectedSources
            .Where(s => !state.Findings.Any(f => f.SourceId == s.Id))
            .ToList();

        var findings = new List<Finding>();

        foreach (var source in newSources)
        {
            // 1. 초기 분석 - 핵심 정보 추출
            var initialFindings = await ExtractFindingsAsync(source, state.Request.Query, cancellationToken);

            // 2. Chain-of-Verification - 각 주장 검증
            foreach (var finding in initialFindings)
            {
                var verifiedFinding = await VerifyFindingAsync(finding, source, cancellationToken);
                if (verifiedFinding.VerificationScore >= 0.7m)
                {
                    findings.Add(verifiedFinding);
                }
            }
        }

        // 3. 정보 통합 및 충돌 해결
        var consolidatedFindings = await ConsolidateFindingsAsync(
            state.Findings.Concat(findings).ToList(),
            cancellationToken);

        return new AnalysisResult
        {
            NewFindings = findings,
            ConsolidatedFindings = consolidatedFindings
        };
    }

    private async Task<Finding> VerifyFindingAsync(
        Finding finding,
        SourceDocument source,
        CancellationToken cancellationToken)
    {
        // Chain-of-Verification 패턴
        var verificationPrompt = $@"
다음 주장을 소스 텍스트와 대조하여 검증하세요.

주장: {finding.Claim}
소스: {source.Content}

검증 질문:
1. 이 주장이 소스에 명시적으로 언급되어 있는가?
2. 주장의 범위가 소스의 내용과 일치하는가?
3. 누락되거나 과장된 부분이 있는가?

JSON 형식으로 응답:
{{
    ""is_supported"": true/false,
    ""confidence"": 0.0-1.0,
    ""evidence_quote"": ""원문 인용"",
    ""notes"": ""검증 노트""
}}";

        var response = await _llm.GenerateAsync(verificationPrompt, cancellationToken);
        var verification = JsonSerializer.Deserialize<VerificationResult>(response.Content);

        return finding with
        {
            VerificationScore = verification?.Confidence ?? 0,
            EvidenceQuote = verification?.EvidenceQuote,
            IsVerified = verification?.IsSupported ?? false
        };
    }
}
```

---

## 6. 검색 프로바이더 설계

### 6.1 프로바이더 비교 및 선택 전략

| 프로바이더 | 강점 | 가격 (100K) | 권장 용도 |
|-----------|------|-------------|----------|
| [Tavily](https://docs.tavily.com/) | AI 최적화, 검색+추출 통합 | ~$800 | 기본 웹 검색 |
| [Serper](https://serper.dev/) | 최저가, Google 결과 | ~$50-100 | 비용 최적화 |
| [Brave](https://brave.com/search/api/) | 독립 인덱스, 프라이버시 | ~$300 | 프라이버시 중시 |
| [Semantic Scholar](https://www.semanticscholar.org/product/api) | 학술 검색, 무료 | 무료 | 학술 리서치 |

### 6.2 TavilySearchProvider 구현

```csharp
namespace IronHive.Flux.DeepResearch.Search.Providers;

public class TavilySearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly TavilyOptions _options;
    private readonly ILogger<TavilySearchProvider> _logger;

    private const string BaseUrl = "https://api.tavily.com";

    public string ProviderId => "tavily";
    public SearchCapabilities Capabilities =>
        SearchCapabilities.WebSearch |
        SearchCapabilities.NewsSearch |
        SearchCapabilities.ContentExtraction;

    public async Task<SearchResult> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var request = new TavilySearchRequest
        {
            Query = query.Query,
            SearchDepth = query.Depth == QueryDepth.Deep ? "advanced" : "basic",
            IncludeAnswer = true,
            IncludeRawContent = query.IncludeContent,
            MaxResults = query.MaxResults,
            IncludeDomains = query.IncludeDomains?.ToList(),
            ExcludeDomains = query.ExcludeDomains?.ToList()
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{BaseUrl}/search",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var tavilyResponse = await response.Content
            .ReadFromJsonAsync<TavilySearchResponse>(cancellationToken);

        return MapToSearchResult(tavilyResponse!, query);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchBatchAsync(
        IEnumerable<SearchQuery> queries,
        CancellationToken cancellationToken = default)
    {
        // 병렬 실행 (최대 5개 동시)
        var semaphore = new SemaphoreSlim(5);
        var tasks = queries.Select(async query =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await SearchAsync(query, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private SearchResult MapToSearchResult(TavilySearchResponse response, SearchQuery query)
    {
        return new SearchResult
        {
            Query = query,
            Provider = ProviderId,
            Answer = response.Answer,
            Sources = response.Results.Select(r => new SearchSource
            {
                Url = r.Url,
                Title = r.Title,
                Snippet = r.Content,
                RawContent = r.RawContent,
                Score = r.Score,
                PublishedDate = r.PublishedDate
            }).ToList(),
            SearchedAt = DateTimeOffset.UtcNow
        };
    }
}

// Tavily API 모델
internal record TavilySearchRequest
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("search_depth")]
    public string SearchDepth { get; init; } = "basic";

    [JsonPropertyName("include_answer")]
    public bool IncludeAnswer { get; init; } = true;

    [JsonPropertyName("include_raw_content")]
    public bool IncludeRawContent { get; init; } = false;

    [JsonPropertyName("max_results")]
    public int MaxResults { get; init; } = 10;

    [JsonPropertyName("include_domains")]
    public List<string>? IncludeDomains { get; init; }

    [JsonPropertyName("exclude_domains")]
    public List<string>? ExcludeDomains { get; init; }
}
```

### 6.3 SearchProviderFactory

```csharp
namespace IronHive.Flux.DeepResearch.Search;

public class SearchProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly DeepResearchOptions _options;

    public ISearchProvider GetProvider(string providerId)
    {
        return providerId.ToLowerInvariant() switch
        {
            "tavily" => _serviceProvider.GetRequiredService<TavilySearchProvider>(),
            "serper" => _serviceProvider.GetRequiredService<SerperSearchProvider>(),
            "brave" => _serviceProvider.GetRequiredService<BraveSearchProvider>(),
            "semantic-scholar" => _serviceProvider.GetRequiredService<SemanticScholarProvider>(),
            _ => throw new ArgumentException($"Unknown search provider: {providerId}")
        };
    }

    public ISearchProvider GetDefaultProvider()
    {
        return GetProvider(_options.DefaultSearchProvider);
    }

    public IReadOnlyList<ISearchProvider> GetProvidersForCapability(SearchCapabilities capability)
    {
        return _serviceProvider.GetServices<ISearchProvider>()
            .Where(p => p.Capabilities.HasFlag(capability))
            .ToList();
    }

    /// <summary>
    /// 비용 최적화된 프로바이더 선택
    /// </summary>
    public ISearchProvider GetCostOptimalProvider(SearchQuery query)
    {
        // 학술 검색은 무료 Semantic Scholar 우선
        if (query.Type == SearchType.Academic)
            return GetProvider("semantic-scholar");

        // 기본 검색은 Serper (가장 저렴)
        if (query.Depth == QueryDepth.Basic)
            return GetProvider("serper");

        // 심층 검색은 Tavily (콘텐츠 추출 통합)
        return GetProvider("tavily");
    }
}
```

---

## 7. 충분성 평가 설계 (LLM-as-Judge)

### 7.1 LLMSufficiencyEvaluator

```csharp
namespace IronHive.Flux.DeepResearch.Evaluation;

/// <summary>
/// LLM-as-Judge 패턴 기반 충분성 평가기
/// </summary>
public class LLMSufficiencyEvaluator : ISufficiencyEvaluator
{
    private readonly IMessageGenerator _llm;
    private readonly CoverageScorer _coverageScorer;
    private readonly SourceDiversityScorer _diversityScorer;

    public async Task<SufficiencyScore> EvaluateAsync(
        ResearchState state,
        CancellationToken cancellationToken = default)
    {
        // 1. 커버리지 점수 (하위 질문 답변율)
        var coverageScore = await _coverageScorer.ScoreAsync(state, cancellationToken);

        // 2. 소스 다양성 점수
        var diversityScore = _diversityScorer.Score(state.CollectedSources);

        // 3. LLM 기반 품질 판단 (G-Eval 패턴)
        var qualityScore = await EvaluateQualityWithLLMAsync(state, cancellationToken);

        // 4. 정보 신선도 점수
        var freshnessScore = CalculateFreshnessScore(state.CollectedSources);

        // 종합 점수 계산
        var overallScore =
            coverageScore * 0.35m +
            diversityScore * 0.20m +
            qualityScore * 0.30m +
            freshnessScore * 0.15m;

        return new SufficiencyScore
        {
            OverallScore = overallScore,
            CoverageScore = coverageScore,
            SourceDiversityScore = diversityScore,
            QualityScore = qualityScore,
            FreshnessScore = freshnessScore,
            NewFindingsCount = state.Findings.Count(f => f.IterationDiscovered == state.CurrentIteration),
            EvaluatedAt = DateTimeOffset.UtcNow
        };
    }

    private async Task<decimal> EvaluateQualityWithLLMAsync(
        ResearchState state,
        CancellationToken cancellationToken)
    {
        // G-Eval 패턴: Chain-of-Thought + 루브릭 기반 평가
        var evaluationPrompt = $@"
당신은 리서치 품질 평가 전문가입니다. 다음 기준에 따라 수집된 정보를 평가하세요.

## 원래 질문
{state.Request.Query}

## 수집된 핵심 발견사항
{string.Join("\n", state.Findings.Take(10).Select(f => $"- {f.Claim}"))}

## 평가 기준 (각 1-5점)
1. **정확성**: 발견사항이 신뢰할 수 있는 소스에 의해 뒷받침되는가?
2. **관련성**: 발견사항이 원래 질문에 직접적으로 답하는가?
3. **완전성**: 질문의 모든 측면이 다뤄졌는가?
4. **일관성**: 발견사항들 간에 논리적 일관성이 있는가?

## 평가 프로세스
각 기준에 대해:
1. 먼저 관련 증거를 인용하세요
2. 그 다음 점수를 부여하세요
3. 근거를 설명하세요

## 출력 형식 (JSON)
{{
    ""accuracy"": {{ ""score"": 1-5, ""reasoning"": ""..."" }},
    ""relevance"": {{ ""score"": 1-5, ""reasoning"": ""..."" }},
    ""completeness"": {{ ""score"": 1-5, ""reasoning"": ""..."" }},
    ""consistency"": {{ ""score"": 1-5, ""reasoning"": ""..."" }},
    ""overall_assessment"": ""...""
}}";

        var response = await _llm.GenerateAsync(evaluationPrompt, cancellationToken);
        var evaluation = JsonSerializer.Deserialize<QualityEvaluation>(response.Content);

        if (evaluation == null) return 0.5m;

        var avgScore = (
            evaluation.Accuracy.Score +
            evaluation.Relevance.Score +
            evaluation.Completeness.Score +
            evaluation.Consistency.Score
        ) / 4.0m / 5.0m;  // 0-1 범위로 정규화

        return avgScore;
    }

    public async Task<IReadOnlyList<InformationGap>> IdentifyGapsAsync(
        ResearchState state,
        CancellationToken cancellationToken = default)
    {
        var gapPrompt = $@"
당신은 정보 갭 분석 전문가입니다.

## 원래 질문
{state.Request.Query}

## 현재까지 수집된 정보 요약
{string.Join("\n", state.Findings.Take(15).Select(f => $"- {f.Claim}"))}

## 분석된 관점
{string.Join(", ", state.ResearchAngles)}

## 작업
아직 답변되지 않은 중요한 측면을 식별하세요.

## 출력 형식 (JSON 배열)
[
    {{
        ""gap"": ""누락된 정보 설명"",
        ""priority"": ""high/medium/low"",
        ""suggested_query"": ""이 갭을 채우기 위한 검색 쿼리""
    }}
]";

        var response = await _llm.GenerateAsync(gapPrompt, cancellationToken);
        var gaps = JsonSerializer.Deserialize<List<GapDto>>(response.Content) ?? [];

        return gaps.Select(g => new InformationGap
        {
            Description = g.Gap,
            Priority = Enum.Parse<GapPriority>(g.Priority, true),
            SuggestedQuery = g.SuggestedQuery,
            IdentifiedAt = DateTimeOffset.UtcNow
        }).ToList();
    }
}

public record SufficiencyScore
{
    public decimal OverallScore { get; init; }
    public decimal CoverageScore { get; init; }
    public decimal SourceDiversityScore { get; init; }
    public decimal QualityScore { get; init; }
    public decimal FreshnessScore { get; init; }
    public int NewFindingsCount { get; init; }
    public DateTimeOffset EvaluatedAt { get; init; }

    public bool IsSufficient => OverallScore >= 0.8m;
}
```

---

## 8. 보고서 생성 설계

### 8.1 ReportGenerator

```csharp
namespace IronHive.Flux.DeepResearch.Report;

public class ReportGenerator : IReportGenerator
{
    private readonly IMessageGenerator _llm;
    private readonly CitationManager _citationManager;
    private readonly OutlineBuilder _outlineBuilder;

    public async IAsyncEnumerable<ResearchProgress> GenerateAsync(
        ResearchState state,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. 아웃라인 생성
        var outline = await _outlineBuilder.BuildOutlineAsync(state, cancellationToken);
        state.Outline = outline;

        yield return new ResearchProgress
        {
            Type = ProgressType.ReportSection,
            CurrentIteration = state.CurrentIteration,
            MaxIterations = state.Request.MaxIterations,
            Timestamp = DateTimeOffset.UtcNow
        };

        // 2. 섹션별 작성 (병렬 가능)
        var sectionTasks = outline.Sections.Select(async section =>
        {
            return await GenerateSectionAsync(section, state, cancellationToken);
        });

        var sections = await Task.WhenAll(sectionTasks);

        foreach (var section in sections)
        {
            state.GeneratedSections.Add(section);

            // 스트리밍으로 섹션 전달
            yield return new ResearchProgress
            {
                Type = ProgressType.ReportSection,
                CurrentIteration = state.CurrentIteration,
                MaxIterations = state.Request.MaxIterations,
                Timestamp = DateTimeOffset.UtcNow,
                ReportChunk = section.Content
            };
        }

        // 3. 인용 주입 및 검증
        var sectionsWithCitations = await _citationManager.InjectCitationsAsync(
            state.GeneratedSections, state.CollectedSources, cancellationToken);

        // 4. 최종 조립
        var finalReport = AssembleReport(sectionsWithCitations, state);

        yield return new ResearchProgress
        {
            Type = ProgressType.ReportChunk,
            CurrentIteration = state.CurrentIteration,
            MaxIterations = state.Request.MaxIterations,
            Timestamp = DateTimeOffset.UtcNow,
            ReportChunk = finalReport
        };
    }

    private async Task<ReportSection> GenerateSectionAsync(
        OutlineSection outlineSection,
        ResearchState state,
        CancellationToken cancellationToken)
    {
        // 해당 섹션에 관련된 발견사항 필터링
        var relevantFindings = state.Findings
            .Where(f => IsRelevantToSection(f, outlineSection))
            .ToList();

        var sectionPrompt = $@"
다음 섹션을 작성하세요.

## 섹션 제목
{outlineSection.Title}

## 섹션 목적
{outlineSection.Purpose}

## 관련 발견사항
{string.Join("\n", relevantFindings.Select(f => $"- {f.Claim} [Source: {f.SourceId}]"))}

## 지침
1. 발견사항을 논리적으로 구성하세요
2. 모든 주장에 인용 마커 [1], [2] 등을 포함하세요
3. 객관적이고 분석적인 톤을 유지하세요
4. 언어: {state.Request.Language}

## 출력
마크다운 형식으로 섹션 내용만 출력하세요.";

        var response = await _llm.GenerateAsync(sectionPrompt, cancellationToken);

        return new ReportSection
        {
            Title = outlineSection.Title,
            Content = response.Content,
            Order = outlineSection.Order,
            RelatedFindings = relevantFindings.Select(f => f.Id).ToList()
        };
    }
}
```

### 8.2 CitationManager

```csharp
namespace IronHive.Flux.DeepResearch.Report;

public class CitationManager
{
    private readonly IMessageGenerator _llm;

    /// <summary>
    /// 인용 주입 및 검증
    /// </summary>
    public async Task<IReadOnlyList<ReportSection>> InjectCitationsAsync(
        IReadOnlyList<ReportSection> sections,
        IReadOnlyList<SourceDocument> sources,
        CancellationToken cancellationToken)
    {
        var citationMap = BuildCitationMap(sources);
        var result = new List<ReportSection>();

        foreach (var section in sections)
        {
            // 인용 마커를 실제 인용으로 변환
            var contentWithCitations = ResolveCitationMarkers(section.Content, citationMap);

            // 인용 검증 (소스에 실제로 해당 내용이 있는지)
            var verifiedContent = await VerifyCitationsAsync(
                contentWithCitations, sources, cancellationToken);

            result.Add(section with { Content = verifiedContent });
        }

        return result;
    }

    /// <summary>
    /// 인용 정확성 검증
    /// </summary>
    private async Task<string> VerifyCitationsAsync(
        string content,
        IReadOnlyList<SourceDocument> sources,
        CancellationToken cancellationToken)
    {
        // 인용된 주장 추출
        var citedClaims = ExtractCitedClaims(content);

        foreach (var claim in citedClaims)
        {
            var source = sources.FirstOrDefault(s => s.Id == claim.SourceId);
            if (source == null) continue;

            // 소스에서 해당 주장 지원 여부 확인
            var isSupported = await VerifyClaimInSourceAsync(
                claim.Text, source.Content, cancellationToken);

            if (!isSupported)
            {
                // 지원되지 않는 인용 마킹
                content = content.Replace(
                    claim.OriginalText,
                    $"{claim.Text} [citation needed]");
            }
        }

        return content;
    }

    public IReadOnlyList<Citation> ExtractCitations(
        IReadOnlyList<ReportSection> sections,
        IReadOnlyList<SourceDocument> sources)
    {
        var citations = new List<Citation>();
        var citationNumber = 1;

        foreach (var section in sections)
        {
            var matches = Regex.Matches(section.Content, @"\[(\d+)\]");
            foreach (Match match in matches)
            {
                var sourceIndex = int.Parse(match.Groups[1].Value) - 1;
                if (sourceIndex >= 0 && sourceIndex < sources.Count)
                {
                    var source = sources[sourceIndex];
                    citations.Add(new Citation
                    {
                        Number = citationNumber++,
                        SourceId = source.Id,
                        Url = source.Url,
                        Title = source.Title,
                        Author = source.Author,
                        PublishedDate = source.PublishedDate,
                        AccessedDate = source.ExtractedAt
                    });
                }
            }
        }

        return citations.DistinctBy(c => c.SourceId).ToList();
    }
}
```

---

## 9. 상태 관리 및 체크포인팅

### 9.1 StateManager

```csharp
namespace IronHive.Flux.DeepResearch.Orchestration.State;

/// <summary>
/// 리서치 상태 관리자 - 체크포인트 기반 지속성
/// </summary>
public class StateManager
{
    private readonly string _basePath;
    private readonly ILogger<StateManager> _logger;

    public StateManager(DeepResearchOptions options)
    {
        _basePath = options.CheckpointBasePath
            ?? Path.Combine(Path.GetTempPath(), "ironhive-flux", "deep-research");
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>
    /// 체크포인트 저장
    /// </summary>
    public async Task SaveCheckpointAsync(
        ResearchState state,
        CancellationToken cancellationToken = default)
    {
        var checkpoint = new ResearchCheckpoint
        {
            SessionId = state.SessionId,
            CheckpointNumber = state.CurrentIteration,
            CreatedAt = DateTimeOffset.UtcNow,
            State = state,
            KeyFindings = state.Findings.TakeLast(5).ToList(),
            SuggestedQueries = state.IdentifiedGaps
                .Where(g => g.Priority != GapPriority.Low)
                .Select(g => g.SuggestedQuery)
                .Take(3)
                .ToList()
        };

        var sessionDir = GetSessionDirectory(state.SessionId);
        Directory.CreateDirectory(sessionDir);

        var checkpointPath = Path.Combine(
            sessionDir,
            $"checkpoint-{checkpoint.CheckpointNumber:D3}.json");

        // 원자적 쓰기: 임시 파일 → 이동
        var tempPath = checkpointPath + ".tmp";
        var json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, checkpointPath, overwrite: true);

        // 상태 파일도 업데이트
        var statePath = Path.Combine(sessionDir, "state.json");
        await File.WriteAllTextAsync(
            statePath + ".tmp",
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        File.Move(statePath + ".tmp", statePath, overwrite: true);

        _logger.LogInformation(
            "Checkpoint saved: Session={SessionId}, Iteration={Iteration}",
            state.SessionId, checkpoint.CheckpointNumber);
    }

    /// <summary>
    /// 체크포인트에서 복원
    /// </summary>
    public async Task<ResearchCheckpoint?> LoadLatestCheckpointAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionDir = GetSessionDirectory(sessionId);
        if (!Directory.Exists(sessionDir))
            return null;

        var checkpointFiles = Directory.GetFiles(sessionDir, "checkpoint-*.json")
            .OrderByDescending(f => f)
            .ToList();

        if (checkpointFiles.Count == 0)
            return null;

        var latestPath = checkpointFiles.First();
        var json = await File.ReadAllTextAsync(latestPath, cancellationToken);
        return JsonSerializer.Deserialize<ResearchCheckpoint>(json);
    }

    /// <summary>
    /// 세션 디렉토리 구조
    /// </summary>
    /// <remarks>
    /// {basePath}/{sessionId}/
    /// ├── request.json              # 원본 요청
    /// ├── state.json                # 현재 상태
    /// ├── checkpoint-001.json
    /// ├── checkpoint-002.json
    /// ├── sources/
    /// │   ├── source-001.json
    /// │   └── source-002.json
    /// └── report.md                 # 최종 보고서
    /// </remarks>
    private string GetSessionDirectory(string sessionId)
        => Path.Combine(_basePath, sessionId);

    /// <summary>
    /// 오래된 세션 정리
    /// </summary>
    public void CleanupExpiredSessions(TimeSpan maxAge)
    {
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var dir in Directory.GetDirectories(_basePath))
        {
            if (Directory.GetLastWriteTimeUtc(dir) < cutoff)
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    _logger.LogInformation("Cleaned up expired session: {Directory}", dir);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup session: {Directory}", dir);
                }
            }
        }
    }
}
```

---

## 10. DI 등록 및 옵션

### 10.1 DeepResearchOptions

```csharp
namespace IronHive.Flux.DeepResearch.Options;

public class DeepResearchOptions
{
    /// <summary>
    /// 기본 검색 프로바이더
    /// </summary>
    public string DefaultSearchProvider { get; set; } = "tavily";

    /// <summary>
    /// 검색 API 키들
    /// </summary>
    public Dictionary<string, string> SearchApiKeys { get; set; } = new();

    /// <summary>
    /// 체크포인트 저장 경로
    /// </summary>
    public string? CheckpointBasePath { get; set; }

    /// <summary>
    /// 기본 최대 반복 횟수
    /// </summary>
    public int DefaultMaxIterations { get; set; } = 5;

    /// <summary>
    /// 반복당 기본 최대 소스 수
    /// </summary>
    public int DefaultMaxSourcesPerIteration { get; set; } = 10;

    /// <summary>
    /// 기본 비용 한도 (USD)
    /// </summary>
    public decimal DefaultMaxBudget { get; set; } = 1.0m;

    /// <summary>
    /// 충분성 임계값
    /// </summary>
    public decimal SufficiencyThreshold { get; set; } = 0.8m;

    /// <summary>
    /// 병렬 검색 최대 수
    /// </summary>
    public int MaxParallelSearches { get; set; } = 5;

    /// <summary>
    /// 병렬 콘텐츠 추출 최대 수
    /// </summary>
    public int MaxParallelExtractions { get; set; } = 10;

    /// <summary>
    /// 분석용 경량 모델 사용 여부
    /// </summary>
    public bool UseSmallModelForAnalysis { get; set; } = true;

    /// <summary>
    /// 분석용 모델 ID
    /// </summary>
    public string? AnalysisModelId { get; set; }

    /// <summary>
    /// 합성용 모델 ID
    /// </summary>
    public string? SynthesisModelId { get; set; }
}
```

### 10.2 ServiceCollectionExtensions

```csharp
namespace IronHive.Flux.DeepResearch.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIronHiveFluxDeepResearch(
        this IServiceCollection services,
        Action<DeepResearchOptions>? configureOptions = null)
    {
        // 옵션 등록
        var options = new DeepResearchOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // 검색 프로바이더 등록
        services.AddHttpClient<TavilySearchProvider>();
        services.AddHttpClient<SerperSearchProvider>();
        services.AddHttpClient<BraveSearchProvider>();
        services.AddHttpClient<SemanticScholarProvider>();

        services.AddSingleton<ISearchProvider, TavilySearchProvider>();
        services.AddSingleton<ISearchProvider, SerperSearchProvider>();
        services.AddSingleton<ISearchProvider, BraveSearchProvider>();
        services.AddSingleton<ISearchProvider, SemanticScholarProvider>();
        services.AddSingleton<SearchProviderFactory>();

        // 콘텐츠 추출기 등록
        services.AddSingleton<IContentExtractor, WebFluxContentExtractor>();
        services.AddSingleton<ContentProcessor>();
        services.AddSingleton<ContentChunker>();

        // 쿼리 확장기 등록
        services.AddSingleton<IQueryExpander, LLMQueryExpander>();
        services.AddSingleton<PerspectiveQueryExpander>();

        // 에이전트 등록
        services.AddSingleton<QueryPlannerAgent>();
        services.AddSingleton<SearcherAgent>();
        services.AddSingleton<AnalyzerAgent>();
        services.AddSingleton<ReviewerAgent>();
        services.AddSingleton<SynthesizerAgent>();

        // 평가기 등록
        services.AddSingleton<ISufficiencyEvaluator, LLMSufficiencyEvaluator>();
        services.AddSingleton<CoverageScorer>();
        services.AddSingleton<SourceDiversityScorer>();

        // 보고서 생성기 등록
        services.AddSingleton<IReportGenerator, ReportGenerator>();
        services.AddSingleton<CitationManager>();
        services.AddSingleton<OutlineBuilder>();

        // 상태 관리자 등록
        services.AddSingleton<StateManager>();

        // 오케스트레이터 등록
        services.AddSingleton<IResearchOrchestrator, ResearchOrchestrator>();

        // 메인 파사드 등록
        services.AddSingleton<IDeepResearcher, DeepResearcher>();

        return services;
    }
}
```

---

## 11. 사용 예시

### 11.1 기본 사용

```csharp
// DI 등록
services.AddIronHiveFluxCore(options =>
{
    options.TextCompletionModelId = "gpt-4o";
    options.EmbeddingModelId = "text-embedding-3-small";
});

services.AddIronHiveFluxDeepResearch(options =>
{
    options.DefaultSearchProvider = "tavily";
    options.SearchApiKeys["tavily"] = Environment.GetEnvironmentVariable("TAVILY_API_KEY")!;
    options.SearchApiKeys["serper"] = Environment.GetEnvironmentVariable("SERPER_API_KEY")!;
    options.DefaultMaxIterations = 5;
    options.SufficiencyThreshold = 0.8m;
});

// 사용
var researcher = serviceProvider.GetRequiredService<IDeepResearcher>();

var request = new ResearchRequest
{
    Query = "2026년 AI 에이전트 시장 동향과 주요 플레이어 분석",
    Depth = ResearchDepth.Standard,
    OutputFormat = OutputFormat.Markdown,
    Language = "ko",
    IncludeAcademic = true
};

var result = await researcher.ResearchAsync(request);

Console.WriteLine(result.Report);
Console.WriteLine($"Sources: {result.Sources.Count}");
Console.WriteLine($"Iterations: {result.Metadata.IterationCount}");
Console.WriteLine($"Cost: ${result.Metadata.EstimatedCost:F4}");
```

### 11.2 스트리밍 사용

```csharp
await foreach (var progress in researcher.ResearchStreamAsync(request))
{
    switch (progress.Type)
    {
        case ProgressType.PlanGenerated:
            Console.WriteLine($"[계획] {progress.Plan!.GeneratedQueries.Count}개 쿼리 생성");
            break;

        case ProgressType.SearchCompleted:
            Console.WriteLine($"[검색] {progress.Search!.Provider}: {progress.Search.ResultCount}개 결과");
            break;

        case ProgressType.AnalysisCompleted:
            Console.WriteLine($"[분석] 충분성: {progress.Analysis!.Score.OverallScore:P0}");
            break;

        case ProgressType.ReportChunk:
            Console.Write(progress.ReportChunk);
            break;

        case ProgressType.Completed:
            Console.WriteLine($"\n[완료] {progress.Result!.Sources.Count}개 소스 참조");
            break;
    }
}
```

---

## 12. 참고 자료

- [OpenAI Deep Research API](https://platform.openai.com/docs/guides/deep-research)
- [GPT Researcher LangGraph](https://docs.gptr.dev/docs/gpt-researcher/multi_agents/langgraph)
- [STORM (Stanford)](https://github.com/stanford-oval/storm)
- [Semantic Kernel Agent Orchestration](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-orchestration/)
- [LLM-as-Judge Survey](https://arxiv.org/html/2412.05579v2)
- [Deep Research Survey](https://arxiv.org/html/2508.12752v1)
- [Tavily API](https://docs.tavily.com/)
- [LangGraph Checkpointing](https://sparkco.ai/blog/mastering-langgraph-checkpointing-best-practices-for-2025)
