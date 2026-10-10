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
}
