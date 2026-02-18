using FluentAssertions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Adapters.TextCompletion;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using WebFlux.Core.Options;
using Xunit;

namespace IronHive.Flux.Tests.Core;

public class WebFluxTextCompletionAdapterTests
{
    private readonly IMessageGenerator _mockGenerator;
    private readonly IOptions<IronHiveFluxCoreOptions> _options;

    public WebFluxTextCompletionAdapterTests()
    {
        _mockGenerator = Substitute.For<IMessageGenerator>();
        _options = Options.Create(new IronHiveFluxCoreOptions
        {
            TextCompletionModelId = "gpt-4o",
            DefaultTemperature = 0.7f,
            DefaultCompletionMaxTokens = 500
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

    private void SetupStreamingResponse(params string[] chunks)
    {
        _mockGenerator
            .GenerateStreamingMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(chunks));
    }

    private static async IAsyncEnumerable<StreamingMessageResponse> ToAsyncEnumerable(string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            yield return new StreamingContentDeltaResponse
            {
                Index = 0,
                Delta = new TextDeltaContent { Value = chunk }
            };
        }
        await Task.CompletedTask;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullGenerator_Throws()
    {
        var act = () => new IronHiveTextCompletionServiceForWebFlux(null!, _options);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region CompleteAsync

    [Fact]
    public async Task CompleteAsync_ShouldReturnText()
    {
        SetupGeneratorResponse("Completed text");
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var result = await adapter.CompleteAsync("test prompt");

        result.Should().Be("Completed text");
    }

    [Fact]
    public async Task CompleteAsync_ShouldUseDefaultOptions()
    {
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        await adapter.CompleteAsync("test");

        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r =>
                r.Model == "gpt-4o" &&
                r.Temperature == 0.7f &&
                r.MaxTokens == 500),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_WithCustomOptions_OverridesDefaults()
    {
        SetupGeneratorResponse("ok");
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var customOptions = new TextCompletionOptions
        {
            Temperature = 0.3,
            MaxTokens = 200
        };
        await adapter.CompleteAsync("test", customOptions);

        await _mockGenerator.Received(1).GenerateMessageAsync(
            Arg.Is<MessageGenerationRequest>(r =>
                r.Temperature == 0.3f &&
                r.MaxTokens == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteAsync_MultipleTextContents_JoinsThem()
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse
            {
                Id = "test",
                Message = new AssistantMessage
                {
                    Content =
                    [
                        new TextMessageContent { Value = "Hello " },
                        new TextMessageContent { Value = "World" }
                    ]
                }
            });
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var result = await adapter.CompleteAsync("test");

        result.Should().Be("Hello World");
    }

    [Fact]
    public async Task CompleteAsync_EmptyContent_ReturnsEmpty()
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse
            {
                Id = "test",
                Message = new AssistantMessage { Content = [] }
            });
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var result = await adapter.CompleteAsync("test");

        result.Should().BeEmpty();
    }

    #endregion

    #region CompleteStreamAsync

    [Fact]
    public async Task CompleteStreamAsync_ShouldYieldChunks()
    {
        SetupStreamingResponse("Hello", " ", "World");
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var chunks = new List<string>();
        await foreach (var chunk in adapter.CompleteStreamAsync("test"))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(3);
        chunks[0].Should().Be("Hello");
        chunks[1].Should().Be(" ");
        chunks[2].Should().Be("World");
    }

    [Fact]
    public async Task CompleteStreamAsync_EmptyStream_YieldsNothing()
    {
        _mockGenerator
            .GenerateStreamingMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ToEmptyAsyncEnumerable());
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var chunks = new List<string>();
        await foreach (var chunk in adapter.CompleteStreamAsync("test"))
        {
            chunks.Add(chunk);
        }

        chunks.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteStreamAsync_NonTextDelta_IsFiltered()
    {
        _mockGenerator
            .GenerateStreamingMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ToMixedAsyncEnumerable());
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var chunks = new List<string>();
        await foreach (var chunk in adapter.CompleteStreamAsync("test"))
        {
            chunks.Add(chunk);
        }

        // Only TextDeltaContent should be yielded, not ToolDeltaContent or other types
        chunks.Should().HaveCount(1);
        chunks[0].Should().Be("text chunk");
    }

    [Fact]
    public async Task CompleteStreamAsync_NullValue_YieldsEmptyString()
    {
        _mockGenerator
            .GenerateStreamingMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(ToNullValueAsyncEnumerable());
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var chunks = new List<string>();
        await foreach (var chunk in adapter.CompleteStreamAsync("test"))
        {
            chunks.Add(chunk);
        }

        chunks.Should().HaveCount(1);
        chunks[0].Should().BeEmpty();
    }

    #endregion

    #region CompleteBatchAsync

    [Fact]
    public async Task CompleteBatchAsync_MultiplePrompts_ReturnsAllResults()
    {
        var callCount = 0;
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                return new MessageResponse
                {
                    Id = $"response-{callCount}",
                    Message = new AssistantMessage
                    {
                        Content = [new TextMessageContent { Value = $"result-{callCount}" }]
                    }
                };
            });
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var results = await adapter.CompleteBatchAsync(["prompt1", "prompt2", "prompt3"]);

        results.Should().HaveCount(3);
        results[0].Should().Be("result-1");
        results[1].Should().Be("result-2");
        results[2].Should().Be("result-3");
    }

    [Fact]
    public async Task CompleteBatchAsync_EmptyPrompts_ReturnsEmptyList()
    {
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var results = await adapter.CompleteBatchAsync([]);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task CompleteBatchAsync_SinglePrompt_ReturnsSingleResult()
    {
        SetupGeneratorResponse("single result");
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var results = await adapter.CompleteBatchAsync(["test"]);

        results.Should().HaveCount(1);
        results[0].Should().Be("single result");
    }

    #endregion

    #region IsAvailableAsync

    [Fact]
    public async Task IsAvailableAsync_WhenGeneratorSucceeds_ReturnsTrue()
    {
        SetupGeneratorResponse("pong");
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var available = await adapter.IsAvailableAsync();

        available.Should().BeTrue();
    }

    [Fact]
    public async Task IsAvailableAsync_WhenGeneratorThrows_ReturnsFalse()
    {
        _mockGenerator
            .GenerateMessageAsync(Arg.Any<MessageGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns<MessageResponse>(_ => throw new InvalidOperationException("service down"));
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var available = await adapter.IsAvailableAsync();

        available.Should().BeFalse();
    }

    #endregion

    #region GetHealthInfo

    [Fact]
    public void GetHealthInfo_ReturnsCorrectServiceName()
    {
        var adapter = new IronHiveTextCompletionServiceForWebFlux(_mockGenerator, _options);

        var healthInfo = adapter.GetHealthInfo();

        healthInfo.ServiceName.Should().Be("IronHive TextCompletionService for WebFlux");
    }

    #endregion

    #region Helpers

    private static async IAsyncEnumerable<StreamingMessageResponse> ToEmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<StreamingMessageResponse> ToMixedAsyncEnumerable()
    {
        yield return new StreamingContentDeltaResponse
        {
            Index = 0,
            Delta = new TextDeltaContent { Value = "text chunk" }
        };
        yield return new StreamingContentDeltaResponse
        {
            Index = 1,
            Delta = new ToolDeltaContent { Input = "tool input" }
        };
        yield return new StreamingContentCompletedResponse { Index = 0 };
        await Task.CompletedTask;
    }

    private static async IAsyncEnumerable<StreamingMessageResponse> ToNullValueAsyncEnumerable()
    {
        yield return new StreamingContentDeltaResponse
        {
            Index = 0,
            Delta = new TextDeltaContent { Value = null! }
        };
        await Task.CompletedTask;
    }

    #endregion
}
