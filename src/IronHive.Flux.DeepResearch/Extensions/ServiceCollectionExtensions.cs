using IronHive.Flux.DeepResearch.Abstractions;
using IronHive.Flux.DeepResearch.Options;
using IronHive.Flux.DeepResearch.Search;
using IronHive.Flux.DeepResearch.Search.Caching;
using IronHive.Flux.DeepResearch.Search.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

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

        // 검색 결과 캐시 등록
        services.AddSingleton<ISearchResultCache, MemorySearchResultCache>();

        // 검색 프로바이더 HTTP 클라이언트 등록 (Resilience 포함)
        AddSearchProviderHttpClient<TavilySearchProvider>(services, options, "tavily");

        // 검색 프로바이더 등록
        services.AddSingleton<ISearchProvider, TavilySearchProvider>();

        // TODO: Phase 2에서 추가 프로바이더 등록
        // services.AddSingleton<ISearchProvider, SerperSearchProvider>();
        // services.AddSingleton<ISearchProvider, BraveSearchProvider>();
        // services.AddSingleton<ISearchProvider, SemanticScholarProvider>();

        // 검색 프로바이더 팩토리 등록
        services.AddSingleton<SearchProviderFactory>();

        // TODO: Phase 2+ 구현체 등록
        // 콘텐츠 추출기 등록
        // services.AddSingleton<IContentExtractor, WebFluxContentExtractor>();

        // 평가기 등록
        // services.AddSingleton<ISufficiencyEvaluator, LLMSufficiencyEvaluator>();

        // 보고서 생성기 등록
        // services.AddSingleton<IReportGenerator, ReportGenerator>();

        // 상태 관리자 등록
        // services.AddSingleton<StateManager>();

        // 오케스트레이터 등록
        // services.AddSingleton<IResearchOrchestrator, ResearchOrchestrator>();

        // 메인 파사드 등록
        // services.AddSingleton<IDeepResearcher, DeepResearcher>();

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

        AddSearchProviderHttpClient<TavilySearchProvider>(services, options, "tavily");
        services.AddSingleton<ISearchProvider, TavilySearchProvider>();

        return services;
    }

    private static void AddSearchProviderHttpClient<TProvider>(
        IServiceCollection services,
        DeepResearchOptions options,
        string providerName)
        where TProvider : class
    {
        services.AddHttpClient<TProvider>(client =>
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
