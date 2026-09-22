using AwesomeAssertions;
using FluxFeed.Interfaces;
using IronHive.Flux.Rag.Extensions;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using WebFlux.Core.Interfaces;
using WebFlux.Core.Models;
using WebFlux.Core.Options;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

/// <summary>
/// The tool extracts through WebFlux (robots.txt, per-request timeout, boilerplate removal, Markdown) rather than
/// its own HttpClient — so the extractor is the seam, and the whole flow is testable without a network.
/// </summary>
public class FluxIndexWebMemorizeToolTests
{
    private readonly IVault _vault = Substitute.For<IVault>();
    private readonly IContentExtractService _extractor = Substitute.For<IContentExtractService>();
    private readonly IOptions<FluxRagToolsOptions> _options = Options.Create(new FluxRagToolsOptions());

    private FluxIndexWebMemorizeTool CreateTool() => new(_vault, _options, _extractor);

    private void ExtractorReturns(ExtractedContent page) =>
        _extractor.ExtractContentAsync(Arg.Any<string>(), Arg.Any<ExtractOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ProcessingResult.Success(page));

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    #region Constructor

    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        FluentActions.Invoking(() => new FluxIndexWebMemorizeTool(null!, _options, _extractor)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FluxIndexWebMemorizeTool(_vault, null!, _extractor)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new FluxIndexWebMemorizeTool(_vault, _options, null!)).Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region URL validation

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://example.com/file.txt")]
    [InlineData("/relative/path")]
    public async Task InvalidUrl_IsRejected_WithoutCallingTheExtractor(string url)
    {
        var result = Parse(await CreateTool().MemorizeWebPageAsync(url, cancellationToken: TestContext.Current.CancellationToken));

        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("error").GetString().Should().Contain("Invalid URL");
        await _extractor.DidNotReceiveWithAnyArgs().ExtractContentAsync(default!, default, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Extraction through WebFlux

    [Fact]
    public async Task Memorize_AsksWebFluxForMarkdown_AndIndexesWhatItExtracted()
    {
        ExtractorReturns(new ExtractedContent { Url = "https://example.com/guide", Title = "The Guide", MainContent = "## Install\n\nRun the thing." });
        string? written = null;
        _vault.When(v => v.MemorizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()))
            .Do(call => written = File.ReadAllText(call.Arg<string>()));

        var result = Parse(await CreateTool().MemorizeWebPageAsync("https://example.com/guide", cancellationToken: TestContext.Current.CancellationToken));

        result.GetProperty("success").GetBoolean().Should().BeTrue();
        result.GetProperty("title").GetString().Should().Be("The Guide", "the page's own title is used when none is given");
        await _extractor.Received(1).ExtractContentAsync(
            "https://example.com/guide",
            Arg.Is<ExtractOptions?>(o => o != null && o.Format == OutputFormat.Markdown),
            Arg.Any<CancellationToken>());
        written.Should().Contain("## Install").And.Contain("Run the thing.").And.Contain("source: https://example.com/guide");
    }

    [Fact]
    public async Task Memorize_ACallerTitleWins_AndAPageWithoutTitleFallsBackToTheUrl()
    {
        ExtractorReturns(new ExtractedContent { Title = "Page Title", MainContent = "body" });
        var named = Parse(await CreateTool().MemorizeWebPageAsync("https://example.com/a", "Mine", TestContext.Current.CancellationToken));
        named.GetProperty("title").GetString().Should().Be("Mine");

        ExtractorReturns(new ExtractedContent { Title = "", MainContent = "body" });
        var untitled = Parse(await CreateTool().MemorizeWebPageAsync("https://example.com/getting-started", cancellationToken: TestContext.Current.CancellationToken));
        untitled.GetProperty("title").GetString().Should().Be("getting started");
    }

    [Fact]
    public async Task Memorize_WhenWebFluxRefuses_ReportsItsReason_AndIndexesNothing()
    {
        // e.g. robots.txt disallows the path, or the request timed out — WebFlux decides, the tool reports.
        _extractor.ExtractContentAsync(Arg.Any<string>(), Arg.Any<ExtractOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ProcessingResult.Failure<ExtractedContent>("Disallowed by robots.txt", "ROBOTS"));

        var result = Parse(await CreateTool().MemorizeWebPageAsync("https://example.com/private", cancellationToken: TestContext.Current.CancellationToken));

        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("error").GetString().Should().Contain("Disallowed by robots.txt");
        await _vault.DidNotReceiveWithAnyArgs().MemorizeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Memorize_EmptyExtraction_IsAnError_NotAnEmptyDocument()
    {
        ExtractorReturns(new ExtractedContent { MainContent = "  ", Text = "" });

        var result = Parse(await CreateTool().MemorizeWebPageAsync("https://example.com/", cancellationToken: TestContext.Current.CancellationToken));

        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("error").GetString().Should().Contain("empty");
        await _vault.DidNotReceiveWithAnyArgs().MemorizeAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Memorize_VaultFailure_IsReported()
    {
        ExtractorReturns(new ExtractedContent { MainContent = "body" });
        _vault.MemorizeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("store is read-only"));

        var result = Parse(await CreateTool().MemorizeWebPageAsync("https://example.com/", cancellationToken: TestContext.Current.CancellationToken));

        result.GetProperty("success").GetBoolean().Should().BeFalse();
        result.GetProperty("error").GetString().Should().Contain("store is read-only");
    }

    #endregion

    #region Registration

    [Fact]
    public void GetFluxRagTools_WithoutWebFlux_DoesNotOfferTheWebTool()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _vault);
        services.AddFluxRagTools();
        using var provider = services.BuildServiceProvider();

        provider.GetFluxRagTools().Select(t => t.UniqueName).Should().NotContain("func_memorize_web_page",
            "the tool extracts through WebFlux; without it registered the tool cannot be built");
    }

    [Fact]
    public void GetFluxRagTools_WithWebFlux_OffersTheWebTool()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _vault);
        services.AddScoped(_ => _extractor);
        services.AddFluxRagTools();
        using var provider = services.BuildServiceProvider();

        provider.GetFluxRagTools().Select(t => t.UniqueName).Should().Contain("func_memorize_web_page");
    }

    [Fact]
    public void GetFluxRagTools_ReturnsTheRegisteredTools()
    {
        // Before 0.8.0 each tool's Type object was passed where the factory expects the instance, so this returned nothing.
        var services = new ServiceCollection();
        services.AddScoped(_ => _vault);
        services.AddFluxRagTools();
        using var provider = services.BuildServiceProvider();

        provider.GetFluxRagTools().Select(t => t.UniqueName).Should().Contain(["func_search_knowledge_base", "func_memorize_document", "func_knowledge_base_status", "func_forget_document"]);
    }

    #endregion

    #region Formatting helpers

    [Fact]
    public void ExtractTitleFromUrl_UsesTheLastPathSegment_OrTheHost()
    {
        FluxIndexWebMemorizeTool.ExtractTitleFromUrl(new Uri("https://example.com/docs/getting-started.html")).Should().Be("getting started");
        FluxIndexWebMemorizeTool.ExtractTitleFromUrl(new Uri("https://example.com/")).Should().Be("example.com");
    }

    [Fact]
    public void CreateTempMarkdownFile_IsAMarkdownPathInTheTempDirectory()
    {
        var path = FluxIndexWebMemorizeTool.CreateTempMarkdownFile("https://example.com/page");
        path.Should().EndWith(".md").And.StartWith(Path.GetTempPath());
    }

    #endregion
}
