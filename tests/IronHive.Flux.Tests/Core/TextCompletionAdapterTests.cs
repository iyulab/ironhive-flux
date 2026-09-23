using Flux.Abstractions;
using AwesomeAssertions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Flux.Core.Adapters.TextCompletion;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IronHive.Flux.Tests.Core;

public class TextCompletionAdapterTests
{
    private readonly IMessageGenerator _mockGenerator;
    private readonly IOptions<IronHiveFluxCoreOptions> _options;

    public TextCompletionAdapterTests()
    {
        _mockGenerator = Substitute.For<IMessageGenerator>();
        _options = Options.Create(new IronHiveFluxCoreOptions
        {
            TextCompletionModelId = "gpt-4o",
            EmbeddingModelId = "text-embedding-3-small",
            EmbeddingDimension = 1536,
            MaxTokens = 8191
        });
    }

    private void SetupGeneratorResponse(string text)
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse
            {
                Message = Message.Assistant(text)
            });
    }

    #region FluxIndex Adapter

    public static TheoryData<string> AdapterPaths => ["fluxindex-text", "fluxindex-json", "webflux-text", "webflux-json"];

    private Task<string> CompleteVia(string path, TextCompletionOptions options)
    {
        ITextCompletionService fluxIndex = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);
        ITextCompletionService webFlux = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);
        var ct = TestContext.Current.CancellationToken;
        return path switch
        {
            "fluxindex-text" => fluxIndex.CompleteAsync("p", options, ct),
            "fluxindex-json" => fluxIndex.CompleteJsonAsync("p", options, ct),
            "webflux-text" => webFlux.CompleteAsync("p", options, ct),
            _ => webFlux.CompleteJsonAsync("p", options, ct),
        };
    }

    private void SetupTruncatedResponse() =>
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse { Message = Message.Assistant("{\"cut\": \"o"), DoneReason = MessageDoneReason.MaxTokens });

    [Theory]
    [MemberData(nameof(AdapterPaths))]
    public async Task ThrowOnTruncation_ReportsAResponseCutAtTheOutputBudget(string path)
    {
        SetupTruncatedResponse();

        var act = () => CompleteVia(path, new TextCompletionOptions { MaxTokens = 12, ThrowOnTruncation = true });

        (await act.Should().ThrowAsync<TextCompletionTruncatedException>()).Which.MaxTokens.Should().Be(12);
    }

    [Theory]
    [MemberData(nameof(AdapterPaths))]
    public async Task WithoutThrowOnTruncation_ACutResponseIsReturnedAsBefore(string path)
    {
        SetupTruncatedResponse();

        var act = () => CompleteVia(path, new TextCompletionOptions { MaxTokens = 12 });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ThrowOnTruncation_DoesNotFireOnAFinishedResponse()
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse { Message = Message.Assistant("done"), DoneReason = MessageDoneReason.EndTurn });

        var result = await CompleteVia("fluxindex-text", new TextCompletionOptions { ThrowOnTruncation = true });

        result.Should().Be("done");
    }

    [Fact]
    public async Task FluxIndex_CompleteAsync_ReturnsText()
    {
        // Arrange
        SetupGeneratorResponse("Completed text response");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.CompleteAsync("test prompt", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("Completed text response");
    }

    [Fact]
    public async Task FluxIndex_CompleteAsync_PassesOptionsToRequest()
    {
        // Arrange
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        await adapter.CompleteAsync("test", new TextCompletionOptions { MaxTokens = 100, Temperature = 0.5f }, TestContext.Current.CancellationToken);

        // Assert
        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r =>
                r.Model == "gpt-4o" &&
                r.MaxTokens == 100 &&
                r.Temperature == 0.5f),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FluxIndex_CompleteJsonAsync_ExtractsJson()
    {
        // Arrange
        SetupGeneratorResponse("```json\n{\"key\": \"value\"}\n```");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.CompleteJsonAsync("generate json", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public async Task FluxIndex_CompleteJsonAsync_HandlesRawJson()
    {
        // Arrange
        SetupGeneratorResponse("{\"name\": \"test\"}");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.CompleteJsonAsync("generate json", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("{\"name\": \"test\"}");
    }

    [Fact]
    public async Task FluxIndex_CompleteJsonAsync_HandlesJsonArray()
    {
        // Arrange
        SetupGeneratorResponse("```\n[1, 2, 3]\n```");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.CompleteJsonAsync("generate json array", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be("[1, 2, 3]");
    }

    [Fact]
    public async Task FluxIndex_CompleteJsonAsync_UsesLowTemperature()
    {
        // Arrange
        SetupGeneratorResponse("{}");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        await adapter.CompleteJsonAsync("test", cancellationToken: TestContext.Current.CancellationToken);

        // Assert — JSON completion should use 0.1 temperature
        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r => r.Temperature == 0.1f),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void FluxIndex_Constructor_NullGenerator_Throws()
    {
        var act = () => new IronHiveTextCompletionServiceForFluxIndex(null!, _options);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FluxIndex_Constructor_NullOptions_Throws()
    {
        var act = () => new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
