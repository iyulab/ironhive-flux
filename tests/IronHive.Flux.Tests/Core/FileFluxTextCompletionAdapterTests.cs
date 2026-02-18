using FileFlux;
using FileFlux.Core;
using FluentAssertions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Adapters.TextCompletion;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IronHive.Flux.Tests.Core;

public class FileFluxTextCompletionAdapterTests
{
    private readonly IMessageGenerator _mockGenerator;
    private readonly IOptions<IronHiveFluxCoreOptions> _options;

    public FileFluxTextCompletionAdapterTests()
    {
        _mockGenerator = Substitute.For<IMessageGenerator>();
        _options = Options.Create(new IronHiveFluxCoreOptions
        {
            TextCompletionModelId = "gpt-4o",
            DefaultTemperature = 0.7f,
            DefaultCompletionMaxTokens = 500
        });
    }

    private void SetupGeneratorResponse(string text, int totalTokens = 0)
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse
            {
                Id = "test-response",
                Message = new AssistantMessage
                {
                    Content = [new TextMessageContent { Value = text }]
                },
                TokenUsage = totalTokens > 0
                    ? new MessageTokenUsage { InputTokens = totalTokens / 2, OutputTokens = totalTokens - (totalTokens / 2) }
                    : null
            });
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullGenerator_Throws()
    {
        var act = () => new IronHiveTextCompletionServiceForFileFlux(null!, _options);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region ProviderInfo

    [Fact]
    public void ProviderInfo_ShouldReturnIronHive()
    {
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        adapter.ProviderInfo.Name.Should().Be("IronHive");
        adapter.ProviderInfo.Type.Should().Be(TextCompletionProviderType.OpenAI);
        adapter.ProviderInfo.SupportedModels.Should().Contain("gpt-4o");
        adapter.ProviderInfo.MaxContextLength.Should().Be(128000);
    }

    #endregion

    #region GenerateAsync

    [Fact]
    public async Task GenerateAsync_ShouldReturnText()
    {
        SetupGeneratorResponse("Generated text");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.GenerateAsync("test prompt");

        result.Should().Be("Generated text");
    }

    [Fact]
    public async Task GenerateAsync_ShouldUseDefaultTemperatureAndMaxTokens()
    {
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        await adapter.GenerateAsync("test");

        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r =>
                r.Temperature == 0.7f &&
                r.MaxTokens == 500),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region IsAvailableAsync

    [Fact]
    public async Task IsAvailableAsync_WhenGeneratorSucceeds_ReturnsTrue()
    {
        SetupGeneratorResponse("pong");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var available = await adapter.IsAvailableAsync();

        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenGeneratorThrows_ReturnsFalse()
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns<MessageResponse>(_ => throw new InvalidOperationException("service down"));
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var available = await adapter.IsAvailableAsync();

        available.Should().BeFalse();
    }

    #endregion

    #region AnalyzeStructureAsync

    [Fact]
    public async Task AnalyzeStructureAsync_ShouldReturnResult()
    {
        SetupGeneratorResponse("Sections: Introduction, Body, Conclusion", totalTokens: 50);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.AnalyzeStructureAsync("test document", DocumentType.Text);

        result.Should().NotBeNull();
        result.DocumentType.Should().Be(DocumentType.Text);
        result.RawResponse.Should().Contain("Sections");
        result.TokensUsed.Should().Be(50);
        result.Confidence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task AnalyzeStructureAsync_EmptyResponse_LowConfidence()
    {
        SetupGeneratorResponse("");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.AnalyzeStructureAsync("test", DocumentType.Pdf);

        result.Confidence.Should().BeLessThanOrEqualTo(0.3);
    }

    #endregion

    #region SummarizeContentAsync

    [Fact]
    public async Task SummarizeContentAsync_ShortResponse_HighConfidence()
    {
        SetupGeneratorResponse("Brief summary.", totalTokens: 10);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.SummarizeContentAsync("long content here", maxLength: 200);

        result.Summary.Should().Be("Brief summary.");
        result.OriginalLength.Should().Be(17);
        result.Confidence.Should().BeGreaterThanOrEqualTo(0.9);
    }

    [Fact]
    public async Task SummarizeContentAsync_LongResponse_TruncatesAndLowerConfidence()
    {
        var longText = new string('x', 300);
        SetupGeneratorResponse(longText);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.SummarizeContentAsync("content", maxLength: 200);

        result.Summary.Length.Should().BeLessThanOrEqualTo(200);
        result.Confidence.Should().Be(0.75);
    }

    [Fact]
    public async Task SummarizeContentAsync_EmptyResponse_LowConfidence()
    {
        SetupGeneratorResponse("   ");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.SummarizeContentAsync("content");

        result.Confidence.Should().Be(0.3);
    }

    #endregion

    #region ExtractMetadataAsync

    [Fact]
    public async Task ExtractMetadataAsync_WithValidJson_ExtractsFields()
    {
        var jsonResponse = """{"keywords": ["AI", "ML"], "language": "en", "categories": ["tech"]}""";
        SetupGeneratorResponse(jsonResponse, totalTokens: 30);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.ExtractMetadataAsync("test content", DocumentType.Text);

        result.Keywords.Should().Contain("AI");
        result.Keywords.Should().Contain("ML");
        result.Language.Should().Be("en");
        result.Categories.Should().Contain("tech");
        result.Confidence.Should().BeGreaterThan(0.8); // 3 fields → 0.5 + 3*0.15 = 0.95
    }

    [Fact]
    public async Task ExtractMetadataAsync_WithInvalidJson_LowConfidence()
    {
        SetupGeneratorResponse("no json here");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.ExtractMetadataAsync("test", DocumentType.Text);

        result.Confidence.Should().BeLessThanOrEqualTo(0.3);
    }

    [Fact]
    public async Task ExtractMetadataAsync_PartialJson_MediumConfidence()
    {
        var jsonResponse = """{"keywords": ["test"], "language": "ko"}""";
        SetupGeneratorResponse(jsonResponse);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.ExtractMetadataAsync("test", DocumentType.Text);

        result.Keywords.Should().Contain("test");
        result.Language.Should().Be("ko");
        result.Confidence.Should().Be(0.8); // 2 fields → 0.5 + 2*0.15
    }

    #endregion

    #region AssessQualityAsync

    [Fact]
    public async Task AssessQualityAsync_WithValidScores_ParsesCorrectly()
    {
        var jsonResponse = """{"confidence": 0.8, "completeness": 0.9, "consistency": 0.7}""";
        SetupGeneratorResponse(jsonResponse, totalTokens: 20);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.AssessQualityAsync("test content");

        result.ConfidenceScore.Should().Be(0.8);
        result.CompletenessScore.Should().Be(0.9);
        result.ConsistencyScore.Should().Be(0.7);
        result.TokensUsed.Should().Be(20);
    }

    [Fact]
    public async Task AssessQualityAsync_WithInvalidJson_FallsBackToDefaults()
    {
        SetupGeneratorResponse("Quality is good overall.");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.AssessQualityAsync("test content");

        // Fallback: all 0.7
        result.ConfidenceScore.Should().Be(0.7);
        result.CompletenessScore.Should().Be(0.7);
        result.ConsistencyScore.Should().Be(0.7);
    }

    [Fact]
    public async Task AssessQualityAsync_EmptyResponse_LowScores()
    {
        SetupGeneratorResponse("");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.AssessQualityAsync("test");

        result.ConfidenceScore.Should().Be(0.3);
        result.CompletenessScore.Should().Be(0.3);
        result.ConsistencyScore.Should().Be(0.3);
    }

    #endregion
}
