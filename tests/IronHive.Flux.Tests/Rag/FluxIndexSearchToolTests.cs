using AwesomeAssertions;
using FluxIndex.Core.Application.Interfaces;
using FluxFeed.Interfaces;
using FluxFeed.Services;
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

        var resultJson = await _tool.SearchAsync("test query", cancellationToken: TestContext.Current.CancellationToken);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("resultCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyResults_ShouldReturnNotFoundContext()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        var resultJson = await _tool.SearchAsync("test query", cancellationToken: TestContext.Current.CancellationToken);
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
            TotalCount = 1
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("weather Seattle", cancellationToken: TestContext.Current.CancellationToken);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("resultCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SearchAsync_ShouldPassOptionsToVault()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        await _tool.SearchAsync("test", maxResults: 10, minScore: 0.7f, pathScope: "/docs", cancellationToken: TestContext.Current.CancellationToken);

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
    public async Task SearchAsync_CallerCancels_Propagates()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns<VaultSearchResult>(ci => throw new OperationCanceledException(ci.ArgAt<CancellationToken>(2)));

        var act = () => _tool.SearchAsync("test", cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SearchAsync_VaultThrows_ShouldReturnError()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns<VaultSearchResult>(_ => throw new InvalidOperationException("Connection failed"));

        var resultJson = await _tool.SearchAsync("test", cancellationToken: TestContext.Current.CancellationToken);
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

        var resultJson = await _tool.SearchAsync("my search query", cancellationToken: TestContext.Current.CancellationToken);
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
            TotalCount = 1
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("search testing", cancellationToken: TestContext.Current.CancellationToken);
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
            TotalCount = 1
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("keyword", cancellationToken: TestContext.Current.CancellationToken);
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

        var resultJson = await _tool.SearchAsync("test", cancellationToken: TestContext.Current.CancellationToken);
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("reranked").GetBoolean().Should().BeFalse();
    }

    #endregion

    #region Metadata Extraction

    // The tool reads what the vault writes. These keys come from FluxFeed's own constants, so a renamed key breaks the
    // build here instead of turning the fields null in production (the previous keys were never written to a vault chunk).
    [Fact]
    public async Task SearchAsync_TableChunk_ReportsItsKindAndPages()
    {
        var searchResult = CreateSearchResultWithMetadata(new Dictionary<string, object>
        {
            [VaultPipeline.ChunkKindMetadataKey] = VaultPipeline.TableChunkKind,
            [VaultPipeline.StartPageMetadataKey] = 3,
            [VaultPipeline.EndPageMetadataKey] = 4L,
            [VaultPipeline.HeadingPathMetadataKey] = "Results > Revenue",
        });

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("revenue table", cancellationToken: TestContext.Current.CancellationToken);
        var first = JsonDocument.Parse(resultJson).RootElement.GetProperty("sources").EnumerateArray().First();

        first.GetProperty("kind").GetString().Should().Be("table");
        first.GetProperty("startPage").GetInt32().Should().Be(3);
        first.GetProperty("endPage").GetInt32().Should().Be(4);
        first.GetProperty("headingPath").GetString().Should().Be("Results > Revenue");
        first.TryGetProperty("breadcrumb", out _).Should().BeFalse("the fields nothing wrote are gone");
    }

    [Fact]
    public async Task SearchAsync_NoMetadata_IsATextChunkWithoutPages()
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
            TotalCount = 1
        };

        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(searchResult);

        var resultJson = await _tool.SearchAsync("test", cancellationToken: TestContext.Current.CancellationToken);
        var first = JsonDocument.Parse(resultJson).RootElement.GetProperty("sources").EnumerateArray().First();

        first.GetProperty("kind").GetString().Should().Be("text");
        first.GetProperty("startPage").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("endPage").ValueKind.Should().Be(JsonValueKind.Null);
        first.GetProperty("headingPath").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void MapToSearchResult_ImageOnOnePage_UsesPageNumberForBothEnds()
    {
        var item = new VaultSearchResultItem
        {
            Entry = null!,
            SourcePath = "/docs/deck.pptx",
            FileName = "deck.pptx",
            Content = "A bar chart of quarterly revenue",
            Score = 0.75f,
            ChunkIndex = 7,
            Metadata = new Dictionary<string, object>
            {
                [VaultPipeline.ChunkKindMetadataKey] = VaultPipeline.ImageDescriptionChunkKind,
                [VaultPipeline.PageNumberMetadataKey] = "5",
            }
        };

        var result = FluxIndexSearchTool.MapToSearchResult(item);

        result.Kind.Should().Be("image_description");
        result.StartPage.Should().Be(5);
        result.EndPage.Should().Be(5);
    }

    #endregion

    #region Reranking

    [Fact]
    public async Task SearchAsync_WithReranker_AsksTheVaultToRerank_AndNeverRerunsItItself()
    {
        // The vault owns reranking (FluxFeed VaultSearchOptions.UseReranker); the tool used to repeat it with its own
        // over-fetch, id mapping and score swap. It now only asks, with the same candidate pool it used (topK * 2).
        var reranker = Substitute.For<IReranker>();
        var options = Options.Create(new FluxRagToolsOptions { DefaultMinScore = 0.0f });
        var builder = new RagContextBuilder(options);
        var toolWithReranker = new FluxIndexSearchTool(_vault, options, builder, reranker);
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        var json = await toolWithReranker.SearchAsync("test", maxResults: 5, cancellationToken: TestContext.Current.CancellationToken);

        await _vault.Received(1).SearchAsync(
            "test",
            Arg.Is<VaultSearchOptions>(o => o.UseReranker && o.TopK == 5 && o.RerankCandidateCount == 10),
            Arg.Any<CancellationToken>());
        await reranker.DidNotReceiveWithAnyArgs().RerankAsync(default!, default!, default!, TestContext.Current.CancellationToken);
        json.Should().Contain("\"reranked\": true");
    }

    [Fact]
    public async Task SearchAsync_WithoutReranker_DoesNotAskForReranking()
    {
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(VaultSearchResult.Empty("test"));

        await _tool.SearchAsync("test", maxResults: 5, cancellationToken: TestContext.Current.CancellationToken);

        await _vault.Received(1).SearchAsync(
            "test",
            Arg.Is<VaultSearchOptions>(o => !o.UseReranker && o.TopK == 5 && o.RerankCandidateCount == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_WithReranker_KeepsTheVaultsRerankedOrderAndScores()
    {
        var reranker = Substitute.For<IReranker>();
        var options = Options.Create(new FluxRagToolsOptions { DefaultMinScore = 0.0f });
        var builder = new RagContextBuilder(options);
        var toolWithReranker = new FluxIndexSearchTool(_vault, options, builder, reranker);
        _vault.SearchAsync(Arg.Any<string>(), Arg.Any<VaultSearchOptions>(), Arg.Any<CancellationToken>())
            .Returns(new VaultSearchResult
            {
                Query = "test",
                Items =
                [
                    new VaultSearchResultItem { Entry = null!, SourcePath = "/docs/b.md", FileName = "b.md", Content = "Content B", Score = 0.95f, RetrievalScore = 0.7f, ChunkIndex = 0 },
                    new VaultSearchResultItem { Entry = null!, SourcePath = "/docs/a.md", FileName = "a.md", Content = "Content A", Score = 0.80f, RetrievalScore = 0.9f, ChunkIndex = 0 }
                ],
                TotalCount = 2
            });

        var json = await toolWithReranker.SearchAsync("test", maxResults: 5, cancellationToken: TestContext.Current.CancellationToken);

        json.IndexOf("/docs/b.md", StringComparison.Ordinal).Should().BeLessThan(json.IndexOf("/docs/a.md", StringComparison.Ordinal));
        json.Should().Contain("0.95");
    }

    #endregion

    #region GetString / GetInt Helpers

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
    public void GetInt_NullMetadata_ShouldReturnNull()
    {
        FluxIndexSearchTool.GetInt(null, "any.key").Should().BeNull();
    }

    [Theory]
    [InlineData(7)]
    [InlineData(7L)]
    [InlineData(7.0)]
    [InlineData("7")]
    public void GetInt_StoredForms_ShouldRead(object stored)
    {
        var metadata = new Dictionary<string, object> { ["page"] = stored };
        FluxIndexSearchTool.GetInt(metadata, "page").Should().Be(7);
    }

    [Fact]
    public void GetInt_JsonNumber_ShouldRead()
    {
        var metadata = new Dictionary<string, object> { ["page"] = JsonDocument.Parse("12").RootElement.Clone() };
        FluxIndexSearchTool.GetInt(metadata, "page").Should().Be(12);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData(2.5)]
    public void GetInt_NotAnInteger_ShouldReturnNull(object stored)
    {
        var metadata = new Dictionary<string, object> { ["page"] = stored };
        FluxIndexSearchTool.GetInt(metadata, "page").Should().BeNull();
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
            TotalCount = 1
        };
    }

    #endregion
}
