using FluentAssertions;
using IronHive.Flux.Rag.Context;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using TokenMeter;
using Xunit;

namespace IronHive.Flux.Tests.Rag;

public class RagContextBuilderTests
{
    private static IOptions<FluxRagToolsOptions> CreateOptions(FluxRagToolsOptions? opts = null)
    {
        return Options.Create(opts ?? new FluxRagToolsOptions());
    }

    private static RagSearchResult CreateResult(string id, string content, float score, string? title = null)
    {
        return new RagSearchResult
        {
            DocumentId = id,
            Content = content,
            Score = score,
            Title = title
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrow()
    {
        var act = () => new RagContextBuilder(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldNotThrow()
    {
        var builder = new RagContextBuilder(CreateOptions());
        builder.Should().NotBeNull();
    }

    #endregion

    #region BuildContext - Score Filtering

    [Fact]
    public void BuildContext_ShouldFilterBelowMinScore()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "high score", 0.9f),
            CreateResult("2", "low score", 0.3f),
            CreateResult("3", "medium score", 0.6f)
        };

        var context = builder.BuildContext(results);

        context.Sources.Should().HaveCount(2);
        context.Sources.Should().Contain(r => r.DocumentId == "1");
        context.Sources.Should().Contain(r => r.DocumentId == "3");
        context.Sources.Should().NotContain(r => r.DocumentId == "2");
    }

    [Fact]
    public void BuildContext_WithCustomMinScore_ShouldUseCustomThreshold()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "content", 0.9f),
            CreateResult("2", "content", 0.7f),
            CreateResult("3", "content", 0.6f)
        };
        var options = new RagContextOptions { Query = "test", MinScore = 0.8f };

        var context = builder.BuildContext(results, options);

        context.Sources.Should().HaveCount(1);
        context.Sources[0].DocumentId.Should().Be("1");
    }

    [Fact]
    public void BuildContext_WithOptionsMinScore_ShouldOverrideDefault()
    {
        var opts = new FluxRagToolsOptions { DefaultMinScore = 0.9f };
        var builder = new RagContextBuilder(CreateOptions(opts));
        var results = new[]
        {
            CreateResult("1", "content", 0.85f),
            CreateResult("2", "content", 0.95f)
        };

        var context = builder.BuildContext(results);

        context.Sources.Should().HaveCount(1);
        context.Sources[0].DocumentId.Should().Be("2");
    }

    #endregion

    #region BuildContext - Ordering

    [Fact]
    public void BuildContext_ShouldOrderByScoreDescending()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "content a", 0.6f),
            CreateResult("2", "content b", 0.9f),
            CreateResult("3", "content c", 0.7f)
        };

        var context = builder.BuildContext(results);

        context.Sources[0].DocumentId.Should().Be("2");
        context.Sources[1].DocumentId.Should().Be("3");
        context.Sources[2].DocumentId.Should().Be("1");
    }

    #endregion

    #region BuildContext - Token Budget

    [Fact]
    public void BuildContext_ShouldRespectTokenBudget()
    {
        var opts = new FluxRagToolsOptions { MaxContextTokens = 10 };
        var builder = new RagContextBuilder(CreateOptions(opts));
        var results = new[]
        {
            CreateResult("1", "a]short text", 0.9f),
            CreateResult("2", new string('x', 200), 0.8f)
        };

        var context = builder.BuildContext(results);

        // First result should always be included even if over budget
        context.Sources.Should().ContainSingle(r => r.DocumentId == "1");
    }

    [Fact]
    public void BuildContext_ShouldAlwaysIncludeFirstResult()
    {
        var opts = new FluxRagToolsOptions { MaxContextTokens = 1 };
        var builder = new RagContextBuilder(CreateOptions(opts));
        var results = new[]
        {
            CreateResult("1", new string('x', 100), 0.9f)
        };

        var context = builder.BuildContext(results);

        context.Sources.Should().HaveCount(1);
    }

    #endregion

    #region BuildContext - Context Text

    [Fact]
    public void BuildContext_WithNoResults_ShouldReturnNotFoundMessage()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = Array.Empty<RagSearchResult>();

        var context = builder.BuildContext(results);

        context.ContextText.Should().Contain("관련 정보를 찾을 수 없습니다.");
        context.Sources.Should().BeEmpty();
    }

    [Fact]
    public void BuildContext_WithTitle_ShouldIncludeTitleInContext()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "content here", 0.9f, title: "My Document")
        };

        var context = builder.BuildContext(results);

        context.ContextText.Should().Contain("[Source 1: My Document]");
        context.ContextText.Should().Contain("content here");
    }

    [Fact]
    public void BuildContext_WithoutTitle_ShouldUseSourceNumber()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "content here", 0.9f)
        };

        var context = builder.BuildContext(results);

        context.ContextText.Should().Contain("[Source 1]");
    }

    [Fact]
    public void BuildContext_MultipleResults_ShouldSeparateWithChunkSeparator()
    {
        var opts = new FluxRagToolsOptions { ChunkSeparator = "---SEP---" };
        var builder = new RagContextBuilder(CreateOptions(opts));
        var results = new[]
        {
            CreateResult("1", "first content", 0.9f),
            CreateResult("2", "second content", 0.8f)
        };

        var context = builder.BuildContext(results);

        context.ContextText.Should().Contain("---SEP---");
    }

    #endregion

    #region BuildContext - Metrics

    [Fact]
    public void BuildContext_ShouldCalculateAverageRelevance()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "content", 0.9f),
            CreateResult("2", "content", 0.7f)
        };

        var context = builder.BuildContext(results);

        context.AverageRelevance.Should().BeApproximately(0.8f, 0.01f);
    }

    [Fact]
    public void BuildContext_WithNoFilteredResults_ShouldHaveZeroRelevance()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "content", 0.1f) // Below default min score of 0.5
        };

        var context = builder.BuildContext(results);

        context.AverageRelevance.Should().Be(0);
    }

    [Fact]
    public void BuildContext_ShouldSetSearchStrategy()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "content", 0.9f)
        };
        var options = new RagContextOptions { Query = "test", Strategy = "vector" };

        var context = builder.BuildContext(results, options);

        context.SearchStrategy.Should().Be("vector");
    }

    #endregion

    #region TokenMeter Integration

    [Fact]
    public void BuildContext_WithTokenCounter_ShouldUseAccurateTokenCounting()
    {
        var mockCounter = Substitute.For<ITokenCounter>();
        mockCounter.CountTokens(Arg.Any<string>()).Returns(10);

        var builder = new RagContextBuilder(CreateOptions(), tokenCounter: mockCounter);
        var results = new[]
        {
            CreateResult("1", "some content", 0.9f)
        };

        var context = builder.BuildContext(results);

        context.TokenCount.Should().Be(10);
        mockCounter.Received(1).CountTokens("some content");
    }

    [Fact]
    public void BuildContext_WithoutTokenCounter_ShouldUseFallbackHeuristic()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "hello world", 0.9f)
        };

        var context = builder.BuildContext(results);

        // Fallback heuristic: English ~0.25 tokens/char → "hello world" (11 chars) ≈ 2-3 tokens
        context.TokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void BuildContext_WithTokenCounter_ShouldRespectTokenBudget()
    {
        var mockCounter = Substitute.For<ITokenCounter>();
        mockCounter.CountTokens("first").Returns(5);
        mockCounter.CountTokens("second").Returns(100);

        var opts = new FluxRagToolsOptions { MaxContextTokens = 20 };
        var builder = new RagContextBuilder(CreateOptions(opts), tokenCounter: mockCounter);
        var results = new[]
        {
            CreateResult("1", "first", 0.9f),
            CreateResult("2", "second", 0.8f)
        };

        var context = builder.BuildContext(results);

        // First fits (5 <= 20), second doesn't (5 + 100 > 20)
        context.Sources.Should().HaveCount(1);
        context.Sources[0].DocumentId.Should().Be("1");
    }

    #endregion

    #region BuildContextAsync

    [Fact]
    public async Task BuildContextAsync_ShouldCallSearchAndBuildContext()
    {
        var builder = new RagContextBuilder(CreateOptions());
        var results = new[]
        {
            CreateResult("1", "async content", 0.9f)
        };

        var context = await builder.BuildContextAsync(() => Task.FromResult<IEnumerable<RagSearchResult>>(results));

        context.Sources.Should().HaveCount(1);
        context.Sources[0].DocumentId.Should().Be("1");
    }

    [Fact]
    public async Task BuildContextAsync_WithCancellation_ShouldThrow()
    {
        var builder = new RagContextBuilder(CreateOptions());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => builder.BuildContextAsync(
            () => Task.FromResult<IEnumerable<RagSearchResult>>(Array.Empty<RagSearchResult>()),
            cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    #endregion
}
