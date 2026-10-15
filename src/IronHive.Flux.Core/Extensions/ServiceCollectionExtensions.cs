using Flux.Abstractions;
using IronHive.Flux.Core.Adapters.ImageToText;
using IronHive.Flux.Core.Adapters.TextCompletion;
using FluxIndex.Core.Application.Services;
using IronHive.Extensions.AI;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.AI;
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
            opt.DefaultTemperature = options.DefaultTemperature;
            opt.DefaultCompletionMaxTokens = options.DefaultCompletionMaxTokens;
        });

        return services;
    }

    /// <summary>
    /// FileFlux용 IronHive 어댑터를 등록합니다.
    /// </summary>
    /// <remarks>
    /// Embeddings need no adapter: IronHive's generator is registered as the container's <c>Microsoft.Extensions.AI</c>
    /// generator (<see cref="AddIronHiveEmbeddingGenerator"/>), and FileFlux's semantic chunking reaches it through
    /// FluxCurator's <c>EmbeddingGeneratorEmbedder</c>, which FileFlux builds from that generator.
    /// </remarks>
    public static IServiceCollection AddIronHiveFileFluxAdapters(this IServiceCollection services)
    {
        services.AddIronHiveEmbeddingGenerator();
        services.TryAddSingleton<FileFlux.IDocumentAnalysisService, IronHiveTextCompletionServiceForFileFlux>();
        services.TryAddSingleton<FileFlux.IImageToTextService, IronHiveImageToTextServiceForFileFlux>();
        return services;
    }

    /// <summary>
    /// WebFlux용 IronHive 어댑터를 등록합니다.
    /// </summary>
    public static IServiceCollection AddIronHiveWebFluxAdapters(this IServiceCollection services)
    {
        // Semantic chunking runs on FluxCurator, which reads IEmbedder from the container (WebFlux 0.23.0 removed its own
        // interface): FluxCurator's embedder over the container's generator, sized from its metadata.
        services.AddIronHiveEmbeddingGenerator();
        services.TryAddSingleton<FluxCurator.Core.Core.IEmbedder>(sp =>
            new FluxCurator.Infrastructure.Embedding.EmbeddingGeneratorEmbedder(
                sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()));
        // One adapter for the shared Flux.Abstractions contract: WebFlux, FluxIndex and the rest resolve the same service.
        services.TryAddSingleton<ITextCompletionService, IronHiveTextCompletionService>();
        return services;
    }

    /// <summary>
    /// FluxIndex용 IronHive 어댑터를 등록합니다.
    /// </summary>
    public static IServiceCollection AddIronHiveFluxIndexAdapters(this IServiceCollection services)
    {
        services.AddIronHiveEmbeddingGenerator();
        services.TryAddSingleton(CreateFluxIndexEmbeddingService);
        // One adapter for the shared Flux.Abstractions contract: WebFlux, FluxIndex and the rest resolve the same service.
        services.TryAddSingleton<ITextCompletionService, IronHiveTextCompletionService>();
        return services;
    }

    /// <summary>
    /// Registers IronHive's embedding generator as the container's <c>Microsoft.Extensions.AI</c>
    /// <c>IEmbeddingGenerator&lt;string, Embedding&lt;float&gt;&gt;</c> (unless one is registered already), embedding with
    /// <see cref="IronHiveFluxCoreOptions.EmbeddingModelId"/> and reporting
    /// <see cref="IronHiveFluxCoreOptions.EmbeddingDimension"/> as the model's vector size when it is set. FileFlux,
    /// FluxCurator, WebFlux and FluxIndex all take that generator, so one registration serves every library.
    /// </summary>
    public static IServiceCollection AddIronHiveEmbeddingGenerator(this IServiceCollection services)
    {
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<IronHiveFluxCoreOptions>>().Value;
            var generator = sp.GetRequiredService<IronHive.Abstractions.Embedding.IEmbeddingGenerator>();
            // The provider name stays "IronHive", so a FluxIndex fingerprint (provider + model + dimension) matches what
            // the earlier adapters wrote.
            return options.EmbeddingDimension > 0
                ? generator.AsEmbeddingGenerator(options.EmbeddingModelId, options.EmbeddingDimension, providerName: "IronHive")
                : generator.AsEmbeddingGenerator(options.EmbeddingModelId, providerName: "IronHive");
        });
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

    // FluxIndex takes the container's Microsoft.Extensions.AI generator (AddIronHiveEmbeddingGenerator). It needs the
    // dimension to fingerprint and size its index; IronHive's generator does not know it, so the option must be set.
    private static FluxIndex.Core.Application.Interfaces.IEmbeddingService CreateFluxIndexEmbeddingService(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<IronHiveFluxCoreOptions>>().Value;
        if (options.EmbeddingDimension <= 0)
        {
            throw new InvalidOperationException(
                $"IronHiveFluxCoreOptions.EmbeddingDimension is {options.EmbeddingDimension}; FluxIndex needs the embedding " +
                $"model's dimension (model '{options.EmbeddingModelId}') to fingerprint and size its index.");
        }

        return new EmbeddingGeneratorEmbeddingService(sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    }
}
