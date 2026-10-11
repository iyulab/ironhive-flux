using AwesomeAssertions;
using Flux.Abstractions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Flux.Core.Extensions;
using IronHive.Flux.Rag.Extensions;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IronHive.Flux.Tests.Core;

/// <summary>
/// What a consumer gets from the registration extensions — the configuration it sets must reach the service it
/// resolves, whichever extension (or combination, or order) it called.
/// </summary>
public class AdapterRegistrationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static (ServiceProvider Provider, List<MessageGenerationRequest> Sent) Build(Action<IServiceCollection> register)
    {
        var sent = new List<MessageGenerationRequest>();
        var generator = Substitute.For<IMessageGenerator>();
        generator.GenerateMessageAsync(Arg.Do<MessageGenerationRequest>(sent.Add), Arg.Any<CancellationToken>())
            .Returns(new MessageResponse { Message = Message.Assistant("ok") });
        var services = new ServiceCollection();
        services.AddSingleton(generator);
        services.AddIronHiveFluxCore(o => { o.DefaultCompletionMaxTokens = 900; o.DefaultTemperature = 0.3f; });
        register(services);
        return (services.BuildServiceProvider(), sent);
    }

    [Theory]
    [InlineData("fluxindex")]
    [InlineData("webflux")]
    [InlineData("all")]
    public async Task The_text_completion_service_uses_the_configured_defaults_whichever_adapters_registered_it(string which)
    {
        var (provider, sent) = Build(services => _ = which switch
        {
            "fluxindex" => services.AddIronHiveFluxIndexAdapters(),
            "webflux" => services.AddIronHiveWebFluxAdapters(),
            _ => services.AddAllIronHiveFluxAdapters(),
        });

        await provider.GetRequiredService<ITextCompletionService>().CompleteAsync("p", cancellationToken: Ct);

        var request = sent.Should().ContainSingle().Subject;
        request.MaxTokens.Should().Be(900);
        request.Temperature.Should().Be(0.3f);
    }

    [Fact]
    public async Task The_fluxindex_registration_streams_and_answers_json_like_the_others()
    {
        var (provider, sent) = Build(services => services.AddIronHiveFluxIndexAdapters());
        var service = provider.GetRequiredService<ITextCompletionService>();

        var json = await service.CompleteJsonAsync("p", cancellationToken: Ct);

        json.Should().Be("ok");
        sent.Should().ContainSingle();
        // The interface default would answer a batch one CompleteAsync at a time as well, but the stream default returns
        // the whole text as one chunk without asking the generator to stream; the adapter streams.
        service.GetType().GetMethod(nameof(ITextCompletionService.CompleteStreamAsync))!.DeclaringType
            .Should().Be(service.GetType(), "the adapter implements streaming itself instead of the one-chunk interface default");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Vault_security_set_through_ConfigureVaultSecurity_reaches_the_tools_in_either_order(bool configureFirst)
    {
        var services = new ServiceCollection();
        if (configureFirst)
        {
            services.ConfigureVaultSecurity(o => { o.AllowedBasePaths.Add("/data/allowed"); o.MaxFileSizeBytes = 1024; });
            services.AddFluxRagTools();
        }
        else
        {
            services.AddFluxRagTools();
            services.ConfigureVaultSecurity(o => { o.AllowedBasePaths.Add("/data/allowed"); o.MaxFileSizeBytes = 1024; });
        }

        var security = services.BuildServiceProvider().GetRequiredService<IOptions<VaultSecurityOptions>>().Value;

        security.AllowedBasePaths.Should().Contain("/data/allowed");
        security.MaxFileSizeBytes.Should().Be(1024);
        security.RejectSymlinks.Should().BeTrue("an unset member keeps its default");
    }

    [Fact]
    public void Vault_security_defaults_apply_when_nothing_configures_it()
    {
        var services = new ServiceCollection();
        services.AddFluxRagTools();

        var security = services.BuildServiceProvider().GetRequiredService<IOptions<VaultSecurityOptions>>().Value;

        security.AllowedBasePaths.Should().BeEmpty();
        security.MaxFileSizeBytes.Should().Be(100 * 1024 * 1024);
        security.RejectSymlinks.Should().BeTrue();
    }
}
