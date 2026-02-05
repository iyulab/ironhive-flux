using IronHive.Abstractions.Tools;
using IronHive.Core.Tools;
using IronHive.Flux.Agent.Options;
using IronHive.Flux.Agent.Tools.FileFlux;
using IronHive.Flux.Agent.Tools.WebFlux;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IronHive.Flux.Agent.Extensions;

/// <summary>
/// IronHive.Flux.Agent 서비스 등록 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// IronHive.Flux.Agent 도구를 등록합니다.
    /// </summary>
    /// <param name="services">서비스 컬렉션</param>
    /// <param name="configure">옵션 구성 액션</param>
    /// <returns>서비스 컬렉션</returns>
    public static IServiceCollection AddFluxAgentTools(
        this IServiceCollection services,
        Action<FluxAgentToolsOptions>? configure = null)
    {
        // 옵션 등록
        var options = new FluxAgentToolsOptions();
        configure?.Invoke(options);
        services.Configure<FluxAgentToolsOptions>(opt =>
        {
            opt.EnableFileFluxTools = options.EnableFileFluxTools;
            opt.EnableWebFluxTools = options.EnableWebFluxTools;
            opt.DefaultChunkingStrategy = options.DefaultChunkingStrategy;
            opt.DefaultMaxChunkSize = options.DefaultMaxChunkSize;
            opt.DefaultChunkOverlap = options.DefaultChunkOverlap;
            opt.DefaultMaxCrawlDepth = options.DefaultMaxCrawlDepth;
            opt.DefaultExtractImages = options.DefaultExtractImages;
            opt.ToolTimeout = options.ToolTimeout;
        });

        // HttpClient 등록
        services.AddHttpClient();

        // 도구 클래스 등록
        if (options.EnableFileFluxTools)
        {
            services.TryAddSingleton<DocumentProcessorTool>();
            services.TryAddSingleton<DocumentAnalyzerTool>();
            services.TryAddSingleton<MetadataExtractorTool>();
        }

        if (options.EnableWebFluxTools)
        {
            services.TryAddSingleton<WebCrawlerTool>();
            services.TryAddSingleton<WebContentAnalyzerTool>();
            services.TryAddSingleton<WebChunkerTool>();
        }

        return services;
    }

    /// <summary>
    /// FileFlux 도구들을 ITool 컬렉션으로 가져옵니다.
    /// </summary>
    public static IEnumerable<ITool> GetFileFluxTools(this IServiceProvider provider)
    {
        var tools = new List<ITool>();

        var processorTool = provider.GetService<DocumentProcessorTool>();
        if (processorTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(processorTool.GetType()));

        var analyzerTool = provider.GetService<DocumentAnalyzerTool>();
        if (analyzerTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(analyzerTool.GetType()));

        var metadataTool = provider.GetService<MetadataExtractorTool>();
        if (metadataTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(metadataTool.GetType()));

        return tools;
    }

    /// <summary>
    /// WebFlux 도구들을 ITool 컬렉션으로 가져옵니다.
    /// </summary>
    public static IEnumerable<ITool> GetWebFluxTools(this IServiceProvider provider)
    {
        var tools = new List<ITool>();

        var crawlerTool = provider.GetService<WebCrawlerTool>();
        if (crawlerTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(crawlerTool.GetType()));

        var analyzerTool = provider.GetService<WebContentAnalyzerTool>();
        if (analyzerTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(analyzerTool.GetType()));

        var chunkerTool = provider.GetService<WebChunkerTool>();
        if (chunkerTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(chunkerTool.GetType()));

        return tools;
    }

    /// <summary>
    /// 모든 Flux Agent 도구들을 ITool 컬렉션으로 가져옵니다.
    /// </summary>
    public static IEnumerable<ITool> GetAllFluxAgentTools(this IServiceProvider provider)
    {
        return provider.GetFileFluxTools().Concat(provider.GetWebFluxTools());
    }
}
