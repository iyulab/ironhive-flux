using IronHive.Flux.Core.Adapters.Embedding;
using IronHive.Flux.Core.Adapters.ImageToText;
using IronHive.Flux.Core.Adapters.TextCompletion;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IronHive.Flux.Core.Extensions;

/// <summary>
/// IronHive.Flux.Core 서비스 등록 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// IronHive.Flux.Core 어댑터 서비스를 등록합니다.
    /// </summary>
    /// <param name="services">서비스 컬렉션</param>
    /// <param name="configure">옵션 구성 액션</param>
    /// <returns>서비스 컬렉션</returns>
    public static IServiceCollection AddIronHiveFluxCore(
        this IServiceCollection services,
        Action<IronHiveFluxCoreOptions>? configure = null)
    {
        // 옵션 등록
        var options = new IronHiveFluxCoreOptions();
        configure?.Invoke(options);
        services.Configure<IronHiveFluxCoreOptions>(opt =>
        {
            opt.EmbeddingModelId = options.EmbeddingModelId;
            opt.TextCompletionModelId = options.TextCompletionModelId;
            opt.ImageToTextModelId = options.ImageToTextModelId;
            opt.EmbeddingDimension = options.EmbeddingDimension;
            opt.MaxTokens = options.MaxTokens;
            opt.DefaultTemperature = options.DefaultTemperature;
            opt.DefaultCompletionMaxTokens = options.DefaultCompletionMaxTokens;
        });

        return services;
    }

    /// <summary>
    /// FileFlux용 IronHive 어댑터를 등록합니다.
    /// </summary>
    public static IServiceCollection AddIronHiveFileFluxAdapters(this IServiceCollection services)
    {
        services.TryAddSingleton<FileFlux.IEmbeddingService, IronHiveEmbeddingServiceForFileFlux>();
        services.TryAddSingleton<FileFlux.ITextCompletionService, IronHiveTextCompletionServiceForFileFlux>();
        services.TryAddSingleton<FileFlux.IImageToTextService, IronHiveImageToTextServiceForFileFlux>();
        return services;
    }

    /// <summary>
    /// WebFlux용 IronHive 어댑터를 등록합니다.
    /// </summary>
    public static IServiceCollection AddIronHiveWebFluxAdapters(this IServiceCollection services)
    {
        services.TryAddSingleton<WebFlux.Core.Interfaces.ITextEmbeddingService, IronHiveEmbeddingServiceForWebFlux>();
        services.TryAddSingleton<WebFlux.Core.Interfaces.ITextCompletionService, IronHiveTextCompletionServiceForWebFlux>();
        services.TryAddSingleton<WebFlux.Core.Interfaces.IImageToTextService, IronHiveImageToTextServiceForWebFlux>();
        return services;
    }

    /// <summary>
    /// FluxIndex용 IronHive 어댑터를 등록합니다.
    /// </summary>
    public static IServiceCollection AddIronHiveFluxIndexAdapters(this IServiceCollection services)
    {
        services.TryAddSingleton<FluxIndex.Core.Application.Interfaces.IEmbeddingService, IronHiveEmbeddingServiceForFluxIndex>();
        services.TryAddSingleton<FluxIndex.Core.Application.Interfaces.ITextCompletionService, IronHiveTextCompletionServiceForFluxIndex>();
        return services;
    }

    /// <summary>
    /// 모든 Flux 어댑터를 한번에 등록합니다.
    /// </summary>
    public static IServiceCollection AddAllIronHiveFluxAdapters(this IServiceCollection services)
    {
        services.AddIronHiveFileFluxAdapters();
        services.AddIronHiveWebFluxAdapters();
        services.AddIronHiveFluxIndexAdapters();
        return services;
    }
}
