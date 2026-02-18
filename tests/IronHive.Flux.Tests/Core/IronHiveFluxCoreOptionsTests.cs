using FluentAssertions;
using IronHive.Flux.Core.Options;
using Xunit;

namespace IronHive.Flux.Tests.Core;

public class IronHiveFluxCoreOptionsTests
{
    [Fact]
    public void Defaults_ShouldHaveCorrectValues()
    {
        var options = new IronHiveFluxCoreOptions();

        options.EmbeddingModelId.Should().Be("text-embedding-3-small");
        options.TextCompletionModelId.Should().Be("gpt-4o");
        options.ImageToTextModelId.Should().Be("gpt-4o");
        options.EmbeddingDimension.Should().Be(0);
        options.MaxTokens.Should().Be(8191);
        options.DefaultTemperature.Should().Be(0.7f);
        options.DefaultCompletionMaxTokens.Should().Be(500);
    }

    [Fact]
    public void AllProperties_ShouldBeSettable()
    {
        var options = new IronHiveFluxCoreOptions
        {
            EmbeddingModelId = "text-embedding-ada-002",
            TextCompletionModelId = "gpt-3.5-turbo",
            ImageToTextModelId = "gpt-4-vision",
            EmbeddingDimension = 768,
            MaxTokens = 4096,
            DefaultTemperature = 0.3f,
            DefaultCompletionMaxTokens = 1000
        };

        options.EmbeddingModelId.Should().Be("text-embedding-ada-002");
        options.TextCompletionModelId.Should().Be("gpt-3.5-turbo");
        options.ImageToTextModelId.Should().Be("gpt-4-vision");
        options.EmbeddingDimension.Should().Be(768);
        options.MaxTokens.Should().Be(4096);
        options.DefaultTemperature.Should().Be(0.3f);
        options.DefaultCompletionMaxTokens.Should().Be(1000);
    }
}
