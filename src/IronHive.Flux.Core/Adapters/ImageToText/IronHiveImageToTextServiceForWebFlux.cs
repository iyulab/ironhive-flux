using IronHive.Abstractions.Messages;
using IronHive.Abstractions.Messages.Content;
using IronHive.Abstractions.Messages.Roles;
using IronHive.Flux.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebFlux.Core.Interfaces;
using WebFlux.Core.Models;
using WebFlux.Core.Options;

namespace IronHive.Flux.Core.Adapters.ImageToText;

/// <summary>
/// IronHive IMessageGenerator를 WebFlux IImageToTextService로 어댑트
/// </summary>
public class IronHiveImageToTextServiceForWebFlux : WebFlux.Core.Interfaces.IImageToTextService
{
    private readonly IMessageGenerator _generator;
    private readonly IronHiveFluxCoreOptions _options;
    private readonly ILogger<IronHiveImageToTextServiceForWebFlux>? _logger;
    private readonly HttpClient _httpClient;

    public IronHiveImageToTextServiceForWebFlux(
        IMessageGenerator generator,
        IOptions<IronHiveFluxCoreOptions> options,
        HttpClient httpClient,
        ILogger<IronHiveImageToTextServiceForWebFlux>? logger = null)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ConvertImageToTextAsync(
        string imageUrl,
        ImageToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("WebFlux URL 이미지 텍스트 변환 시작 - URL: {Url}", imageUrl);

        var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        var mimeType = DetectMimeType(imageUrl, imageBytes);

        return await ConvertImageToTextAsync(imageBytes, mimeType, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> ConvertImageToTextAsync(
        byte[] imageBytes,
        string mimeType,
        ImageToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("WebFlux 이미지 텍스트 변환 시작 - Size: {Size} bytes, MimeType: {MimeType}",
            imageBytes.Length, mimeType);

        var base64 = Convert.ToBase64String(imageBytes);
        var format = MimeTypeToImageFormat(mimeType);
        var prompt = "Describe this image in detail.";

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
            Temperature = 0.5f,
            MaxTokens = 1000
        };

        var response = await _generator.GenerateMessageAsync(request, cancellationToken);
        var result = ExtractTextFromResponse(response);

        _logger?.LogDebug("WebFlux 이미지 텍스트 변환 완료 - ResultLength: {Length}", result.Length);
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ConvertImagesBatchAsync(
        IEnumerable<string> imageUrls,
        ImageToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var urlList = imageUrls.ToList();
        _logger?.LogDebug("WebFlux 배치 이미지 변환 시작 - Count: {Count}", urlList.Count);

        var results = new List<string>();
        foreach (var url in urlList)
        {
            var result = await ConvertImageToTextAsync(url, options, cancellationToken);
            results.Add(result);
        }

        _logger?.LogDebug("WebFlux 배치 이미지 변환 완료 - Count: {Count}", results.Count);
        return results;
    }

    /// <inheritdoc />
    public async Task<string> ExtractTextFromImageAsync(
        string imageUrl,
        CancellationToken cancellationToken = default)
    {
        return await ConvertImageToTextAsync(imageUrl, null, cancellationToken);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSupportedImageFormats()
    {
        return ["image/png", "image/jpeg", "image/gif", "image/webp"];
    }

    /// <inheritdoc />
    public async Task<ImageToTextResult> ExtractTextFromWebImageAsync(
        string imageUrl,
        ImageToTextOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("WebFlux 웹 이미지 텍스트 추출 시작 - URL: {Url}", imageUrl);

        var text = await ConvertImageToTextAsync(imageUrl, options, cancellationToken);

        return new ImageToTextResult
        {
            ExtractedText = text,
            Confidence = 0.85,
            IsSuccess = true,
            SourceUrl = imageUrl
        };
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new MessageGenerationRequest
            {
                Model = _options.ImageToTextModelId,
                Messages = [new UserMessage { Content = [new TextMessageContent { Value = "ping" }] }],
                MaxTokens = 1
            };
            await _generator.GenerateMessageAsync(request, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ImageFormat MimeTypeToImageFormat(string mimeType)
    {
        return mimeType.ToLower() switch
        {
            "image/png" => ImageFormat.Png,
            "image/jpeg" or "image/jpg" => ImageFormat.Jpeg,
            "image/gif" => ImageFormat.Gif,
            "image/webp" => ImageFormat.Webp,
            _ => ImageFormat.Jpeg
        };
    }

    private static string DetectMimeType(string url, byte[] imageData)
    {
        var extension = Path.GetExtension(new Uri(url).AbsolutePath).ToLower();
        var mimeFromExtension = extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => null
        };

        if (mimeFromExtension != null)
            return mimeFromExtension;

        if (imageData.Length >= 4)
        {
            if (imageData[0] == 0x89 && imageData[1] == 0x50)
                return "image/png";
            if (imageData[0] == 0xFF && imageData[1] == 0xD8)
                return "image/jpeg";
            if (imageData[0] == 0x47 && imageData[1] == 0x49)
                return "image/gif";
        }

        return "image/jpeg";
    }

    private static string ExtractTextFromResponse(MessageResponse response)
    {
        var textContents = response.Message.Content?
            .OfType<TextMessageContent>()
            .Select(c => c.Value);

        return textContents != null ? string.Join("", textContents) : string.Empty;
    }
}
