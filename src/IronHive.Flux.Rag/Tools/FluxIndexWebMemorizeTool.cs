using FluxIndex.Extensions.FileVault.Interfaces;
using IronHive.Core.Tools;
using IronHive.Flux.Rag.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IronHive.Flux.Rag.Tools;

/// <summary>
/// FluxIndex 웹 페이지 memorize 도구 - URL에서 콘텐츠를 추출하여 IVault에 인덱싱
/// HttpClient로 웹 콘텐츠를 가져와 임시 .md 파일로 저장 후 IVault.MemorizeAsync 호출
/// </summary>
public partial class FluxIndexWebMemorizeTool : IDisposable
{
    private static readonly JsonSerializerOptions s_indentedJsonOptions = new() { WriteIndented = true };

    private readonly FluxRagToolsOptions _options;
    private readonly IVault _vault;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ILogger<FluxIndexWebMemorizeTool>? _logger;
    private bool _disposed;

    public FluxIndexWebMemorizeTool(
        IVault vault,
        IOptions<FluxRagToolsOptions> options,
        HttpClient? httpClient = null,
        ILogger<FluxIndexWebMemorizeTool>? logger = null)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = CreateDefaultHttpClient();
            _ownsHttpClient = true;
        }
    }

    /// <summary>
    /// URL에서 웹 페이지 콘텐츠를 추출하여 지식 베이스에 저장합니다.
    /// 웹 콘텐츠를 다운로드하고, 임시 마크다운 파일로 저장한 후 인덱싱 파이프라인을 실행합니다.
    /// </summary>
    /// <param name="url">인덱싱할 웹 페이지 URL</param>
    /// <param name="title">문서 제목 (null이면 URL에서 추출)</param>
    /// <returns>저장 결과 (JSON 문자열)</returns>
    [FunctionTool("memorize_web_page")]
    [Description("웹 페이지 URL에서 콘텐츠를 추출하여 지식 베이스에 저장합니다. 웹 콘텐츠를 마크다운으로 변환하여 인덱싱합니다.")]
    public async Task<string> MemorizeWebPageAsync(
        [Description("인덱싱할 웹 페이지의 URL")] string url,
        [Description("문서 제목 (선택사항, 없으면 URL에서 추출)")] string? title = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogWebMemorizeStarted(_logger, url);

        string? tempFilePath = null;
        try
        {
            // URL validation
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    url,
                    error = $"Invalid URL: {url}. Only HTTP and HTTPS URLs are supported."
                }, s_indentedJsonOptions);
            }

            // Download web content
            var (content, contentType) = await DownloadContentAsync(uri, cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    url,
                    error = "Downloaded content is empty."
                }, s_indentedJsonOptions);
            }

            // Convert to markdown if HTML
            var markdownContent = IsHtmlContent(content, contentType)
                ? ConvertHtmlToBasicMarkdown(content, url, title)
                : FormatAsMarkdown(content, url, title);

            // Save to temp file
            tempFilePath = CreateTempMarkdownFile(url);
            await File.WriteAllTextAsync(tempFilePath, markdownContent, cancellationToken);

            if (_logger is not null)
                LogTempFileSaved(_logger, tempFilePath, markdownContent.Length);

            // Memorize via IVault
            await _vault.MemorizeAsync(tempFilePath, cancellationToken);

            var effectiveTitle = title ?? ExtractTitleFromUrl(uri);

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
        catch (HttpRequestException ex)
        {
            if (_logger is not null)
                LogWebMemorizeFailed(_logger, ex, url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url,
                error = $"Failed to download web page: {ex.Message}"
            }, s_indentedJsonOptions);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            if (_logger is not null)
                LogWebMemorizeFailed(_logger, ex, url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url,
                error = "Request timed out while downloading web page."
            }, s_indentedJsonOptions);
        }
        catch (Exception ex)
        {
            if (_logger is not null)
                LogWebMemorizeFailed(_logger, ex, url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url,
                error = ex.Message
            }, s_indentedJsonOptions);
        }
        finally
        {
            // Cleanup temp file
            CleanupTempFile(tempFilePath);
        }
    }

    #region Content Download & Conversion

    internal async Task<(string content, string? contentType)> DownloadContentAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;

        return (content, contentType);
    }

    internal static bool IsHtmlContent(string content, string? contentType)
    {
        if (contentType is not null &&
            contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
            return true;

        // Heuristic: check if content starts with HTML-like tags
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// HTML 콘텐츠를 기본 마크다운으로 변환합니다.
    /// script/style 태그 제거, HTML 엔티티 처리, 기본 텍스트 추출 수행.
    /// 정교한 변환이 필요하면 WebFlux IContentExtractor 연동을 권장.
    /// </summary>
    internal static string ConvertHtmlToBasicMarkdown(string html, string url, string? title)
    {
        // Remove script and style blocks
        var cleaned = RemoveHtmlBlocks(html, "script");
        cleaned = RemoveHtmlBlocks(cleaned, "style");
        cleaned = RemoveHtmlBlocks(cleaned, "nav");
        cleaned = RemoveHtmlBlocks(cleaned, "footer");
        cleaned = RemoveHtmlBlocks(cleaned, "header");

        // Extract title from <title> tag if not provided
        var extractedTitle = title ?? ExtractHtmlTitle(html);

        // Basic HTML tag removal (preserving content between tags)
        cleaned = StripHtmlTags(cleaned);

        // Decode common HTML entities
        cleaned = DecodeHtmlEntities(cleaned);

        // Normalize whitespace
        cleaned = NormalizeWhitespace(cleaned);

        return FormatAsMarkdown(cleaned.Trim(), url, extractedTitle);
    }

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

        // Remove file extension and decode
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

    #region HTML Processing Helpers

    internal static string RemoveHtmlBlocks(string html, string tagName)
    {
        var result = html;
        while (true)
        {
            var startIdx = result.IndexOf($"<{tagName}", StringComparison.OrdinalIgnoreCase);
            if (startIdx < 0) break;

            var endTag = $"</{tagName}>";
            var endIdx = result.IndexOf(endTag, startIdx, StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0)
            {
                // Self-closing or malformed - remove to end of tag
                var closeIdx = result.IndexOf('>', startIdx);
                if (closeIdx < 0) break;
                result = string.Concat(result.AsSpan(0, startIdx), result.AsSpan(closeIdx + 1));
            }
            else
            {
                result = string.Concat(result.AsSpan(0, startIdx), result.AsSpan(endIdx + endTag.Length));
            }
        }
        return result;
    }

    internal static string? ExtractHtmlTitle(string html)
    {
        var titleStart = html.IndexOf("<title", StringComparison.OrdinalIgnoreCase);
        if (titleStart < 0) return null;

        var contentStart = html.IndexOf('>', titleStart);
        if (contentStart < 0) return null;
        contentStart++;

        var titleEnd = html.IndexOf("</title>", contentStart, StringComparison.OrdinalIgnoreCase);
        if (titleEnd < 0) return null;

        var title = html[contentStart..titleEnd].Trim();
        return string.IsNullOrWhiteSpace(title) ? null : DecodeHtmlEntities(title);
    }

    internal static string StripHtmlTags(string html)
    {
        var result = new System.Text.StringBuilder(html.Length);
        var inTag = false;

        foreach (var ch in html)
        {
            if (ch == '<')
            {
                inTag = true;
                continue;
            }
            if (ch == '>')
            {
                inTag = false;
                result.Append(' '); // Replace tag boundary with space
                continue;
            }
            if (!inTag)
            {
                result.Append(ch);
            }
        }

        return result.ToString();
    }

    internal static string DecodeHtmlEntities(string text)
    {
        return text
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&#39;", "'")
            .Replace("&apos;", "'")
            .Replace("&nbsp;", " ")
            .Replace("&#x27;", "'")
            .Replace("&#x2F;", "/");
    }

    internal static string NormalizeWhitespace(string text)
    {
        // Collapse multiple whitespace into single space, then restore paragraph breaks
        var lines = text.Split('\n')
            .Select(line => CollapseSpaces(line.Trim()))
            .Where(line => !string.IsNullOrEmpty(line));

        return string.Join("\n\n", lines);
    }

    private static string CollapseSpaces(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = new System.Text.StringBuilder(text.Length);
        var previousWasSpace = false;

        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    result.Append(' ');
                    previousWasSpace = true;
                }
            }
            else
            {
                result.Append(ch);
                previousWasSpace = false;
            }
        }

        return result.ToString();
    }

    #endregion

    #region Cleanup & Lifecycle

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

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("IronHive-FluxRag", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/html"));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("text/plain", 0.9));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("*/*", 0.8));
        return client;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        GC.SuppressFinalize(this);
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
