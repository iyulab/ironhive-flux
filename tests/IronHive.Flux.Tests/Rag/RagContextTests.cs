using FluentAssertions;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class RagContextTests
{
    #region RagContext — Record Properties

    [Fact]
    public void RagContext_RequiredProperties_ShouldBeSet()
    {
        var context = new RagContext
        {
            ContextText = "Some context text"
        };

        context.ContextText.Should().Be("Some context text");
        context.Sources.Should().BeEmpty();
        context.TokenCount.Should().Be(0);
        context.AverageRelevance.Should().Be(0);
        context.SearchStrategy.Should().Be("hybrid");
    }

    [Fact]
    public void RagContext_WithAllProperties_ShouldRetainValues()
    {
        var sources = new List<RagSearchResult>
        {
            new() { DocumentId = "1", Content = "text", Score = 0.9f }
        };

        var context = new RagContext
        {
            ContextText = "text",
            Sources = sources,
            TokenCount = 100,
            AverageRelevance = 0.85f,
            SearchStrategy = "vector"
        };

        context.Sources.Should().HaveCount(1);
        context.TokenCount.Should().Be(100);
        context.AverageRelevance.Should().Be(0.85f);
        context.SearchStrategy.Should().Be("vector");
    }

    [Fact]
    public void RagContext_CreatedAt_ShouldBeRecentUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var context = new RagContext { ContextText = "text" };
        var after = DateTime.UtcNow.AddSeconds(1);

        context.CreatedAt.Should().BeAfter(before);
        context.CreatedAt.Should().BeBefore(after);
    }

    #endregion

    #region RagSearchResult — Record Properties

    [Fact]
    public void RagSearchResult_RequiredProperties_ShouldBeSet()
    {
        var result = new RagSearchResult
        {
            DocumentId = "doc-1",
            Content = "chunk content"
        };

        result.DocumentId.Should().Be("doc-1");
        result.Content.Should().Be("chunk content");
        result.Score.Should().Be(0);
        result.Metadata.Should().BeNull();
        result.ChunkIndex.Should().BeNull();
        result.Title.Should().BeNull();
    }

    [Fact]
    public void RagSearchResult_WithAllProperties_ShouldRetainValues()
    {
        var metadata = new Dictionary<string, object> { ["source"] = "test" };
        var result = new RagSearchResult
        {
            DocumentId = "doc-1",
            Content = "content",
            Score = 0.95f,
            Metadata = metadata,
            ChunkIndex = 3,
            Title = "Test Document"
        };

        result.Score.Should().Be(0.95f);
        result.Metadata.Should().ContainKey("source");
        result.ChunkIndex.Should().Be(3);
        result.Title.Should().Be("Test Document");
    }

    [Fact]
    public void RagSearchResult_RecordEquality_ShouldWork()
    {
        var r1 = new RagSearchResult { DocumentId = "1", Content = "text", Score = 0.5f };
        var r2 = new RagSearchResult { DocumentId = "1", Content = "text", Score = 0.5f };

        r1.Should().Be(r2);
    }

    #endregion

    #region RagContextOptions — Defaults & Properties

    [Fact]
    public void RagContextOptions_Defaults_ShouldBeCorrect()
    {
        var options = new RagContextOptions { Query = "test" };

        options.MaxResults.Should().Be(5);
        options.Strategy.Should().Be("hybrid");
        options.MinScore.Should().Be(0.5f);
        options.MaxTokens.Should().Be(4000);
        options.IndexName.Should().BeNull();
        options.MetadataFilter.Should().BeNull();
    }

    [Fact]
    public void RagContextOptions_AllProperties_ShouldBeSettable()
    {
        var filter = new Dictionary<string, object> { ["category"] = "tech" };
        var options = new RagContextOptions
        {
            Query = "search query",
            MaxResults = 10,
            Strategy = "vector",
            MinScore = 0.8f,
            MaxTokens = 8000,
            IndexName = "custom-index",
            MetadataFilter = filter
        };

        options.Query.Should().Be("search query");
        options.MaxResults.Should().Be(10);
        options.Strategy.Should().Be("vector");
        options.MinScore.Should().Be(0.8f);
        options.MaxTokens.Should().Be(8000);
        options.IndexName.Should().Be("custom-index");
        options.MetadataFilter.Should().ContainKey("category");
    }

    #endregion

    #region FluxRagToolsOptions — Defaults

    [Fact]
    public void FluxRagToolsOptions_Defaults_ShouldBeCorrect()
    {
        var options = new FluxRagToolsOptions();

        options.DefaultMaxResults.Should().Be(5);
        options.DefaultSearchStrategy.Should().Be("hybrid");
        options.DefaultMinScore.Should().Be(0.5f);
        options.MaxContextTokens.Should().Be(4000);
        options.ChunkSeparator.Should().Be("\n\n---\n\n");
        options.DefaultIndexName.Should().Be("default");
        options.ToolTimeout.Should().Be(60);
    }

    [Fact]
    public void FluxRagToolsOptions_AllProperties_ShouldBeSettable()
    {
        var options = new FluxRagToolsOptions
        {
            DefaultMaxResults = 20,
            DefaultSearchStrategy = "keyword",
            DefaultMinScore = 0.3f,
            MaxContextTokens = 16000,
            ChunkSeparator = "\n---\n",
            DefaultIndexName = "my-index",
            ToolTimeout = 120
        };

        options.DefaultMaxResults.Should().Be(20);
        options.DefaultSearchStrategy.Should().Be("keyword");
        options.DefaultMinScore.Should().Be(0.3f);
        options.MaxContextTokens.Should().Be(16000);
        options.ChunkSeparator.Should().Be("\n---\n");
        options.DefaultIndexName.Should().Be("my-index");
        options.ToolTimeout.Should().Be(120);
    }

    #endregion
}
