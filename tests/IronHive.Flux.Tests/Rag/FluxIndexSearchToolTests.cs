using FluentAssertions;
using FluxIndex.Core.Application.Interfaces;
using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexSearchToolTests
{
    private readonly IVault _vault;
    private readonly FluxIndexSearchTool _tool;
    private readonly RagContextBuilder _contextBuilder;

    public FluxIndexSearchToolTests()
    {
        _vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions
        {
            DefaultMinScore = 0.0f
        });
        _contextBuilder = new RagContextBuilder(options);
        _tool = new FluxIndexSearchTool(_vault, options, _contextBuilder);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullVault_ShouldThrow()
    {
        var options = Options.Create(new FluxRagToolsOptions());
        var builder = new RagContextBuilder(options);

        var act = () => new FluxIndexSearchTool(null!, options, builder);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var builder = new RagContextBuilder(options);

        var act = () => new FluxIndexSearchTool(vault, null!, builder);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullContextBuilder_ShouldThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());

        var act = () => new FluxIndexSearchTool(vault, options, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithOptionalReranker_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var builder = new RagContextBuilder(options);
        var reranker = Substitute.For<IReranker>();

        var tool = new FluxIndexSearchTool(vault, options, builder, reranker);
        tool.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullReranker_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var builder = new RagContextBuilder(options);

        var tool = new FluxIndexSearchTool(vault, options, builder, reranker: null);
        tool.Should().NotBeNull();
    }

    #endregion

    #region SearchAsync — Empty Results

    [Fact]
    public async Task SearchAsync_EmptyResults_ShouldReturnSuccess()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test query"));

        var resultJson = await _tool.SearchAsync("test query");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("resultCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ShouldReturnNotFoundContext()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        var resultJson = await _tool.SearchAsync("test query");
        var result = JsonDocument.Parse(resultJson);

        var context = result.RootElement.GetProperty("context").GetString();
        context.Should().Contain("관련 정보를 찾을 수 없습니다.");
    }

    #endregion

    #region SearchAsync — With Results

    [Fact]
    public async Task SearchAsync_WithMatchingDocument_ShouldReturnResults()
    {
        var searchResult = new VaultSearchResult
        {
            Query = "weather Seattle",
            Items =
            [
                new VaultSearchResultItem
                {
                    Entry = null!,
                    SourcePath = "/docs/weather.md",
                    FileName = "weather.md",
                    Content = "The weather in Seattle is rainy",
                    Score = 0.85f,
                    ChunkIndex = 0
                }
            ],
            TotalCount = 1,
            IsSuccess = true
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("weather Seattle");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("resultCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_ShouldPassOptionsToVault()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        await _tool.SearchAsync("test", maxResults: 10, minScore: 0.7f, pathScope: "/docs");

        await _vault.Received(1).SearchAsync(
            "test",
            Arg.Is<VaultSearchOptions>(o =>
                o.TopK == 10 &&
                o.MinScore == 0.7f &&
                o.PathScope.Count == 1 &&
                o.PathScope[0] == "/docs"),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region SearchAsync — Error Handling

    [Fact]
    public async Task SearchAsync_VaultError_ShouldReturnError()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Error("test", "Index not available"));

        var resultJson = await _tool.SearchAsync("test");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Index not available");
    }

    [Fact]
    public async Task SearchAsync_VaultThrows_ShouldReturnError()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns<VaultSearchResult>(_ => throw new InvalidOperationException("Connection failed"));

        var resultJson = await _tool.SearchAsync("test");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Connection failed");
    }

    #endregion

    #region SearchAsync — Response Structure

    [Fact]
    public async Task SearchAsync_ShouldReturnQueryInResponse()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("my search query"));

        var resultJson = await _tool.SearchAsync("my search query");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("query").GetString().Should().Be("my search query");
    }

    [Fact]
    public async Task SearchAsync_WithResults_ShouldIncludeSourcePreviews()
    {
        var searchResult = new VaultSearchResult
        {
            Query = "search testing",
            Items =
            [
                new VaultSearchResultItem
                {
                    Entry = null!,
                    SourcePath = "/docs/test.md",
                    FileName = "test.md",
                    Content = "Short content about search testing",
                    Score = 0.9f,
                    ChunkIndex = 0
                }
            ],
            TotalCount = 1,
            IsSuccess = true
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("search testing");
        var result = JsonDocument.Parse(resultJson);

        var sources = result.RootElement.GetProperty("sources");
        var firstSource = sources.EnumerateArray().First();
        firstSource.GetProperty("documentId").GetString().Should().Be("/docs/test.md");
        firstSource.GetProperty("title").GetString().Should().Be("test.md");
    }

    [Fact]
    public async Task SearchAsync_LongContent_ShouldTruncatePreview()
    {
        var longContent = new string('x', 300) + " matching keyword";
        var searchResult = new VaultSearchResult
        {
            Query = "keyword",
            Items =
            [
                new VaultSearchResultItem
                {
                    Entry = null!,
                    SourcePath = "/docs/long.md",
                    FileName = "long.md",
                    Content = longContent,
                    Score = 0.8f,
                    ChunkIndex = 0
                }
            ],
            TotalCount = 1,
            IsSuccess = true
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("keyword");
        var result = JsonDocument.Parse(resultJson);

        var sources = result.RootElement.GetProperty("sources");
        var firstSource = sources.EnumerateArray().First();
        var preview = firstSource.GetProperty("preview").GetString();
        preview.Should().EndWith("...");
        preview!.Length.Should().BeLessThanOrEqualTo(203); // 200 + "..."
    }

    [Fact]
    public async Task SearchAsync_ShouldIncludeRerankedFlag_WhenNoReranker()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        var resultJson = await _tool.SearchAsync("test");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("reranked").GetBoolean().Should().BeFalse();
    }

    #endregion

    #region Metadata Extraction

    [Fact]
    public async Task SearchAsync_WithRichMetadata_ShouldExtractBreadcrumb()
    {
        var searchResult = CreateSearchResultWithMetadata(new Dictionary<string, object>
        {
            ["context.breadcrumb"] = "Chapter 1 > Section 2 > Subsection A",
            ["document.topic"] = "Machine Learning",
            ["document.keywords"] = "neural networks, deep learning",
            ["quality.overall"] = 0.92f,
            ["content.structuralRole"] = "paragraph"
        });

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("ML concepts");
        var result = JsonDocument.Parse(resultJson);

        var sources = result.RootElement.GetProperty("sources");
        var first = sources.EnumerateArray().First();
        first.GetProperty("breadcrumb").GetString().Should().Be("Chapter 1 > Section 2 > Subsection A");
        first.GetProperty("documentTopic").GetString().Should().Be("Machine Learning");
        first.GetProperty("keywords").GetString().Should().Be("neural networks, deep learning");
        first.GetProperty("qualityScore").GetSingle().Should().BeApproximately(0.92f, 0.01f);
        first.GetProperty("structuralRole").GetString().Should().Be("paragraph");
    }

    [Fact]
    public async Task SearchAsync_WithNoMetadata_ShouldReturnNullFields()
    {
        var searchResult = new VaultSearchResult
        {
            Query = "test",
            Items =
            [
                new VaultSearchResultItem
                {
                    Entry = null!,
                    SourcePath = "/docs/simple.md",
                    FileName = "simple.md",
                    Content = "Simple content",
                    Score = 0.8f,
                    ChunkIndex = 0,
                    Metadata = null
                }
            ],
            TotalCount = 1,
            IsSuccess = true
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("test");
        var result = JsonDocument.Parse(resultJson);

        var sources = result.RootElement.GetProperty("sources");
        var first = sources.EnumerateArray().First();
        first.GetProperty("breadcrumb").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("documentTopic").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("keywords").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("qualityScore").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("structuralRole").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void MapToSearchResult_WithPartialMetadata_ShouldHandleGracefully()
    {
        var item = new VaultSearchResultItem
        {
            Entry = null!,
            SourcePath = "/docs/partial.md",
            FileName = "partial.md",
            Content = "Partial metadata",
            Score = 0.75f,
            ChunkIndex = 0,
            Metadata = new Dictionary<string, object>
            {
                ["context.breadcrumb"] = "Only breadcrumb"
            }
        };

        var result = FluxIndexSearchTool.MapToSearchResult(item);

        result.Breadcrumb.Should().Be("Only breadcrumb");
        result.DocumentTopic.Should().BeNull();
        result.Keywords.Should().BeNull();
        result.QualityScore.Should().BeNull();
        result.StructuralRole.Should().BeNull();
    }

    [Fact]
    public void MapToSearchResult_WithDoubleQualityScore_ShouldConvertToFloat()
    {
        var item = new VaultSearchResultItem
        {
            Entry = null!,
            SourcePath = "/docs/double.md",
            FileName = "double.md",
            Content = "Double score",
            Score = 0.8f,
            ChunkIndex = 0,
            Metadata = new Dictionary<string, object>
            {
                ["quality.overall"] = 0.85 // double, not float
            }
        };

        var result = FluxIndexSearchTool.MapToSearchResult(item);

        result.QualityScore.Should().BeApproximately(0.85f, 0.001f);
    }

    [Fact]
    public void MapToSearchResult_WithStringQualityScore_ShouldParse()
    {
        var item = new VaultSearchResultItem
        {
            Entry = null!,
            SourcePath = "/docs/string-score.md",
            FileName = "string-score.md",
            Content = "String score",
            Score = 0.7f,
            ChunkIndex = 0,
            Metadata = new Dictionary<string, object>
            {
                ["quality.overall"] = "0.75"
            }
        };

        var result = FluxIndexSearchTool.MapToSearchResult(item);

        result.QualityScore.Should().BeApproximately(0.75f, 0.001f);
    }

    #endregion

    #region Reranking

    [Fact]
    public async Task SearchAsync_WithReranker_ShouldOverFetchByDoubleTopK()
    {
        var reranker = Substitute.For<IReranker>();
        var options = Options.Create(new FluxRagToolsOptions { DefaultMinScore = 0.0f });
        var builder = new RagContextBuilder(options);
        var toolWithReranker = new FluxIndexSearchTool(
            _vault, options, builder, reranker);

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        reranker.RerankAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<RetrievalCandidate>>(),
            Arg.Any<RerankOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(Enumerable.Empty<RerankResult>());

        await toolWithReranker.SearchAsync("test", maxResults: 5);

        // Should fetch TopK=10 (5*2) when reranker is present
        await _vault.Received(1).SearchAsync(
            "test",
            Arg.Is<VaultSearchOptions>(o => o.TopK == 10),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WithReranker_ShouldRerankResults()
    {
        var reranker = Substitute.For<IReranker>();
        var options = Options.Create(new FluxRagToolsOptions { DefaultMinScore = 0.0f });
        var builder = new RagContextBuilder(options);
        var toolWithReranker = new FluxIndexSearchTool(
            _vault, options, builder, reranker);

        var searchResult = new VaultSearchResult
        {
            Query = "test",
            Items =
            [
                new VaultSearchResultItem
                {
                    Entry = null!,
                    SourcePath = "/docs/a.md",
                    FileName = "a.md",
                    Content = "Content A",
                    Score = 0.9f,
                    ChunkIndex = 0
                },
                new VaultSearchResultItem
                {
                    Entry = null!,
                    SourcePath = "/docs/b.md",
                    FileName = "b.md",
                    Content = "Content B",
                    Score = 0.7f,
                    ChunkIndex = 0
                }
            ],
            TotalCount = 2,
            IsSuccess = true
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        // Reranker returns B higher than A
        reranker.RerankAsync(
            Arg.Any<string>(),
            Arg.Any<IEnumerable<RetrievalCandidate>>(),
            Arg.Any<RerankOptions>(),
            Arg.Any<CancellationToken>())
            .Returns(new List<RerankResult>
            {
                new() { Id = "/docs/b.md:0", Content = "Content B", RerankScore = 0.95f, NewRank = 1 },
                new() { Id = "/docs/a.md:0", Content = "Content A", RerankScore = 0.80f, NewRank = 2 }
            });

        var resultJson = await toolWithReranker.SearchAsync("test");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("reranked").GetBoolean().Should().BeTrue();

        var sources = result.RootElement.GetProperty("sources");
        var sourcesList = sources.EnumerateArray().ToList();
        // After reranking, B should be first (higher rerank score)
        sourcesList[0].GetProperty("documentId").GetString().Should().Be("/docs/b.md");
        sourcesList[0].GetProperty("score").GetSingle().Should().BeApproximately(0.95f, 0.01f);
    }

    [Fact]
    public async Task SearchAsync_WithoutReranker_ShouldNotOverFetch()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        await _tool.SearchAsync("test", maxResults: 5);

        // Without reranker, TopK should be exactly 5
        await _vault.Received(1).SearchAsync(
            "test",
            Arg.Is<VaultSearchOptions>(o => o.TopK == 5),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region GetString / GetFloat Helpers

    [Fact]
    public void GetString_NullMetadata_ShouldReturnNull()
    {
        FluxIndexSearchTool.GetString(null, "any.key").Should().BeNull();
    }

    [Fact]
    public void GetString_MissingKey_ShouldReturnNull()
    {
        var metadata = new Dictionary<string, object> { ["other.key"] = "value" };
        FluxIndexSearchTool.GetString(metadata, "missing.key").Should().BeNull();
    }

    [Fact]
    public void GetString_StringValue_ShouldReturn()
    {
        var metadata = new Dictionary<string, object> { ["my.key"] = "hello" };
        FluxIndexSearchTool.GetString(metadata, "my.key").Should().Be("hello");
    }

    [Fact]
    public void GetString_NonStringValue_ShouldCallToString()
    {
        var metadata = new Dictionary<string, object> { ["my.key"] = 42 };
        FluxIndexSearchTool.GetString(metadata, "my.key").Should().Be("42");
    }

    [Fact]
    public void GetFloat_NullMetadata_ShouldReturnNull()
    {
        FluxIndexSearchTool.GetFloat(null, "any.key").Should().BeNull();
    }

    [Fact]
    public void GetFloat_FloatValue_ShouldReturn()
    {
        var metadata = new Dictionary<string, object> { ["score"] = 0.85f };
        FluxIndexSearchTool.GetFloat(metadata, "score").Should().BeApproximately(0.85f, 0.001f);
    }

    [Fact]
    public void GetFloat_DoubleValue_ShouldConvert()
    {
        var metadata = new Dictionary<string, object> { ["score"] = 0.92 };
        FluxIndexSearchTool.GetFloat(metadata, "score").Should().BeApproximately(0.92f, 0.001f);
    }

    [Fact]
    public void GetFloat_IntValue_ShouldConvert()
    {
        var metadata = new Dictionary<string, object> { ["score"] = 1 };
        FluxIndexSearchTool.GetFloat(metadata, "score").Should().Be(1.0f);
    }

    [Fact]
    public void GetFloat_StringValue_ShouldParse()
    {
        var metadata = new Dictionary<string, object> { ["score"] = "0.77" };
        FluxIndexSearchTool.GetFloat(metadata, "score").Should().BeApproximately(0.77f, 0.001f);
    }

    [Fact]
    public void GetFloat_InvalidStringValue_ShouldReturnNull()
    {
        var metadata = new Dictionary<string, object> { ["score"] = "not-a-number" };
        FluxIndexSearchTool.GetFloat(metadata, "score").Should().BeNull();
    }

    #endregion

    #region Helpers

    private static VaultSearchResult CreateSearchResultWithMetadata(Dictionary<string, object> metadata)
    {
        return new VaultSearchResult
        {
            Query = "test",
            Items =
            [
                new VaultSearchResultItem
                {
                    Entry = null!,
                    SourcePath = "/docs/enriched.md",
                    FileName = "enriched.md",
                    Content = "Enriched content with metadata",
                    Score = 0.88f,
                    ChunkIndex = 0,
                    Metadata = metadata
                }
            ],
            TotalCount = 1,
            IsSuccess = true
        };
    }

    #endregion
}
