using AwesomeAssertions;
using IronHive.Abstractions.Embedding;
using IronHive.Flux.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WebFlux.Core.Interfaces;
using WebFlux.Core.Models;
using WebFlux.Extensions;
using Xunit;

namespace IronHive.Flux.Tests.Core;

/// <summary>
/// The IronHive embedder registered for WebFlux is the one WebFlux's semantic chunking calls. Before, the adapter implemented a
/// WebFlux interface nothing read, and semantic chunking failed for want of an embedder.
/// </summary>
public class SemanticChunkingEmbedderWiringTests
{
    private static readonly float[] Finance = [0f, 1f, 0f];
    private static readonly float[] Animals = [1f, 0f, 0f];

    [Fact]
    public async Task WebFlux_semantic_chunking_embeds_through_the_IronHive_generator()
    {
        var generator = Substitute.For<IEmbeddingGenerator>();
        generator.EmbedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(1).Contains("revenue", StringComparison.OrdinalIgnoreCase)
                ? Finance
                : Animals);
        generator.EmbedBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(call => new EmbeddingResponse
            {
                Results = call.ArgAt<IEnumerable<string>>(1)
                    .Select((t, i) => new EmbeddingResult
                    {
                        Index = i,
                        Embedding = t.Contains("revenue", StringComparison.OrdinalIgnoreCase) ? Finance : Animals,
                    })
                    .ToList(),
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(generator);
        services.AddIronHiveFluxCore(o => o.EmbeddingDimension = 3);
        services.AddIronHiveWebFluxAdapters();
        services.AddWebFlux();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var semantic = scope.ServiceProvider.GetRequiredKeyedService<IChunkingStrategy>("Semantic");
        var chunks = await semantic.ChunkAsync(
            new ExtractedContent
            {
                Url = "https://example.com",
                MainContent = "Cats purr and sleep. Kittens are young cats.\n\nQuarterly revenue grew. The revenue outlook is strong.",
            },
            cancellationToken: TestContext.Current.CancellationToken);

        chunks.Should().NotBeEmpty();
        generator.ReceivedCalls().Should().NotBeEmpty("WebFlux's semantic chunking embeds through the registered IronHive generator");
    }
}
