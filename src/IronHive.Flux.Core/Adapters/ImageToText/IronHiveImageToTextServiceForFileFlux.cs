using FileFlux;
using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace IronHive.Flux.Core.Adapters.ImageToText;

/// <summary>
/// IronHive IMessageGenerator를 FileFlux IImageToTextService로 어댑트
/// </summary>
public partial class IronHiveImageToTextServiceForFileFlux : FileFlux.IImageToTextService
{
    private readonly IMessageGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveImageToTextServiceForFileFlux>? _logger;

    public IronHiveImageToTextServiceForFileFlux(
        IMessageGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        ILogger<IronHiveImageToTextServiceForFileFlux>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public IEnumerable<string> SupportedImageFormats => ["png", "jpeg", "jpg", "gif", "webp"];

    /// <inheritdoc />
    public string ProviderName => "IronHive";

    /// <inheritdoc />
    public async Task<ImageToTextResult> ExtractTextAsync(
        byte[] imageData,
        ImageToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogImageExtractStarted(_logger, imageData.Length);
        var stopwatch = Stopwatch.StartNew();

        var base64 = Convert.ToBase64String(imageData);
        var format = DetectImageFormat(imageData);

        var result = await ProcessImageAsync(base64, format, options, cancellationToken);

        stopwatch.Stop();
        result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        result.Metadata.FileSize = imageData.Length;

        if (_logger is not null)
            LogImageExtractCompleted(_logger, result.ProcessingTimeMs);
        return result;
    }

    /// <inheritdoc />
    public async Task<ImageToTextResult> ExtractTextAsync(
        Stream imageStream,
        ImageToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream, cancellationToken);
        return await ExtractTextAsync(memoryStream.ToArray(), options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ImageToTextResult> ExtractTextAsync(
        string imagePath,
        ImageToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        return await ExtractTextAsync(imageData, options, cancellationToken);
    }

    private async Task<ImageToTextResult> ProcessImageAsync(
        string base64,
        ImageFormat format,
        ImageToTextOptions? options,
        CancellationToken cancellationToken)
    {
        var prompt = options?.CustomPrompt ?? BuildPrompt(options);

        var request = new MessageGenerationRequest
        {
            Model = _options.ImageToTextModelId,
            Messages =
            [
                new UserMessage
                {
                    Content =
                    [
                        new ImageMessageContent
                        {
                            Format = format,
                            Base64 = base64
                        },
                        new TextMessageContent { Value = prompt }
                    ]
                }
            ],
            Temperature = 0.3f,
            MaxTokens = 2000
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var extractedText = ExtractTextFromResponse(response);

        return new ImageToTextResult
        {
            ExtractedText = extractedText,
            ConfidenceScore = string.IsNullOrWhiteSpace(extractedText) ? 0.3 : 0.85,
            DetectedLanguage = options?.Language == "auto" ? "unknown" : (options?.Language ?? "unknown"),
            ImageType = options?.ImageTypeHint ?? "unknown",
            Metadata = new FileFlux.ImageMetadata
            {
                Format = format.ToString().ToLowerInvariant()
            }
        };
    }

    private static string BuildPrompt(ImageToTextOptions? options)
    {
        var parts = new List<string>
        {
            "Extract all text from this image."
        };

        if (options?.ExtractStructure == true)
            parts.Add("Preserve the structure including tables, lists, and headings.");

        if (!string.IsNullOrEmpty(options?.ImageTypeHint))
            parts.Add($"This is a {options.ImageTypeHint} image.");

        if (options?.Quality == "high")
            parts.Add("Be thorough and extract every detail.");

        return string.Join(" ", parts);
    }

    private static ImageFormat DetectImageFormat(byte[] imageData)
    {
        if (imageData.Length < 4)
            return ImageFormat.Jpeg;

        if (imageData[0] == 0x89 && imageData[1] == 0x50 && imageData[2] == 0x4E && imageData[3] == 0x47)
            return ImageFormat.Png;

        if (imageData[0] == 0xFF && imageData[1] == 0xD8 && imageData[2] == 0xFF)
            return ImageFormat.Jpeg;

        if (imageData[0] == 0x47 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x38)
            return ImageFormat.Gif;

        if (imageData.Length > 12 && imageData[0] == 0x52 && imageData[1] == 0x49 && imageData[2] == 0x46 && imageData[3] == 0x46
            && imageData[8] == 0x57 && imageData[9] == 0x45 && imageData[10] == 0x42 && imageData[11] == 0x50)
            return ImageFormat.Webp;

        return ImageFormat.Jpeg;
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 이미지 텍스트 추출 시작 - Size: {Size} bytes")]
    private static partial void LogImageExtractStarted(ILogger logger, int Size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "FileFlux 이미지 텍스트 추출 완료 - Time: {Time}ms")]
    private static partial void LogImageExtractCompleted(ILogger logger, long Time);

    #endregion
}
