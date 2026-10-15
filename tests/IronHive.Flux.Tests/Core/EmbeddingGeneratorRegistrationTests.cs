using AwesomeAssertions;
using FileFlux;
using FluxCurator.Core.Core;
using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using IHiveEmbeddingGenerator = IronHive.Abstractions.Embedding.IEmbeddingGenerator;

namespace IronHive.Flux.Tests.Core;

/// <summary>
/// IronHive's generator is registered once as the container's <c>Microsoft.Extensions.AI</c> generator, and FileFlux,
/// FluxCurator and FluxIndex take it from there — no adapter class per library.
/// </summary>
public class EmbeddingGeneratorRegistrationTests
{
    private static IHiveEmbeddingGenerator ThreeDimensionGenerator()
    {
        var generator = Substitute.For<IHiveEmbeddingGenerator>();
        generator.EmbedBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => new EmbeddingResponse
            {
                Results = call.ArgAt<IEnumerable<string>>(1)
                    .Select((_, i) => new EmbeddingResult { Index = i, Embedding = [1f, 0f, 0f] })
                    .ToList(),
            });
        return generator;
    }

    [Fact]
    public void TheRegisteredGenerator_ReportsTheModelAndTheConfiguredDimension()
    {
        var services = new ServiceCollection();
        services.AddSingleton(ThreeDimensionGenerator());
        services.AddIronHiveFluxCore(o =>
        {
            o.EmbeddingModelId = "embed-model";
            o.EmbeddingDimension = 3;
        });
        services.AddIronHiveEmbeddingGenerator();
        using var provider = services.BuildServiceProvider();

        var metadata = provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
            .GetService<EmbeddingGeneratorMetadata>();

        metadata!.ProviderName.Should().Be("IronHive");
        metadata.DefaultModelId.Should().Be("embed-model");
        metadata.DefaultModelDimensions.Should().Be(3);
    }

    [Fact]
    public async Task FileFlux_SemanticEmbedder_EmbedsThroughTheIronHiveGenerator()
    {
        var generator = ThreeDimensionGenerator();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(generator);
        services.AddIronHiveFluxCore(o => o.EmbeddingDimension = 3);
        services.AddIronHiveFileFluxAdapters();
        services.AddFileFlux();
        using var provider = services.BuildServiceProvider();

        var embedder = provider.GetRequiredService<IEmbedder>();
        var vector = await embedder.GenerateEmbeddingAsync("a sentence", TestContext.Current.CancellationToken);

        vector.Should().HaveCount(3);
        generator.ReceivedCalls().Should().NotBeEmpty();
    }

    [Fact]
    public async Task WithoutADimension_FileFluxStillResolves_AndOnlyEmbeddingFails()
    {
        // A host that never chunks semantically must not lose FileFlux because the embedding dimension is unset.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(ThreeDimensionGenerator());
        services.AddIronHiveFluxCore();
        services.AddIronHiveFileFluxAdapters();
        services.AddFileFlux();
        using var provider = services.BuildServiceProvider();

        var embedder = provider.GetRequiredService<IEmbedder>();

        await embedder.Awaiting(e => e.GenerateEmbeddingAsync("a sentence", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*dimension*");
    }
}
