using FluentAssertions;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexSearchToolTests : IDisposable
{
    private readonly FluxIndexSearchTool _tool;
    private readonly RagContextBuilder _contextBuilder;
    private const string TestIndex = "search-test-index";

    public FluxIndexSearchToolTests()
    {
        var options = Options.Create(new FluxRagToolsOptions
        {
            DefaultIndexName = TestIndex,
            DefaultMinScore = 0.0f // Low threshold for testing keyword matching
        });
        _contextBuilder = new RagContextBuilder(options);
        _tool = new FluxIndexSearchTool(options, _contextBuilder);
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
        var options = Options.Create(new FluxRagToolsOptions());
        var builder = new RagContextBuilder(options);

        var act = () => new FluxIndexSearchTool(null!, builder);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullContextBuilder_ShouldThrow()
    {
        var options = Options.Create(new FluxRagToolsOptions());

        var act = () => new FluxIndexSearchTool(options, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region SearchAsync — Empty Index

    [Fact]
    public async Task SearchAsync_EmptyIndex_ShouldReturnSuccess()
    {
        var resultJson = await _tool.SearchAsync("test query");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("resultCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyIndex_ShouldReturnNotFoundContext()
    {
        var resultJson = await _tool.SearchAsync("test query");
        var result = JsonDocument.Parse(resultJson);

        var context = result.RootElement.GetProperty("context").GetString();
        context.Should().Contain("관련 정보를 찾을 수 없습니다.");
    }

    #endregion

    #region SearchAsync — With Documents

    [Fact]
    public async Task SearchAsync_WithMatchingDocument_ShouldReturnResults()
    {
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "doc-1",
            Content = "The weather in Seattle is rainy",
            Metadata = new Dictionary<string, object> { ["title"] = "Weather Report" }
        });

        var resultJson = await _tool.SearchAsync("weather Seattle");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("resultCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_NoMatchingDocument_ShouldReturnEmpty()
    {
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "doc-1",
            Content = "This document is about cooking"
        });

        // High min score ensures no match with unrelated query words
        var resultJson = await _tool.SearchAsync("quantum physics", minScore: 0.9f);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("resultCount").GetInt32().Should().Be(0);
    }

    #endregion

    #region SearchAsync — Parameters

    [Fact]
    public async Task SearchAsync_WithCustomMaxResults_ShouldLimitResults()
    {
        for (var i = 0; i < 10; i++)
        {
            FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
            {
                Id = $"doc-{i}",
                Content = $"document about test topic number {i}"
            });
        }

        var resultJson = await _tool.SearchAsync("test topic", maxResults: 3);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("resultCount").GetInt32().Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task SearchAsync_WithCustomStrategy_ShouldIncludeInResult()
    {
        var resultJson = await _tool.SearchAsync("test", strategy: "vector");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("strategy").GetString().Should().Be("vector");
    }

    [Fact]
    public async Task SearchAsync_WithCustomIndex_ShouldSearchCustomIndex()
    {
        var customIndex = "custom-search-index";

        try
        {
            FluxIndexSearchTool.AddDocument(customIndex, new StoredDocument
            {
                Id = "custom-doc",
                Content = "custom index content about specific topic"
            });

            var resultJson = await _tool.SearchAsync("specific topic", indexName: customIndex);
            var result = JsonDocument.Parse(resultJson);

            result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        }
        finally
        {
            FluxIndexSearchTool.ClearIndex(customIndex);
        }
    }

    #endregion

    #region SearchAsync — Response Structure

    [Fact]
    public async Task SearchAsync_ShouldReturnQueryInResponse()
    {
        var resultJson = await _tool.SearchAsync("my search query");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("query").GetString().Should().Be("my search query");
    }

    [Fact]
    public async Task SearchAsync_WithResults_ShouldIncludeSourcePreviews()
    {
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "doc-preview",
            Content = "Short content about search testing",
            Metadata = new Dictionary<string, object> { ["title"] = "Test Doc" }
        });

        var resultJson = await _tool.SearchAsync("search testing");
        var result = JsonDocument.Parse(resultJson);

        if (result.RootElement.GetProperty("resultCount").GetInt32() > 0)
        {
            var sources = result.RootElement.GetProperty("sources");
            var firstSource = sources.EnumerateArray().First();
            firstSource.GetProperty("documentId").GetString().Should().Be("doc-preview");
        }
    }

    [Fact]
    public async Task SearchAsync_LongContent_ShouldTruncatePreview()
    {
        var longContent = new string('x', 300) + " matching keyword";
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "doc-long",
            Content = longContent
        });

        var resultJson = await _tool.SearchAsync("keyword");
        var result = JsonDocument.Parse(resultJson);

        if (result.RootElement.GetProperty("resultCount").GetInt32() > 0)
        {
            var sources = result.RootElement.GetProperty("sources");
            var firstSource = sources.EnumerateArray().First();
            var preview = firstSource.GetProperty("preview").GetString();
            preview.Should().EndWith("...");
            preview!.Length.Should().BeLessThanOrEqualTo(203); // 200 + "..."
        }
    }

    #endregion

    #region Static Storage Methods

    [Fact]
    public void AddDocument_ShouldStoreInIndex()
    {
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "add-test",
            Content = "test content"
        });

        FluxIndexSearchTool.RemoveDocument(TestIndex, "add-test").Should().BeTrue();
    }

    [Fact]
    public void RemoveDocument_NonExistentIndex_ShouldReturnFalse()
    {
        FluxIndexSearchTool.RemoveDocument("non-existent-index", "doc-1").Should().BeFalse();
    }

    [Fact]
    public void RemoveDocument_NonExistentDoc_ShouldReturnFalse()
    {
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument
        {
            Id = "existing",
            Content = "content"
        });

        FluxIndexSearchTool.RemoveDocument(TestIndex, "non-existent-doc").Should().BeFalse();
    }

    [Fact]
    public void ClearIndex_ShouldRemoveAllDocuments()
    {
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument { Id = "a", Content = "a" });
        FluxIndexSearchTool.AddDocument(TestIndex, new StoredDocument { Id = "b", Content = "b" });

        FluxIndexSearchTool.ClearIndex(TestIndex);

        FluxIndexSearchTool.RemoveDocument(TestIndex, "a").Should().BeFalse();
        FluxIndexSearchTool.RemoveDocument(TestIndex, "b").Should().BeFalse();
    }

    [Fact]
    public void ClearIndex_NonExistentIndex_ShouldNotThrow()
    {
        var act = () => FluxIndexSearchTool.ClearIndex("never-existed-index");
        act.Should().NotThrow();
    }

    #endregion
}
