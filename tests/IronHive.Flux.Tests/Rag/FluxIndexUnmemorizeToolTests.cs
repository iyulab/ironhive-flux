using FluentAssertions;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexUnmemorizeToolTests : IDisposable
{
    private readonly FluxIndexUnmemorizeTool _tool;
    private const string TestIndex = "unmemorize-test-index";

    public FluxIndexUnmemorizeToolTests()
    {
        var options = Options.Create(new FluxRagToolsOptions
        {
            DefaultIndexName = TestIndex
        });
        _tool = new FluxIndexUnmemorizeTool(options);
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
        var act = () => new FluxIndexUnmemorizeTool(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UnmemorizeAsync - Successful Deletion

    [Fact]
    public async Task UnmemorizeAsync_WithExistingDocument_ShouldReturnDeleted()
    {
        // Arrange
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "doc-1",
            Content = "test content"
        });

        // Act
        var resultJson = await _tool.UnmemorizeAsync("doc-1");
        var result = JsonDocument.Parse(resultJson);

        // Assert
        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("deleted").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("documentId").GetString().Should().Be("doc-1");
        result.RootElement.GetProperty("deletedAt").GetString().Should().NotBeNullOrEmpty();
        result.RootElement.GetProperty("message").GetString().Should().Contain("성공");
    }

    [Fact]
    public async Task UnmemorizeAsync_ShouldActuallyRemoveFromStorage()
    {
        // Arrange
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "doc-remove",
            Content = "to be removed"
        });

        // Act
        await _tool.UnmemorizeAsync("doc-remove");

        // Assert - Second deletion should fail (already removed)
        var resultJson = await _tool.UnmemorizeAsync("doc-remove");
        var result = JsonDocument.Parse(resultJson);
        result.RootElement.GetProperty("deleted").GetBoolean().Should().BeFalse();
    }

    #endregion

    #region UnmemorizeAsync - Non-Existent Document

    [Fact]
    public async Task UnmemorizeAsync_WithNonExistentDocument_ShouldReturnNotDeleted()
    {
        // Act
        var resultJson = await _tool.UnmemorizeAsync("non-existent");
        var result = JsonDocument.Parse(resultJson);

        // Assert
        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("deleted").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("message").GetString().Should().Contain("찾을 수 없");
    }

    #endregion

    #region UnmemorizeAsync - Index Name

    [Fact]
    public async Task UnmemorizeAsync_WithCustomIndexName_ShouldUseCustomIndex()
    {
        // Arrange
        var customIndex = "custom-unmemorize-index";
        FluxIndexSearchTool.AddDocument(customIndex, new StoredDocument
        {
            Id = "doc-custom",
            Content = "custom content"
        });

        try
        {
            // Act
            var resultJson = await _tool.UnmemorizeAsync("doc-custom", indexName: customIndex);
            var result = JsonDocument.Parse(resultJson);

            // Assert
            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
            result.RootElement.GetProperty("indexName").GetString().Should().Be(customIndex);
        }
        finally
        {
            FluxIndexSearchTool.ClearIndex(customIndex);
        }
    }

    [Fact]
    public async Task UnmemorizeAsync_WithDefaultIndex_ShouldUseOptionsDefault()
    {
        // Arrange
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "doc-default",
            Content = "default index content"
        });

        // Act
        var resultJson = await _tool.UnmemorizeAsync("doc-default");
        var result = JsonDocument.Parse(resultJson);

        // Assert
        result.RootElement.GetProperty("indexName").GetString().Should().Be(TestIndex);
    }

    #endregion

    #region UnmemorizeAsync - Multiple Documents

    [Fact]
    public async Task UnmemorizeAsync_ShouldOnlyRemoveTargetDocument()
    {
        // Arrange
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "keep-me",
            Content = "should remain"
        });
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "delete-me",
            Content = "should be deleted"
        });

        // Act
        await _tool.UnmemorizeAsync("delete-me");

        // Assert - "keep-me" should still be there, "delete-me" should not
        var deleteAgain = await _tool.UnmemorizeAsync("delete-me");
        var keepResult = JsonDocument.Parse(deleteAgain);
        keepResult.RootElement.GetProperty("deleted").GetBoolean().Should().BeFalse();

        // Verify "keep-me" still exists by trying to remove it
        var keepJson = await _tool.UnmemorizeAsync("keep-me");
        var keepDoc = JsonDocument.Parse(keepJson);
        keepDoc.RootElement.GetProperty("deleted").GetBoolean().Should().BeTrue();
    }

    #endregion
}
