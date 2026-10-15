using AwesomeAssertions;
using FluxIndex.Core.Application.Services;
using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Extensions;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace IronHive.Flux.Tests.Core;

/// <summary>
/// <c>AddIronHiveFluxIndexAdapters</c> gives FluxIndex its embedding service through FluxIndex's own
/// <c>Microsoft.Extensions.AI</c> intake over IronHive's generator — no adapter class of ours in between.
/// </summary>
public class FluxIndexEmbeddingRegistrationTests
{
    private static ServiceProvider Build(IEmbeddingGenerator generator, int dimension)
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<IronHiveFluxCoreOptions>(o =>
        {
            o.EmbeddingModelId = "text-embedding-3-small";
            o.EmbeddingDimension = dimension;
        });
        services.AddSingleton(generator);
        services.AddIronHiveFluxIndexAdapters();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TheRegisteredService_EmbedsThroughIronHive_WithTheConfiguredModel()
    {
        var generator = Substitute.For<IEmbeddingGenerator>();
        generator
            .EmbedBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new EmbeddingResponse { Model = "text-embedding-3-small", Results = [new() { Embedding = new float[4], Index = 0 }] });
        using var provider = Build(generator, dimension: 4);

        var service = provider.GetRequiredService<FluxIndex.Core.Application.Interfaces.IEmbeddingService>();
        var vector = await service.GenerateEmbeddingAsync("hello", TestContext.Current.CancellationToken);

        service.Should().BeOfType<EmbeddingGeneratorEmbeddingService>();
        vector.Should().HaveCount(4);
        await generator.Received(1).EmbedBatchAsync(
            "text-embedding-3-small", Arg.Is<IEnumerable<string>>(t => t.Single() == "hello"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheIdentity_KeepsTheProviderNameTheEarlierAdapterWrote()
    {
        using var provider = Build(Substitute.For<IEmbeddingGenerator>(), dimension: 1536);

        var identity = provider.GetRequiredService<FluxIndex.Core.Application.Interfaces.IEmbeddingService>().GetIdentity();

        identity.Provider.Should().Be("IronHive");
        identity.Model.Should().Be("text-embedding-3-small");
        identity.Dimension.Should().Be(1536);
    }

    [Fact]
    public void AnUnsetDimension_FailsAtResolve_NamingTheOption()
    {
        using var provider = Build(Substitute.For<IEmbeddingGenerator>(), dimension: 0);

        var act = () => provider.GetRequiredService<FluxIndex.Core.Application.Interfaces.IEmbeddingService>();

        act.Should().Throw<InvalidOperationException>().WithMessage("*IronHiveFluxCoreOptions.EmbeddingDimension*");
    }
}
