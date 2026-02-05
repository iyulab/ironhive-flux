using IronHive.Abstractions.Tools;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IronHive.Flux.Rag.Extensions;

/// <summary>
/// IronHive.Flux.Rag 서비스 등록 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// IronHive.Flux.Rag 도구를 등록합니다.
    /// </summary>
    /// <param name="services">서비스 컬렉션</param>
    /// <param name="configure">옵션 구성 액션</param>
    /// <returns>서비스 컬렉션</returns>
    public static IServiceCollection AddFluxRagTools(
        this IServiceCollection services,
        Action<FluxRagToolsOptions>? configure = null)
    {
        // 옵션 등록
        var options = new FluxRagToolsOptions();
        configure?.Invoke(options);
        services.Configure<FluxRagToolsOptions>(opt =>
        {
            opt.DefaultMaxResults = options.DefaultMaxResults;
            opt.DefaultSearchStrategy = options.DefaultSearchStrategy;
            opt.DefaultMinScore = options.DefaultMinScore;
            opt.MaxContextTokens = options.MaxContextTokens;
            opt.ChunkSeparator = options.ChunkSeparator;
            opt.DefaultIndexName = options.DefaultIndexName;
            opt.ToolTimeout = options.ToolTimeout;
        });

        // 컨텍스트 빌더 등록
        services.TryAddSingleton<RagContextBuilder>();

        // 도구 클래스 등록
        services.TryAddSingleton<FluxIndexSearchTool>();
        services.TryAddSingleton<FluxIndexMemorizeTool>();
        services.TryAddSingleton<FluxIndexUnmemorizeTool>();

        return services;
    }

    /// <summary>
    /// RAG 컨텍스트 빌더를 등록합니다.
    /// </summary>
    public static IServiceCollection AddFluxRagContext(
        this IServiceCollection services,
        Action<FluxRagToolsOptions>? configure = null)
    {
        var options = new FluxRagToolsOptions();
        configure?.Invoke(options);
        services.Configure<FluxRagToolsOptions>(opt =>
        {
            opt.MaxContextTokens = options.MaxContextTokens;
            opt.ChunkSeparator = options.ChunkSeparator;
        });

        services.TryAddSingleton<RagContextBuilder>();
        return services;
    }

    /// <summary>
    /// FluxIndex RAG 도구들을 ITool 컬렉션으로 가져옵니다.
    /// </summary>
    public static IEnumerable<ITool> GetFluxRagTools(this IServiceProvider provider)
    {
        var tools = new List<ITool>();

        var searchTool = provider.GetService<FluxIndexSearchTool>();
        if (searchTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(searchTool.GetType()));

        var memorizeTool = provider.GetService<FluxIndexMemorizeTool>();
        if (memorizeTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(memorizeTool.GetType()));

        var unmemorizeTool = provider.GetService<FluxIndexUnmemorizeTool>();
        if (unmemorizeTool != null)
            tools.AddRange(FunctionToolFactory.CreateFrom(unmemorizeTool.GetType()));

        return tools;
    }
}
