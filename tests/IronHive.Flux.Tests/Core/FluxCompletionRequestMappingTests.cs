using AwesomeAssertions;
using Flux.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Flux.Core.Adapters.TextCompletion;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IronHive.Flux.Tests.Core;

/// <summary>
/// Both <see cref="ITextCompletionService"/> adapters carry every option IronHive can express. Before, they sent only
/// temperature and output budget (system prompt, top-p, stop sequences, response format and schema were dropped), and
/// the WebFlux adapter's <c>CompleteJsonAsync</c> fell through to the interface default — a plain completion returned
/// as it came, code fence included.
/// </summary>
public class FluxCompletionRequestMappingTests
{
    private readonly IMessageGenerator _generator = Substitute.For<IMessageGenerator>();
    private readonly IOptions<IronHiveFluxCoreOptions> _options = Options.Create(new IronHiveFluxCoreOptions
    {
        TextCompletionModelId = "m", DefaultTemperature = 0.7f, DefaultCompletionMaxTokens = 500,
    });

    private MessageGenerationRequest? _sent;

    private void Respond(string text)
        => _generator.GenerateMessageAsync(Arg.Do<MessageGenerationRequest>(r => _sent = r), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse { Message = Message.Assistant(text) });

    public static TheoryData<string> Adapters => new() { "webflux", "fluxindex" };

    private ITextCompletionService Create(string which) => which == "webflux"
        ? new IronHiveTextCompletionServiceForWebFlux(_generator, _options)
        : new IronHiveTextCompletionServiceForFluxIndex(_generator, _options);

    [Theory]
    [MemberData(nameof(Adapters))]
    public async Task Every_expressible_option_reaches_the_request(string which)
    {
        Respond("ok");
        var options = new TextCompletionOptions
        {
            Temperature = 0.2f, MaxTokens = 900, TopP = 0.8f, StopSequences = ["END"], SystemPrompt = "be brief",
            ResponseSchema = """{"type":"object","properties":{"a":{"type":"string"}}}""",
        };

        await Create(which).CompleteAsync("p", options, TestContext.Current.CancellationToken);

        _sent!.Temperature.Should().Be(0.2f);
        _sent.MaxTokens.Should().Be(900);
        _sent.TopP.Should().Be(0.8f);
        _sent.StopSequences.Should().Equal("END");
        _sent.System.Should().Be("be brief");
        _sent.OutputFormat!.Schema!["type"]!.GetValue<string>().Should().Be("object");
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public async Task ResponseFormat_json_asks_the_provider_for_json(string which)
    {
        Respond("{}");

        await Create(which).CompleteAsync("p", new TextCompletionOptions { ResponseFormat = "json" }, TestContext.Current.CancellationToken);

        _sent!.OutputFormat.Should().BeSameAs(OutputFormat.Json);
    }

    [Theory]
    [MemberData(nameof(Adapters))]
    public async Task CompleteJson_instructs_and_extracts_the_json(string which)
    {
        Respond("Here you go:\n```json\n{\"a\": 1}\n```");

        var json = await Create(which).CompleteJsonAsync("p", new TextCompletionOptions { SystemPrompt = "you rank" }, TestContext.Current.CancellationToken);

        json.Should().Be("{\"a\": 1}");
        _sent!.System.Should().StartWith("you rank").And.Contain("JSON");
        _sent.Temperature.Should().Be(0.1f, "JSON keeps its low, fixed temperature");
        _sent.OutputFormat.Should().BeNull("provider JSON mode only when the options ask for it — a prompt may want an array");
    }
}
