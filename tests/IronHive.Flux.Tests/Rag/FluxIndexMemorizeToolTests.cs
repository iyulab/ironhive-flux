using FluentAssertions;
using FluxIndex.Core.Application.Interfaces;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexMemorizeToolTests : IDisposable
{
    private static readonly float[] s_testEmbedding = [0.1f, 0.2f, 0.3f];

    private readonly FluxIndexMemorizeTool _tool;
    private const string TestIndex = "memorize-test-index";

    public FluxIndexMemorizeToolTests()
    {
        var options = Options.Create(new FluxRagToolsOptions
        {
            DefaultIndexName = TestIndex
        });
        _tool = new FluxIndexMemorizeTool(options);
    }

    public void Dispose()
    {
        FluxIndexSearchTool.ClearIndex(TestIndex);
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var act = () => new FluxIndexMemorizeTool(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldNotThrow()
    {
        var options = Options.Create(new FluxRagToolsOptions());
        var tool = new FluxIndexMemorizeTool(options);
        tool.Should().NotBeNull();
    }

    #endregion

    #region MemorizeAsync — Basic

    [Fact]
    public async Task MemorizeAsync_WithContent_ShouldReturnSuccess()
    {
        var resultJson = await _tool.MemorizeAsync("Hello world content");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("contentLength").GetInt32().Should().Be(19);
        result.RootElement.GetProperty("indexName").GetString().Should().Be(TestIndex);
        result.RootElement.GetProperty("hasEmbedding").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task MemorizeAsync_WithDocumentId_ShouldUseProvidedId()
    {
        var resultJson = await _tool.MemorizeAsync("content", documentId: "custom-id-123");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("documentId").GetString().Should().Be("custom-id-123");
    }

    [Fact]
    public async Task MemorizeAsync_WithoutDocumentId_ShouldAutoGenerateId()
    {
        var resultJson = await _tool.MemorizeAsync("content");
        var result = JsonDocument.Parse(resultJson);

        var docId = result.RootElement.GetProperty("documentId").GetString();
        docId.Should().NotBeNullOrEmpty();
        Guid.TryParse(docId, out _).Should().BeTrue();
    }

    #endregion

    #region MemorizeAsync — Title & Metadata

    [Fact]
    public async Task MemorizeAsync_WithTitle_ShouldIncludeInMetadata()
    {
        var resultJson = await _tool.MemorizeAsync("content", title: "My Document");
        var result = JsonDocument.Parse(resultJson);

        var metadata = result.RootElement.GetProperty("metadata");
        metadata.GetProperty("title").GetString().Should().Be("My Document");
    }

    [Fact]
    public async Task MemorizeAsync_WithValidMetadata_ShouldParse()
    {
        var metadataJson = """{"category": "tech", "author": "test"}""";
        var resultJson = await _tool.MemorizeAsync("content", metadata: metadataJson);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        var metadata = result.RootElement.GetProperty("metadata");
        metadata.GetProperty("memorizedAt").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MemorizeAsync_WithInvalidMetadata_ShouldStillSucceed()
    {
        // Invalid JSON metadata should be ignored gracefully
        var resultJson = await _tool.MemorizeAsync("content", metadata: "not-valid-json");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task MemorizeAsync_WithNullMetadata_ShouldSucceed()
    {
        var resultJson = await _tool.MemorizeAsync("content", metadata: null);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("metadata").GetProperty("memorizedAt").GetString().Should().NotBeNullOrEmpty();
    }

    #endregion

    #region MemorizeAsync — Index Name

    [Fact]
    public async Task MemorizeAsync_WithCustomIndex_ShouldUseCustomIndex()
    {
        var customIndex = "custom-memorize-index";

        try
        {
            var resultJson = await _tool.MemorizeAsync("content", indexName: customIndex);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("indexName").GetString().Should().Be(customIndex);
        }
        finally
        {
            FluxIndexSearchTool.ClearIndex(customIndex);
        }
    }

    [Fact]
    public async Task MemorizeAsync_WithDefaultIndex_ShouldUseOptionsDefault()
    {
        var resultJson = await _tool.MemorizeAsync("content");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("indexName").GetString().Should().Be(TestIndex);
    }

    #endregion

    #region MemorizeAsync — Embedding Service

    [Fact]
    public async Task MemorizeAsync_WithEmbeddingService_ShouldGenerateEmbedding()
    {
        var mockEmbedding = Substitute.For<IEmbeddingService>();
        mockEmbedding.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(s_testEmbedding);

        var options = Options.Create(new FluxRagToolsOptions { DefaultIndexName = TestIndex });
        var tool = new FluxIndexMemorizeTool(options, embeddingService: mockEmbedding);

        var resultJson = await tool.MemorizeAsync("content with embedding");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("hasEmbedding").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("embeddingDimension").GetInt32().Should().Be(3);
        await mockEmbedding.Received(1).GenerateEmbeddingAsync("content with embedding", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MemorizeAsync_WithoutEmbeddingService_ShouldNotHaveEmbedding()
    {
        var resultJson = await _tool.MemorizeAsync("content");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("hasEmbedding").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("embeddingDimension").GetInt32().Should().Be(0);
    }

    #endregion

    #region MemorizeAsync — Storage Verification

    [Fact]
    public async Task MemorizeAsync_ShouldStoreDocumentForLaterSearch()
    {
        await _tool.MemorizeAsync("stored content", documentId: "stored-doc");

        // Verify by attempting to delete (which confirms storage)
        var deleted = FluxIndexSearchTool.RemoveDocument(TestIndex, "stored-doc");
        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task MemorizeAsync_MultipleDocs_ShouldStoreAll()
    {
        await _tool.MemorizeAsync("first", documentId: "doc-1");
        await _tool.MemorizeAsync("second", documentId: "doc-2");

        FluxIndexSearchTool.RemoveDocument(TestIndex, "doc-1").Should().BeTrue();
        FluxIndexSearchTool.RemoveDocument(TestIndex, "doc-2").Should().BeTrue();
    }

    #endregion
}
