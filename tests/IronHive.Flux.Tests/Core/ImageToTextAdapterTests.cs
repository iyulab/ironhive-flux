using FluentAssertions;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Adapters.ImageToText;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IronHive.Flux.Tests.Core;

public class ImageToTextAdapterTests
{
    private readonly IMessageGenerator _mockGenerator;
    private readonly IOptions<IronHiveFluxCoreOptions> _options;

    public ImageToTextAdapterTests()
    {
        _mockGenerator = Substitute.For<IMessageGenerator>();
        _options = Options.Create(new IronHiveFluxCoreOptions
        {
            ImageToTextModelId = "gpt-4o",
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

    #region FileFlux ImageToText Adapter

    [Fact]
    public async Task FileFlux_ExtractTextAsync_ByteArray_ReturnsResult()
    {
        // Arrange
        SetupGeneratorResponse("Extracted text from image");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        // JPEG magic bytes
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

        // Act
        var result = await adapter.ExtractTextAsync(imageData);

        // Assert
        result.Should().NotBeNull();
        result.ExtractedText.Should().Be("Extracted text from image");
        result.ConfidenceScore.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public async Task FileFlux_ExtractTextAsync_DetectsPng()
    {
        // Arrange
        SetupGeneratorResponse("text");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        // PNG magic bytes
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // Act
        var result = await adapter.ExtractTextAsync(pngData);

        // Assert
        result.Metadata.Format.Should().Be("png");
    }

    [Fact]
    public async Task FileFlux_ExtractTextAsync_DetectsJpeg()
    {
        // Arrange
        SetupGeneratorResponse("text");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        var jpegData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

        // Act
        var result = await adapter.ExtractTextAsync(jpegData);

        // Assert
        result.Metadata.Format.Should().Be("jpeg");
    }

    [Fact]
    public async Task FileFlux_ExtractTextAsync_DetectsGif()
    {
        // Arrange
        SetupGeneratorResponse("text");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        // GIF89a magic bytes
        var gifData = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00 };

        // Act
        var result = await adapter.ExtractTextAsync(gifData);

        // Assert
        result.Metadata.Format.Should().Be("gif");
    }

    [Fact]
    public async Task FileFlux_ExtractTextAsync_EmptyText_LowConfidence()
    {
        // Arrange
        SetupGeneratorResponse("");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        // Act
        var result = await adapter.ExtractTextAsync(imageData);

        // Assert
        result.ConfidenceScore.Should().BeLessThanOrEqualTo(0.5);
    }

    [Fact]
    public async Task FileFlux_ExtractTextAsync_Stream_ReturnsResult()
    {
        // Arrange
        SetupGeneratorResponse("stream text");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });

        // Act
        var result = await adapter.ExtractTextAsync(stream);

        // Assert
        result.ExtractedText.Should().Be("stream text");
    }

    [Fact]
    public void FileFlux_SupportedImageFormats_ContainsExpectedFormats()
    {
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);

        adapter.SupportedImageFormats.Should().Contain("png");
        adapter.SupportedImageFormats.Should().Contain("jpeg");
        adapter.SupportedImageFormats.Should().Contain("gif");
        adapter.SupportedImageFormats.Should().Contain("webp");
    }

    [Fact]
    public void FileFlux_ProviderName_ReturnsIronHive()
    {
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);

        adapter.ProviderName.Should().Be("IronHive");
    }

    [Fact]
    public async Task FileFlux_ExtractTextAsync_RecordsProcessingTime()
    {
        // Arrange
        SetupGeneratorResponse("text");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        // Act
        var result = await adapter.ExtractTextAsync(imageData);

        // Assert
        result.ProcessingTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task FileFlux_ExtractTextAsync_RecordsFileSize()
    {
        // Arrange
        SetupGeneratorResponse("text");
        var adapter = new IronHiveImageToTextServiceForFileFlux(_mockGenerator, _options);
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

        // Act
        var result = await adapter.ExtractTextAsync(imageData);

        // Assert
        result.Metadata.FileSize.Should().Be(6);
    }

    [Fact]
    public void FileFlux_Constructor_NullGenerator_Throws()
    {
        var act = () => new IronHiveImageToTextServiceForFileFlux(null!, _options);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion
}
