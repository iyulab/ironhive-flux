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
    /// IVault가 DI 컨테이너에 미리 등록되어 있어야 합니다.
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
            opt.ToolTimeout = options.ToolTimeout;
        });

        // VaultSecurityOptions (defaults if not configured externally)
        services.TryAddSingleton(Microsoft.Extensions.Options.Options.Create(new VaultSecurityOptions()));

        // 컨텍스트 빌더 등록
        services.TryAddSingleton<RagContextBuilder>();

        // 도구 클래스 등록 (IVault는 Scoped이므로 도구도 Scoped)
        services.TryAddScoped<FluxIndexSearchTool>();
        services.TryAddScoped<FluxIndexMemorizeTool>();
        services.TryAddScoped<FluxIndexUnmemorizeTool>();
        services.TryAddScoped<FluxIndexBatchMemorizeTool>();
        services.TryAddScoped<FluxIndexWebMemorizeTool>();
        services.TryAddScoped<FluxIndexStatusTool>();

        return services;
    }

    /// <summary>
    /// Vault 보안 옵션을 구성합니다.
    /// </summary>
    /// <param name="services">서비스 컬렉션</param>
    /// <param name="configure">보안 옵션 구성 액션</param>
    /// <returns>서비스 컬렉션</returns>
    public static IServiceCollection ConfigureVaultSecurity(
        this IServiceCollection services,
        Action<VaultSecurityOptions> configure)
    {
        services.Configure(configure);
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
    /// <remarks>
    /// 각 도구는 호출될 때마다 <paramref name="provider"/> 에서 해석된다(도구 클래스는 Scoped — 루트에서 미리 만들지 않는다).
    /// <c>memorize_web_page</c> 는 WebFlux 로 추출하므로 호스트가 WebFlux 를 등록했을 때만(<c>services.AddWebFlux()</c>) 포함된다.
    /// 0.8.0 이전에는 각 도구의 <see cref="Type"/> 객체를 인스턴스 자리에 넘겨, 이 메서드가 어떤 도구도 돌려주지 않았다.
    /// </remarks>
    public static IEnumerable<ITool> GetFluxRagTools(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var isService = provider.GetService<IServiceProviderIsService>();
        bool Registered(Type type) => isService?.IsService(type) ?? provider.GetService(type) is not null;

        var tools = new List<ITool>();
        if (Registered(typeof(FluxIndexSearchTool)))
            tools.AddRange(FunctionToolFactory.CreateFrom<FluxIndexSearchTool>(provider));
        if (Registered(typeof(FluxIndexMemorizeTool)))
            tools.AddRange(FunctionToolFactory.CreateFrom<FluxIndexMemorizeTool>(provider));
        if (Registered(typeof(FluxIndexUnmemorizeTool)))
            tools.AddRange(FunctionToolFactory.CreateFrom<FluxIndexUnmemorizeTool>(provider));
        if (Registered(typeof(FluxIndexBatchMemorizeTool)))
            tools.AddRange(FunctionToolFactory.CreateFrom<FluxIndexBatchMemorizeTool>(provider));
        if (Registered(typeof(FluxIndexWebMemorizeTool)) && Registered(typeof(WebFlux.Core.Interfaces.IContentExtractService)))
            tools.AddRange(FunctionToolFactory.CreateFrom<FluxIndexWebMemorizeTool>(provider));
        if (Registered(typeof(FluxIndexStatusTool)))
            tools.AddRange(FunctionToolFactory.CreateFrom<FluxIndexStatusTool>(provider));

        return tools;
    }
}
