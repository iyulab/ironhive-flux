using AwesomeAssertions;
using FluxFeed.Interfaces;
using IronHive.Flux.Rag.Options;
using IronHive.Flux.Rag.Tools;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Text.Json;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class FluxIndexWebMemorizeToolTests : IDisposable
{
    private readonly IVault _vault;
    private readonly FluxIndexWebMemorizeTool _tool;

    public FluxIndexWebMemorizeToolTests()
    {
        _vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        _tool = new FluxIndexWebMemorizeTool(_vault, options);
    }

    public void Dispose()
    {
        _tool.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullVault_ShouldThrow()
    {
        var options = Options.Create(new FluxRagToolsOptions());

        var act = () => new FluxIndexWebMemorizeTool(null!, options);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var vault = Substitute.For<IVault>();

        var act = () => new FluxIndexWebMemorizeTool(vault, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidArgs_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());

        using var tool = new FluxIndexWebMemorizeTool(vault, options);
        tool.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithHttpClient_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var httpClient = new HttpClient();

        using var tool = new FluxIndexWebMemorizeTool(vault, options, httpClient);
        tool.Should().NotBeNull();
    }

    #endregion

    #region URL Validation

    [Fact]
    public async Task MemorizeWebPageAsync_WithInvalidUrl_ShouldReturnError()
    {
        var resultJson = await _tool.MemorizeWebPageAsync("not-a-valid-url");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Invalid URL");
    }

    [Fact]
    public async Task MemorizeWebPageAsync_WithFtpUrl_ShouldReturnError()
    {
        var resultJson = await _tool.MemorizeWebPageAsync("ftp://example.com/file.txt");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Invalid URL");
    }

    [Fact]
    public async Task MemorizeWebPageAsync_WithRelativeUrl_ShouldReturnError()
    {
        var resultJson = await _tool.MemorizeWebPageAsync("/relative/path");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("error").GetString().Should().Contain("Invalid URL");
    }

    #endregion

    #region HTML Detection

    [Fact]
    public void IsHtmlContent_WithHtmlContentType_ShouldReturnTrue()
    {
        FluxIndexWebMemorizeTool.IsHtmlContent("<p>test</p>", "text/html").Should().BeTrue();
    }

    [Fact]
    public void IsHtmlContent_WithHtmlDoctype_ShouldReturnTrue()
    {
        FluxIndexWebMemorizeTool.IsHtmlContent("<!DOCTYPE html><html>", null).Should().BeTrue();
    }

    [Fact]
    public void IsHtmlContent_WithHtmlTag_ShouldReturnTrue()
    {
        FluxIndexWebMemorizeTool.IsHtmlContent("  <html><body>test</body></html>", null).Should().BeTrue();
    }

    [Fact]
    public void IsHtmlContent_WithPlainText_ShouldReturnFalse()
    {
        FluxIndexWebMemorizeTool.IsHtmlContent("Just plain text", "text/plain").Should().BeFalse();
    }

    [Fact]
    public void IsHtmlContent_WithJsonContent_ShouldReturnFalse()
    {
        FluxIndexWebMemorizeTool.IsHtmlContent("{\"key\": \"value\"}", "application/json").Should().BeFalse();
    }

    #endregion

    #region HTML Processing

    [Fact]
    public void RemoveHtmlBlocks_ShouldRemoveScriptTags()
    {
        var html = "before<script>alert('hi')</script>after";
        var result = FluxIndexWebMemorizeTool.RemoveHtmlBlocks(html, "script");
        result.Should().Be("beforeafter");
    }

    [Fact]
    public void RemoveHtmlBlocks_ShouldRemoveStyleTags()
    {
        var html = "text<style>.cls{color:red}</style>more";
        var result = FluxIndexWebMemorizeTool.RemoveHtmlBlocks(html, "style");
        result.Should().Be("textmore");
    }

    [Fact]
    public void RemoveHtmlBlocks_ShouldRemoveMultipleBlocks()
    {
        var html = "a<script>1</script>b<script>2</script>c";
        var result = FluxIndexWebMemorizeTool.RemoveHtmlBlocks(html, "script");
        result.Should().Be("abc");
    }

    [Fact]
    public void ExtractHtmlTitle_ShouldExtractTitle()
    {
        var html = "<html><head><title>Test Page Title</title></head></html>";
        var result = FluxIndexWebMemorizeTool.ExtractHtmlTitle(html);
        result.Should().Be("Test Page Title");
    }

    [Fact]
    public void ExtractHtmlTitle_WithNoTitle_ShouldReturnNull()
    {
        var html = "<html><head></head></html>";
        var result = FluxIndexWebMemorizeTool.ExtractHtmlTitle(html);
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractHtmlTitle_WithEmptyTitle_ShouldReturnNull()
    {
        var html = "<html><head><title>  </title></head></html>";
        var result = FluxIndexWebMemorizeTool.ExtractHtmlTitle(html);
        result.Should().BeNull();
    }

    [Fact]
    public void StripHtmlTags_ShouldRemoveTags()
    {
        var html = "<p>Hello <b>World</b></p>";
        var result = FluxIndexWebMemorizeTool.StripHtmlTags(html);
        result.Should().Contain("Hello").And.Contain("World");
        result.Should().NotContain("<p>").And.NotContain("<b>");
    }

    [Fact]
    public void DecodeHtmlEntities_ShouldDecodeCommonEntities()
    {
        var text = "A &amp; B &lt; C &gt; D &quot;E&quot; &#39;F&#39;";
        var result = FluxIndexWebMemorizeTool.DecodeHtmlEntities(text);
        result.Should().Be("A & B < C > D \"E\" 'F'");
    }

    [Fact]
    public void NormalizeWhitespace_ShouldCollapseSpaces()
    {
        var text = "  Hello    World  \n\n  Test  ";
        var result = FluxIndexWebMemorizeTool.NormalizeWhitespace(text);
        result.Should().Contain("Hello World");
        result.Should().Contain("Test");
    }

    #endregion

    #region Markdown Conversion

    [Fact]
    public void ConvertHtmlToBasicMarkdown_ShouldProduceFrontmatter()
    {
        var html = "<html><head><title>My Page</title></head><body><p>Content here</p></body></html>";
        var result = FluxIndexWebMemorizeTool.ConvertHtmlToBasicMarkdown(html, "https://example.com/page", null);

        result.Should().Contain("source: https://example.com/page");
        result.Should().Contain("title: My Page");
        result.Should().Contain("# My Page");
        result.Should().Contain("Content here");
    }

    [Fact]
    public void ConvertHtmlToBasicMarkdown_WithExplicitTitle_ShouldUseIt()
    {
        var html = "<html><head><title>HTML Title</title></head><body><p>Text</p></body></html>";
        var result = FluxIndexWebMemorizeTool.ConvertHtmlToBasicMarkdown(html, "https://example.com", "Custom Title");

        result.Should().Contain("title: Custom Title");
        result.Should().Contain("# Custom Title");
    }

    [Fact]
    public void ConvertHtmlToBasicMarkdown_ShouldRemoveScriptsAndStyles()
    {
        var html = "<html><body><script>alert('xss')</script><style>.bad{}</style><p>Safe content</p></body></html>";
        var result = FluxIndexWebMemorizeTool.ConvertHtmlToBasicMarkdown(html, "https://example.com", "Test");

        result.Should().Contain("Safe content");
        result.Should().NotContain("alert");
        result.Should().NotContain(".bad");
    }

    [Fact]
    public void FormatAsMarkdown_ShouldProduceFrontmatter()
    {
        var result = FluxIndexWebMemorizeTool.FormatAsMarkdown("Some content", "https://example.com/page", "Page Title");

        result.Should().Contain("---");
        result.Should().Contain("source: https://example.com/page");
        result.Should().Contain("title: Page Title");
        result.Should().Contain("# Page Title");
        result.Should().Contain("Some content");
    }

    #endregion

    #region Title Extraction from URL

    [Fact]
    public void ExtractTitleFromUrl_WithPathSegment_ShouldExtractTitle()
    {
        var uri = new Uri("https://example.com/blog/my-first-post");
        var title = FluxIndexWebMemorizeTool.ExtractTitleFromUrl(uri);
        title.Should().Be("my first post");
    }

    [Fact]
    public void ExtractTitleFromUrl_WithExtension_ShouldRemoveExtension()
    {
        var uri = new Uri("https://example.com/docs/guide.html");
        var title = FluxIndexWebMemorizeTool.ExtractTitleFromUrl(uri);
        title.Should().Be("guide");
    }

    [Fact]
    public void ExtractTitleFromUrl_WithRootPath_ShouldReturnHost()
    {
        var uri = new Uri("https://example.com/");
        var title = FluxIndexWebMemorizeTool.ExtractTitleFromUrl(uri);
        title.Should().Be("example.com");
    }

    [Fact]
    public void ExtractTitleFromUrl_WithUnderscores_ShouldConvertToSpaces()
    {
        var uri = new Uri("https://example.com/some_document_title");
        var title = FluxIndexWebMemorizeTool.ExtractTitleFromUrl(uri);
        title.Should().Be("some document title");
    }

    #endregion

    #region Temp File Path

    [Fact]
    public void CreateTempMarkdownFile_ShouldReturnMdExtension()
    {
        var path = FluxIndexWebMemorizeTool.CreateTempMarkdownFile("https://example.com");
        path.Should().EndWith(".md");
    }

    [Fact]
    public void CreateTempMarkdownFile_ShouldBeInTempDir()
    {
        var path = FluxIndexWebMemorizeTool.CreateTempMarkdownFile("https://example.com");
        path.Should().StartWith(Path.GetTempPath());
    }

    [Fact]
    public void CreateTempMarkdownFile_DifferentUrls_ShouldProduceDifferentPaths()
    {
        var path1 = FluxIndexWebMemorizeTool.CreateTempMarkdownFile("https://example.com/page1");
        var path2 = FluxIndexWebMemorizeTool.CreateTempMarkdownFile("https://example.com/page2");
        // Different URLs with different hash should produce different file names (path prefix matches)
        Path.GetFileName(path1).Should().NotBe(Path.GetFileName(path2));
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_MultipleDispose_ShouldNotThrow()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var tool = new FluxIndexWebMemorizeTool(vault, options);

        tool.Dispose();
        var act = () => tool.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithExternalHttpClient_ShouldNotDisposeIt()
    {
        var vault = Substitute.For<IVault>();
        var options = Options.Create(new FluxRagToolsOptions());
        var httpClient = new HttpClient();

        using var tool = new FluxIndexWebMemorizeTool(vault, options, httpClient);
        tool.Dispose();

        // External HttpClient should still be usable (not disposed)
        var act = () => httpClient.BaseAddress = new Uri("https://example.com");
        act.Should().NotThrow();

        httpClient.Dispose();
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task MemorizeWebPageAsync_VaultThrows_ShouldReturnError()
    {
        // We can't easily test the full flow without a real HTTP server,
        // but we can test that the tool handles errors gracefully.
        // The URL validation tests above cover the early exit paths.

        // Test: HTTP download failure (invalid host)
        var resultJson = await _tool.MemorizeWebPageAsync("https://this-domain-does-not-exist-12345.invalid/page");
        var result = JsonDocument.Parse(resultJson);

        result.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        result.RootElement.GetProperty("url").GetString().Should().Be("https://this-domain-does-not-exist-12345.invalid/page");
        result.RootElement.TryGetProperty("error", out _).Should().BeTrue();
    }

    #endregion
}
