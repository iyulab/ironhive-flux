using IronHive.Flux.DeepResearch.Abstractions;
using IronHive.Flux.DeepResearch.Content;
using IronHive.Flux.DeepResearch.Options;
using IronHive.Flux.DeepResearch.Orchestration;
using IronHive.Flux.DeepResearch.Orchestration.Agents;
using IronHive.Flux.DeepResearch.Search;
using IronHive.Flux.DeepResearch.Search.Caching;
using IronHive.Flux.DeepResearch.Search.Providers;
using IronHive.Flux.DeepResearch.Search.QueryExpansion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using WebFlux.Extensions;

namespace IronHive.Flux.DeepResearch.Extensions;

/// <summary>
/// DI 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// IronHive.Flux.DeepResearch 서비스 등록
    /// </summary>
    public static IServiceCollection AddIronHiveFluxDeepResearch(
        this IServiceCollection services,
        Action<DeepResearchOptions>? configureOptions = null)
    {
        // 옵션 등록
        var options = new DeepResearchOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        // 메모리 캐시 추가
        services.AddMemoryCache();

        // === Phase 1: 검색 인프라 ===

        // 검색 결과 캐시 등록
        services.AddSingleton<ISearchResultCache, MemorySearchResultCache>();

        // 검색 프로바이더 HTTP 클라이언트 등록 (Resilience 포함)
        AddResilientHttpClient<TavilySearchProvider>(services, options);
        AddResilientHttpClient<DuckDuckGoSearchProvider>(services, options);

        // 검색 프로바이더 등록
        services.AddSingleton<ISearchProvider, TavilySearchProvider>();
        services.AddSingleton<ISearchProvider, DuckDuckGoSearchProvider>();

        // TODO: 추가 프로바이더 (Phase 7)
        // services.AddSingleton<ISearchProvider, SerperSearchProvider>();
        // services.AddSingleton<ISearchProvider, BraveSearchProvider>();
        // services.AddSingleton<ISearchProvider, SemanticScholarProvider>();

        // 검색 프로바이더 팩토리 등록
        services.AddSingleton<SearchProviderFactory>();

        // === Phase 2: 콘텐츠 추출 ===

        // 청커 등록 (항상 필요)
        services.AddSingleton<ContentChunker>();

        if (options.UseWebFluxPackage)
        {
            // WebFlux 패키지 사용: 고급 크롤링 및 콘텐츠 추출
            services.AddWebFlux();
            services.AddSingleton<IContentExtractor, WebFluxIntegratedContentExtractor>();
        }
        else
        {
            // 기본 경량 콘텐츠 추출기 사용
            services.AddSingleton<ContentProcessor>();
            AddResilientHttpClient<WebFluxContentExtractor>(services, options);
            services.AddSingleton<IContentExtractor, WebFluxContentExtractor>();
        }

        // === Phase 3: 쿼리 계획 ===

        // 쿼리 확장기 등록 (ITextGenerationService 필요)
        services.AddSingleton<IQueryExpander, LLMQueryExpander>();

        // 쿼리 계획 에이전트 등록
        services.AddSingleton<QueryPlannerAgent>();

        // === Phase 4: 검색 실행 ===

        // 검색 조율 에이전트 등록
        services.AddSingleton<SearchCoordinatorAgent>();

        // === Phase 5: 콘텐츠 강화 ===

        // 콘텐츠 강화 에이전트 등록
        services.AddSingleton<ContentEnrichmentAgent>();

        // === Phase 6: 분석 및 충분성 평가 ===

        // 분석 에이전트 등록
        services.AddSingleton<AnalysisAgent>();

        // === Phase 7: 보고서 생성 ===

        // 보고서 생성 에이전트 등록
        services.AddSingleton<ReportGeneratorAgent>();

        // === Phase 8: 오케스트레이터 및 통합 ===

        // 리서치 오케스트레이터 등록
        services.AddSingleton<ResearchOrchestrator>();

        // 메인 파사드 등록
        services.AddSingleton<IDeepResearcher, DeepResearcher>();

        return services;
    }

    /// <summary>
    /// Tavily 검색 프로바이더만 등록 (단독 사용 시)
    /// </summary>
    public static IServiceCollection AddTavilySearchProvider(
        this IServiceCollection services,
        Action<DeepResearchOptions>? configureOptions = null)
    {
        var options = new DeepResearchOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        services.AddMemoryCache();
        services.AddSingleton<ISearchResultCache, MemorySearchResultCache>();

        AddResilientHttpClient<TavilySearchProvider>(services, options);
        services.AddSingleton<ISearchProvider, TavilySearchProvider>();

        return services;
    }

    /// <summary>
    /// 콘텐츠 추출기만 등록 (단독 사용 시)
    /// </summary>
    public static IServiceCollection AddContentExtractor(
        this IServiceCollection services,
        Action<DeepResearchOptions>? configureOptions = null)
    {
        var options = new DeepResearchOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<ContentChunker>();

        if (options.UseWebFluxPackage)
        {
            services.AddWebFlux();
            services.AddSingleton<IContentExtractor, WebFluxIntegratedContentExtractor>();
        }
        else
        {
            services.AddSingleton<ContentProcessor>();
            AddResilientHttpClient<WebFluxContentExtractor>(services, options);
            services.AddSingleton<IContentExtractor, WebFluxContentExtractor>();
        }

        return services;
    }

    /// <summary>
    /// 쿼리 계획 에이전트만 등록 (단독 사용 시)
    /// ITextGenerationService 구현체가 별도로 등록되어 있어야 함
    /// </summary>
    public static IServiceCollection AddQueryPlanner(
        this IServiceCollection services,
        Action<DeepResearchOptions>? configureOptions = null)
    {
        var options = new DeepResearchOptions();
        configureOptions?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<IQueryExpander, LLMQueryExpander>();
        services.AddSingleton<QueryPlannerAgent>();

        return services;
    }

    private static void AddResilientHttpClient<TClient>(
        IServiceCollection services,
        DeepResearchOptions options)
        where TClient : class
    {
        services.AddHttpClient<TClient>(client =>
            {
                client.Timeout = options.HttpTimeout;
            })
            .AddStandardResilienceHandler(resilienceOptions =>
            {
                // 재시도 정책 설정
                resilienceOptions.Retry.MaxRetryAttempts = options.MaxRetries;
                resilienceOptions.Retry.Delay = TimeSpan.FromSeconds(1);
                resilienceOptions.Retry.UseJitter = true;
                resilienceOptions.Retry.BackoffType = DelayBackoffType.Exponential;

                // 서킷 브레이커 설정
                resilienceOptions.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
                resilienceOptions.CircuitBreaker.FailureRatio = 0.5;
                resilienceOptions.CircuitBreaker.MinimumThroughput = 5;
                resilienceOptions.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

                // 타임아웃 설정
                resilienceOptions.TotalRequestTimeout.Timeout = options.HttpTimeout * 2;
            });
    }
}
