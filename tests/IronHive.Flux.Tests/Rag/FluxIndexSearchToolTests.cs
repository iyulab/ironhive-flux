using FluentAssertions;
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

    #endregion
}
