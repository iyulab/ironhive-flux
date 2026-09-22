using FileFlux;
using FileFlux.Core;
using AwesomeAssertions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
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
                Message = Message.Assistant(text),
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
        adapter.ProviderInfo.Type.Should().Be(DocumentAnalysisProviderType.Custom);
        adapter.ProviderInfo.SupportedModels.Should().Contain("gpt-4o");
        // 0 = not declared — the adapter does not know the model's context window.
        adapter.ProviderInfo.MaxContextLength.Should().Be(0);
    }

    #endregion

    #region GenerateAsync

    [Fact]
    public async Task GenerateAsync_WithSettings_SendsCallersBudgetAndTemperature()
    {
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        await adapter.GenerateAsync("p", new GenerationSettings(Temperature: 0.1, MaxTokens: 3000), TestContext.Current.CancellationToken);

        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r => r.MaxTokens == 3000 && r.Temperature == 0.1f),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateAsync_WithUnsetSettings_FallsBackToConfiguredDefaults()
    {
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        await adapter.GenerateAsync("p", GenerationSettings.Default, TestContext.Current.CancellationToken);

        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r => r.MaxTokens == 500 && r.Temperature == 0.7f),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GenerateAsync_StoppedAtOutputLimit_ThrowsTruncated(bool withSettings)
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse { Message = Message.Assistant("cut o"), DoneReason = MessageDoneReason.MaxTokens });
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        Func<Task<string>> act = withSettings
            ? () => adapter.GenerateAsync("p", new GenerationSettings(MaxTokens: 10), TestContext.Current.CancellationToken)
            : () => adapter.GenerateAsync("p", TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<GenerationTruncatedException>()).Which.MaxTokens.Should().Be(withSettings ? 10 : 500);
    }

    [Fact]
    public async Task Refining_through_the_adapter_keeps_the_document_when_the_model_is_cut_off()
    {
        var document = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"Paragraph {i}: the quarterly report covers revenue, cost and headcount in detail."));
        MessageGenerationRequest? sent = null;
        _mockGenerator
            .GenerateMessageAsync(Arg.Do<MessageGenerationRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse { Message = Message.Assistant(document[..(document.Length / 3)]), DoneReason = MessageDoneReason.MaxTokens });
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);
        var noiseOnly = new LlmRefineOptions
        {
            RestoreSentences = false, CorrectOcrErrors = false, RestructureSections = false, MergeDuplicates = false, RemoveNoise = true,
        };

        var result = await new FileFlux.Infrastructure.LlmRefiner(adapter).RefineAsync(new RefinedContent { Text = document }, noiseOnly, TestContext.Current.CancellationToken);

        result.Text.Should().Be(document.Trim(), "a cut-off rewrite is lost content, not removed noise");
        result.Info.Warnings.Should().ContainSingle().Which.Should().Contain("truncated");
        sent!.MaxTokens.Should().BeGreaterThan(500, "the refiner's text-sized budget reaches the model instead of the adapter default");
    }

    [Fact]
    public async Task GenerateAsync_EndedNormally_ReturnsText()
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse { Message = Message.Assistant("whole"), DoneReason = MessageDoneReason.EndTurn });
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        (await adapter.GenerateAsync("p", new GenerationSettings(MaxTokens: 10), TestContext.Current.CancellationToken)).Should().Be("whole");
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnText()
    {
        SetupGeneratorResponse("Generated text");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.GenerateAsync("test prompt", TestContext.Current.CancellationToken);

        result.Should().Be("Generated text");
    }

    [Fact]
    public async Task GenerateAsync_ShouldUseDefaultTemperatureAndMaxTokens()
    {
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        await adapter.GenerateAsync("test", TestContext.Current.CancellationToken);

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

        var available = await adapter.IsAvailableAsync(TestContext.Current.CancellationToken);

        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenGeneratorThrows_ReturnsFalse()
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns<MessageResponse>(_ => throw new InvalidOperationException("service down"));
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var available = await adapter.IsAvailableAsync(TestContext.Current.CancellationToken);

        available.Should().BeFalse();
    }

    #endregion

    #region AnalyzeStructureAsync

    [Fact]
    public async Task AnalyzeStructureAsync_ShouldReturnResult()
    {
        SetupGeneratorResponse("Sections: Introduction, Body, Conclusion", totalTokens: 50);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.AnalyzeStructureAsync("test document", DocumentType.Text, TestContext.Current.CancellationToken);

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

        var result = await adapter.AnalyzeStructureAsync("test", DocumentType.Pdf, TestContext.Current.CancellationToken);

        result.Confidence.Should().BeLessThanOrEqualTo(0.3);
    }

    #endregion

    #region SummarizeContentAsync

    [Fact]
    public async Task SummarizeContentAsync_ShortResponse_HighConfidence()
    {
        SetupGeneratorResponse("Brief summary.", totalTokens: 10);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.SummarizeContentAsync("long content here", maxLength: 200, cancellationToken: TestContext.Current.CancellationToken);

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

        var result = await adapter.SummarizeContentAsync("content", maxLength: 200, cancellationToken: TestContext.Current.CancellationToken);

        result.Summary.Length.Should().BeLessThanOrEqualTo(200);
        result.Confidence.Should().Be(0.75);
    }

    [Fact]
    public async Task SummarizeContentAsync_EmptyResponse_LowConfidence()
    {
        SetupGeneratorResponse("   ");
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.SummarizeContentAsync("content", cancellationToken: TestContext.Current.CancellationToken);

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

        var result = await adapter.ExtractMetadataAsync("test content", DocumentType.Text, TestContext.Current.CancellationToken);

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

        var result = await adapter.ExtractMetadataAsync("test", DocumentType.Text, TestContext.Current.CancellationToken);

        result.Confidence.Should().BeLessThanOrEqualTo(0.3);
    }

    [Fact]
    public async Task ExtractMetadataAsync_PartialJson_MediumConfidence()
    {
        var jsonResponse = """{"keywords": ["test"], "language": "ko"}""";
        SetupGeneratorResponse(jsonResponse);
        var adapter = new IronHiveTextCompletionServiceForFileFlux(_mockGenerator, _options);

        var result = await adapter.ExtractMetadataAsync("test", DocumentType.Text, TestContext.Current.CancellationToken);

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

        var result = await adapter.AssessQualityAsync("test content", TestContext.Current.CancellationToken);

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

        var result = await adapter.AssessQualityAsync("test content", TestContext.Current.CancellationToken);

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

        var result = await adapter.AssessQualityAsync("test", TestContext.Current.CancellationToken);

        result.ConfidenceScore.Should().Be(0.3);
        result.CompletenessScore.Should().Be(0.3);
        result.ConsistencyScore.Should().Be(0.3);
    }

    #endregion
}
