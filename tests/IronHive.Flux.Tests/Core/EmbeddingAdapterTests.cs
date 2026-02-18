using FluentAssertions;
using IronHive.Abstractions.Embedding;
using Xunit;
using IronHive.Flux.Core.Adapters.Embedding;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IronHive.Flux.Tests.Core;

public class EmbeddingAdapterTests
{
    private readonly IEmbeddingGenerator _mockGenerator;
    private readonly IOptions<IronHiveFluxCoreOptions> _options;

    public EmbeddingAdapterTests()
    {
        _mockGenerator = Substitute.For<IEmbeddingGenerator>();
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
            .EmbedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedEmbedding);

        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);

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
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);
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
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);
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
            .EmbedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedEmbedding);

        var adapter = new IronHiveEmbeddingServiceForWebFlux(_mockGenerator, _options);

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
            .EmbedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedEmbedding);

        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, _options);

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
        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var modelName = adapter.GetModelName();

        // Assert
        modelName.Should().Be("text-embedding-3-small");
    }

    #region FileFlux Properties

    [Fact]
    public void FileFluxAdapter_EmbeddingDimension_ReturnsConfiguredValue()
    {
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);

        adapter.EmbeddingDimension.Should().Be(1536);
    }

    [Fact]
    public void FileFluxAdapter_EmbeddingDimension_ZeroConfig_ReturnsDefault1536()
    {
        var options = Options.Create(new IronHiveFluxCoreOptions
        {
            EmbeddingModelId = "test",
            EmbeddingDimension = 0,
            MaxTokens = 8191
        });
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, options);

        adapter.EmbeddingDimension.Should().Be(1536);
    }

    [Fact]
    public void FileFluxAdapter_MaxTokens_ReturnsConfiguredValue()
    {
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);

        adapter.MaxTokens.Should().Be(8191);
    }

    [Fact]
    public void FileFluxAdapter_SupportsBatchProcessing_ReturnsTrue()
    {
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);

        adapter.SupportsBatchProcessing.Should().BeTrue();
    }

    [Fact]
    public void FileFluxAdapter_Constructor_NullGenerator_Throws()
    {
        var act = () => new IronHiveEmbeddingServiceForFileFlux(null!, _options);

        act.Should().Throw<ArgumentNullException>().WithParameterName("generator");
    }

    [Fact]
    public void FileFluxAdapter_Constructor_NullOptions_Throws()
    {
        var act = () => new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    #endregion

    #region FileFlux Batch

    [Fact]
    public async Task FileFluxAdapter_GenerateBatchEmbeddingsAsync_ReturnsBatch()
    {
        // Arrange
        var embedding1 = new float[] { 1f, 2f, 3f };
        var embedding2 = new float[] { 4f, 5f, 6f };
        _mockGenerator
            .EmbedBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmbeddingResult>
            {
                new() { Embedding = embedding1, Index = 0 },
                new() { Embedding = embedding2, Index = 1 }
            });

        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);

        // Act
        var results = (await adapter.GenerateBatchEmbeddingsAsync(["text1", "text2"])).ToList();

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(embedding1);
        results[1].Should().BeEquivalentTo(embedding2);
    }

    [Fact]
    public async Task FileFluxAdapter_GenerateBatchEmbeddingsAsync_NullEmbedding_ReturnsEmpty()
    {
        // Arrange
        _mockGenerator
            .EmbedBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmbeddingResult>
            {
                new() { Embedding = null, Index = 0 }
            });

        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);

        // Act
        var results = (await adapter.GenerateBatchEmbeddingsAsync(["text1"])).ToList();

        // Assert
        results.Should().HaveCount(1);
        results[0].Should().BeEmpty();
    }

    [Fact]
    public void FileFluxAdapter_CalculateSimilarity_DifferentDimensions_Throws()
    {
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);
        var e1 = new float[] { 1, 0 };
        var e2 = new float[] { 1, 0, 0 };

        var act = () => adapter.CalculateSimilarity(e1, e2);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FileFluxAdapter_CalculateSimilarity_ZeroVectors_ReturnsZero()
    {
        var adapter = new IronHiveEmbeddingServiceForFileFlux(_mockGenerator, _options);
        var zero = new float[] { 0, 0, 0 };

        var similarity = adapter.CalculateSimilarity(zero, zero);

        similarity.Should().Be(0.0);
    }

    #endregion

    #region WebFlux Properties & Batch

    [Fact]
    public void WebFluxAdapter_EmbeddingDimension_ReturnsConfiguredValue()
    {
        var adapter = new IronHiveEmbeddingServiceForWebFlux(_mockGenerator, _options);

        adapter.EmbeddingDimension.Should().Be(1536);
    }

    [Fact]
    public void WebFluxAdapter_MaxTokens_ReturnsConfiguredValue()
    {
        var adapter = new IronHiveEmbeddingServiceForWebFlux(_mockGenerator, _options);

        adapter.MaxTokens.Should().Be(8191);
    }

    [Fact]
    public void WebFluxAdapter_Constructor_NullGenerator_Throws()
    {
        var act = () => new IronHiveEmbeddingServiceForWebFlux(null!, _options);

        act.Should().Throw<ArgumentNullException>().WithParameterName("generator");
    }

    [Fact]
    public async Task WebFluxAdapter_GetEmbeddingsAsync_ReturnsBatch()
    {
        // Arrange
        var embedding1 = new float[] { 1f, 2f };
        var embedding2 = new float[] { 3f, 4f };
        _mockGenerator
            .EmbedBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmbeddingResult>
            {
                new() { Embedding = embedding1, Index = 0 },
                new() { Embedding = embedding2, Index = 1 }
            });

        var adapter = new IronHiveEmbeddingServiceForWebFlux(_mockGenerator, _options);

        // Act
        var results = await adapter.GetEmbeddingsAsync(["text1", "text2"]);

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(embedding1);
        results[1].Should().BeEquivalentTo(embedding2);
    }

    #endregion

    #region FluxIndex Properties & Methods

    [Fact]
    public void FluxIndexAdapter_GetEmbeddingDimension_ReturnsConfiguredValue()
    {
        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, _options);

        adapter.GetEmbeddingDimension().Should().Be(1536);
    }

    [Fact]
    public void FluxIndexAdapter_GetEmbeddingDimension_ZeroConfig_ReturnsDefault1536()
    {
        var options = Options.Create(new IronHiveFluxCoreOptions
        {
            EmbeddingModelId = "test",
            EmbeddingDimension = 0,
            MaxTokens = 8191
        });
        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, options);

        adapter.GetEmbeddingDimension().Should().Be(1536);
    }

    [Fact]
    public void FluxIndexAdapter_GetMaxTokens_ReturnsConfiguredValue()
    {
        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, _options);

        adapter.GetMaxTokens().Should().Be(8191);
    }

    [Fact]
    public async Task FluxIndexAdapter_CountTokensAsync_DelegatesToGenerator()
    {
        // Arrange
        _mockGenerator
            .CountTokensAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(42);
        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var count = await adapter.CountTokensAsync("test text");

        // Assert
        count.Should().Be(42);
        await _mockGenerator.Received(1)
            .CountTokensAsync("text-embedding-3-small", "test text", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FluxIndexAdapter_GenerateEmbeddingsBatchAsync_ReturnsBatch()
    {
        // Arrange
        var embedding1 = new float[] { 1f, 2f, 3f };
        var embedding2 = new float[] { 4f, 5f, 6f };
        _mockGenerator
            .EmbedBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<EmbeddingResult>
            {
                new() { Embedding = embedding1, Index = 0 },
                new() { Embedding = embedding2, Index = 1 }
            });

        var adapter = new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var results = (await adapter.GenerateEmbeddingsBatchAsync(["text1", "text2"])).ToList();

        // Assert
        results.Should().HaveCount(2);
        results[0].Should().BeEquivalentTo(embedding1);
        results[1].Should().BeEquivalentTo(embedding2);
    }

    [Fact]
    public void FluxIndexAdapter_Constructor_NullGenerator_Throws()
    {
        var act = () => new IronHiveEmbeddingServiceForFluxIndex(null!, _options);

        act.Should().Throw<ArgumentNullException>().WithParameterName("generator");
    }

    [Fact]
    public void FluxIndexAdapter_Constructor_NullOptions_Throws()
    {
        var act = () => new IronHiveEmbeddingServiceForFluxIndex(_mockGenerator, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("options");
    }

    #endregion
}
