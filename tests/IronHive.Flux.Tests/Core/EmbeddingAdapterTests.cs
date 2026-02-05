using FluentAssertions;
using IronHive.Abstractions.Embedding;
using Xunit;
using IronHive.Flux.Core.Adapters.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Options;
using Moq;

namespace IronHive.Flux.Tests.Core;

public class EmbeddingAdapterTests
{
    private readonly Mock<IEmbeddingGenerator> _mockGenerator;
    private readonly IOptions<IronHiveFluxCoreOptions> _options;

    public EmbeddingAdapterTests()
    {
        _mockGenerator = new Mock<IEmbeddingGenerator>();
        _options = Options.Create(new IronHiveFluxCoreOptions
        {
            EmbeddingModelId = "text-embedding-3-small",
            EmbeddingDimension = 1536,
            MaxTokens = 8191
        });
    }

    [Fact]
    public async Task FileFluxAdapter_GenerateEmbeddingAsync_ReturnsEmbedding()
    {
        // Arrange
        var expectedEmbedding = new float[1536];
        Random.Shared.NextBytes(new Span<byte>(new byte[1536 * 4]));

        _mockGenerator
            .Setup(g => g.EmbedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator.Object, _options);

        // Act
        var result = await adapter.GenerateEmbeddingAsync("test text");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(1536);
    }

    [Fact]
    public void FileFluxAdapter_CalculateSimilarity_ReturnsValidScore()
    {
        // Arrange
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator.Object, _options);
        var embedding1 = new float[] { 1, 0, 0 };
        var embedding2 = new float[] { 1, 0, 0 };

        // Act
        var similarity = adapter.CalculateSimilarity(embedding1, embedding2);

        // Assert
        similarity.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void FileFluxAdapter_CalculateSimilarity_OrthogonalVectors_ReturnsZero()
    {
        // Arrange
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator.Object, _options);
        var embedding1 = new float[] { 1, 0, 0 };
        var embedding2 = new float[] { 0, 1, 0 };

        // Act
        var similarity = adapter.CalculateSimilarity(embedding1, embedding2);

        // Assert
        similarity.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public async Task WebFluxAdapter_GetEmbeddingAsync_ReturnsEmbedding()
    {
        // Arrange
        var expectedEmbedding = new float[1536];
        _mockGenerator
            .Setup(g => g.EmbedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        var adapter = new IronHiveEmbeddingServiceForWebFlux(_mockGenerator.Object, _options);

        // Act
        var result = await adapter.GetEmbeddingAsync("test text");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(1536);
    }

    [Fact]
    public async Task FluxIndexAdapter_GenerateEmbeddingAsync_ReturnsEmbedding()
    {
        // Arrange
        var expectedEmbedding = new float[1536];
        _mockGenerator
            .Setup(g => g.EmbedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator.Object, _options);

        // Act
        var result = await adapter.GenerateEmbeddingAsync("test text");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(1536);
    }

    [Fact]
    public void FluxIndexAdapter_GetModelName_ReturnsConfiguredModel()
    {
        // Arrange
        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator.Object, _options);

        // Act
        var modelName = adapter.GetModelName();

        // Assert
        modelName.Should().Be("text-embedding-3-small");
    }
}
