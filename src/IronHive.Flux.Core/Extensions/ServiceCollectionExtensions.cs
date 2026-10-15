using Flux.Abstractions;
using IronHive.Flux.Core.Adapters.Embedding;
using IronHive.Flux.Core.Adapters.ImageToText;
using IronHive.Flux.Core.Adapters.TextCompletion;
using FluxIndex.Core.Application.Services;
using IronHive.Extensions.AI;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        // FileFlux's semantic chunking also runs on FluxCurator and reads IEmbedder from the container.
        services.TryAddSingleton<FluxCurator.Core.Core.IEmbedder, IronHiveEmbedderForFluxCurator>();
        services.TryAddSingleton<FileFlux.IDocumentAnalysisService, IronHiveTextCompletionServiceForFileFlux>();
        services.TryAddSingleton<FileFlux.IImageToTextService, IronHiveImageToTextServiceForFileFlux>();
        return services;
    }

    /// <summary>
    /// WebFlux용 IronHive 어댑터를 등록합니다.
    /// </summary>
    public static IServiceCollection AddIronHiveWebFluxAdapters(this IServiceCollection services)
    {
        // Semantic chunking runs on FluxCurator, which reads IEmbedder from the container (WebFlux 0.23.0 removed its own interface).
        services.TryAddSingleton<FluxCurator.Core.Core.IEmbedder, IronHiveEmbedderForFluxCurator>();
        // One adapter for the shared Flux.Abstractions contract: WebFlux, FluxIndex and the rest resolve the same service.
        services.TryAddSingleton<ITextCompletionService, IronHiveTextCompletionService>();
        return services;
    }

    /// <summary>
    /// FluxIndex용 IronHive 어댑터를 등록합니다.
    /// </summary>
    public static IServiceCollection AddIronHiveFluxIndexAdapters(this IServiceCollection services)
    {
        services.TryAddSingleton(CreateFluxIndexEmbeddingService);
        // One adapter for the shared Flux.Abstractions contract: WebFlux, FluxIndex and the rest resolve the same service.
        services.TryAddSingleton<ITextCompletionService, IronHiveTextCompletionService>();
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

    // FluxIndex takes a Microsoft.Extensions.AI generator; IronHive.Extensions.AI bridges IronHive's. The provider name
    // stays "IronHive", so the index fingerprint (provider + model + dimension) matches what the earlier adapter wrote.
    private static FluxIndex.Core.Application.Interfaces.IEmbeddingService CreateFluxIndexEmbeddingService(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<IronHiveFluxCoreOptions>>().Value;
        if (options.EmbeddingDimension <= 0)
        {
            throw new InvalidOperationException(
                $"IronHiveFluxCoreOptions.EmbeddingDimension is {options.EmbeddingDimension}; FluxIndex needs the embedding " +
                $"model's dimension (model '{options.EmbeddingModelId}') to fingerprint and size its index.");
        }

        var generator = sp.GetRequiredService<IronHive.Abstractions.Embedding.IEmbeddingGenerator>()
            .AsEmbeddingGenerator(options.EmbeddingModelId, providerName: "IronHive");
        return new EmbeddingGeneratorEmbeddingService(
            generator, options: new EmbeddingGeneratorServiceOptions { Dimensions = options.EmbeddingDimension });
    }
}
