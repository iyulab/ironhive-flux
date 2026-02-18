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
                Id = "test-response",
                Message = new AssistantMessage
                {
                    Content = [new TextMessageContent { Value = text }]
                }
            });
    }

    #region FluxIndex Adapter

    [Fact]
    public async Task FluxIndex_GenerateCompletionAsync_ReturnsText()
    {
        // Arrange
        SetupGeneratorResponse("Completed text response");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.GenerateCompletionAsync("test prompt");

        // Assert
        result.Should().Be("Completed text response");
    }

    [Fact]
    public async Task FluxIndex_GenerateCompletionAsync_PassesParametersToRequest()
    {
        // Arrange
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        await adapter.GenerateCompletionAsync("test", maxTokens: 100, temperature: 0.5f);

        // Assert
        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r =>
                r.Model == "gpt-4o" &&
                r.MaxTokens == 100 &&
                r.Temperature == 0.5f),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FluxIndex_GenerateJsonCompletionAsync_ExtractsJson()
    {
        // Arrange
        SetupGeneratorResponse("```json\n{\"key\": \"value\"}\n```");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.GenerateJsonCompletionAsync("generate json");

        // Assert
        result.Should().Be("{\"key\": \"value\"}");
    }

    [Fact]
    public async Task FluxIndex_GenerateJsonCompletionAsync_HandlesRawJson()
    {
        // Arrange
        SetupGeneratorResponse("{\"name\": \"test\"}");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.GenerateJsonCompletionAsync("generate json");

        // Assert
        result.Should().Be("{\"name\": \"test\"}");
    }

    [Fact]
    public async Task FluxIndex_GenerateJsonCompletionAsync_HandlesJsonArray()
    {
        // Arrange
        SetupGeneratorResponse("```\n[1, 2, 3]\n```");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        var result = await adapter.GenerateJsonCompletionAsync("generate json array");

        // Assert
        result.Should().Be("[1, 2, 3]");
    }

    [Fact]
    public async Task FluxIndex_GenerateJsonCompletionAsync_UsesLowTemperature()
    {
        // Arrange
        SetupGeneratorResponse("{}");
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        // Act
        await adapter.GenerateJsonCompletionAsync("test");

        // Assert — JSON completion should use 0.1 temperature
        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r => r.Temperature == 0.1f),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void FluxIndex_CountTokens_ReturnsLengthDividedByFour()
    {
        var adapter = new IronHiveTextCompletionServiceForFluxIndex(_mockGenerator, _options);

        adapter.CountTokens("12345678").Should().Be(2); // 8 / 4 = 2
        adapter.CountTokens("Hello").Should().Be(1); // 5 / 4 = 1 (integer division)
        adapter.CountTokens("").Should().Be(0);
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
