using FluxFeed.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;
using WebFlux.Core.Interfaces;
using WebFlux.Core.Options;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 웹 페이지 memorize 도구 — URL 의 본문을 WebFlux(<see cref="IContentExtractService"/>)로 추출해 임시 .md 로
/// 저장한 뒤 <see cref="IVault.MemorizeAsync(string, CancellationToken)"/> 로 인덱싱한다.
/// </summary>
/// <remarks>
/// 추출은 WebFlux 가 한다 — robots.txt 존중, 요청별 타임아웃, 보일러플레이트 제거, 마크다운 변환이 WebFlux 의 기본값과
/// 설정대로 적용된다. 0.8.0 이전에는 이 도구가 자체 <c>HttpClient</c> 와 정규식 태그 제거로 같은 일을 다시 구현해,
/// WebFlux 에서 고친 robots·타임아웃 동작이 이 도구에는 닿지 않았다.
/// <para>
/// 호스트가 WebFlux 를 등록해야 한다(<c>services.AddWebFlux()</c>). 등록되지 않은 컨테이너에서는
/// <c>GetFluxRagTools</c> 가 이 도구를 내놓지 않는다.
/// </para>
/// </remarks>
public partial class FluxIndexWebMemorizeTool
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly IVault _vault;
    private readonly IContentExtractService _extractor;
    private readonly ILogger<FluxIndexWebMemorizeTool>? _logger;

    public FluxIndexWebMemorizeTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        IContentExtractService extractor,
        ILogger<FluxIndexWebMemorizeTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
        _logger = logger;
    }

    /// <summary>
    /// URL에서 웹 페이지 콘텐츠를 추출하여 지식 베이스에 저장합니다.
    /// </summary>
    /// <param name="url">인덱싱할 웹 페이지 URL</param>
    /// <param name="title">문서 제목 (null이면 페이지 제목, 그것도 없으면 URL에서 추출)</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>저장 결과 (JSON 문자열)</returns>
    [FunctionTool("memorize_web_page")]
    [Description("웹 페이지 URL에서 콘텐츠를 추출하여 지식 베이스에 저장합니다. 웹 콘텐츠를 마크다운으로 변환하여 인덱싱합니다.")]
    public async Task<string> MemorizeWebPageAsync(
        [Description("인덱싱할 웹 페이지의 URL")] string url,
        [Description("문서 제목 (선택사항, 없으면 페이지 제목 또는 URL에서 추출)")] string? title = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogWebMemorizeStarted(_logger, url);

        string? tempFilePath = null;
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return Failure(url, $"Invalid URL: {url}. Only HTTP and HTTPS URLs are supported.");
            }

            var extracted = await _extractor.ExtractContentAsync(
                url,
                new ExtractOptions { Format = OutputFormat.Markdown },
                cancellationToken).ConfigureAwait(false);

            if (!extracted.IsSuccess || extracted.Data is null)
            {
                var reason = extracted.Error?.Message ?? "the extractor reported no content";
                return Failure(url, $"Failed to extract web page: {reason}");
            }

            var page = extracted.Data;
            var body = !string.IsNullOrWhiteSpace(page.MainContent) ? page.MainContent : page.Text;
            if (string.IsNullOrWhiteSpace(body))
            {
                return Failure(url, "Extracted content is empty.");
            }

            var effectiveTitle = title
                ?? (string.IsNullOrWhiteSpace(page.Title) ? ExtractTitleFromUrl(uri) : page.Title);
            var markdownContent = FormatAsMarkdown(body, url, effectiveTitle);

            tempFilePath = CreateTempMarkdownFile(url);
            await File.WriteAllTextAsync(tempFilePath, markdownContent, cancellationToken).ConfigureAwait(false);

            if (_logger is not null)
                LogTempFileSaved(_logger, tempFilePath, markdownContent.Length);

            await _vault.MemorizeAsync(tempFilePath, cancellationToken).ConfigureAwait(false);

            var result = new
            {
                success = true,
                url,
                title = effectiveTitle,
                tempFilePath,
                contentLength = markdownContent.Length,
                memorizedAt = DateTime.UtcNow.ToString("O"),
                message = $"Successfully memorized web page '{effectiveTitle}' from {url}"
            };

            if (_logger is not null)
                LogWebMemorizeCompleted(_logger, url, markdownContent.Length);
            return JsonSerializer.Serialize(result, s_indentedJsonOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogWebMemorizeFailed(_logger, ex, url);
            return Failure(url, ex.Message);
        }
        finally
        {
            CleanupTempFile(tempFilePath);
        }
    }

    private static string Failure(string url, string error) =>
        JsonSerializer.Serialize(new { success = false, url, error }, s_indentedJsonOptions);

    #region Formatting

    internal static string FormatAsMarkdown(string content, string url, string? title)
    {
        var effectiveTitle = title ?? new Uri(url).Host;
        return $"""
            ---
            source: {url}
            title: {effectiveTitle}
            memorized_at: {DateTime.UtcNow:O}
            ---

            # {effectiveTitle}

            {content}
            """;
    }

    internal static string ExtractTitleFromUrl(Uri uri)
    {
        // Try to extract a meaningful title from the URL path
        var path = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrEmpty(path))
            return uri.Host;

        var lastSegment = path.Split('/').LastOrDefault(s => !string.IsNullOrEmpty(s));
        if (lastSegment is null)
            return uri.Host;

        var decoded = Uri.UnescapeDataString(lastSegment);
        var withoutExt = Path.GetFileNameWithoutExtension(decoded);
        return string.IsNullOrWhiteSpace(withoutExt) ? uri.Host : withoutExt.Replace('-', ' ').Replace('_', ' ');
    }

    internal static string CreateTempMarkdownFile(string url)
    {
        var hash = url.GetHashCode(StringComparison.OrdinalIgnoreCase);
        var fileName = $"web-memorize-{Math.Abs(hash):X8}-{DateTime.UtcNow:yyyyMMddHHmmss}.md";
        return Path.Combine(Path.GetTempPath(), fileName);
    }

    #endregion

    #region Cleanup

    private void CleanupTempFile(string? filePath)
    {
        if (filePath is null) return;

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                if (_logger is not null)
                    LogTempFileDeleted(_logger, filePath);
            }
        }
        catch (Exception ex)
        {
            // Non-critical - temp file cleanup failure should not fail the operation
            if (_logger is not null)
                LogTempFileCleanupFailed(_logger, ex, filePath);
        }
    }

    #endregion

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "Web page memorize started - URL: {Url}")]
    private static partial void LogWebMemorizeStarted(ILogger logger, string Url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Web page memorize completed - URL: {Url}, ContentLength: {ContentLength}")]
    private static partial void LogWebMemorizeCompleted(ILogger logger, string Url, int ContentLength);

    [LoggerMessage(Level = LogLevel.Error, Message = "Web page memorize failed - URL: {Url}")]
    private static partial void LogWebMemorizeFailed(ILogger logger, Exception ex, string Url);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Temp file saved - Path: {Path}, Size: {Size}")]
    private static partial void LogTempFileSaved(ILogger logger, string Path, int Size);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Temp file deleted - Path: {Path}")]
    private static partial void LogTempFileDeleted(ILogger logger, string Path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Temp file cleanup failed - Path: {Path}")]
    private static partial void LogTempFileCleanupFailed(ILogger logger, Exception ex, string Path);

    #endregion
}
