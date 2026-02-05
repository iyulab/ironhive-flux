using IronHive.Core.Tools;
using IronHive.Flux.Agent.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json;

namespace IronHive.Flux.Agent.Tools.WebFlux;

/// <summary>
/// 웹 크롤링 도구 - WebFlux를 사용하여 웹 페이지 수집
/// </summary>
public class WebCrawlerTool
{
    private readonly FluxAgentToolsOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebCrawlerTool>? _logger;

    public WebCrawlerTool(
        IOptions<FluxAgentToolsOptions> options,
        HttpClient httpClient,
        ILogger<WebCrawlerTool>? logger = null)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    /// <summary>
    /// 웹 페이지를 크롤링합니다.
    /// </summary>
    /// <param name="url">크롤링할 웹 페이지 URL</param>
    /// <param name="maxDepth">링크 탐색 최대 깊이</param>
    /// <param name="extractImages">이미지 URL 추출 여부</param>
    /// <returns>크롤링된 콘텐츠 (JSON 문자열)</returns>
    [FunctionTool("crawl_web_page")]
    [Description("웹 페이지를 크롤링하여 텍스트 콘텐츠와 링크를 추출합니다.")]
    public async Task<string> CrawlAsync(
        [Description("크롤링할 웹 페이지 URL")] string url,
        [Description("링크 탐색 최대 깊이. 기본값: 1")] int? maxDepth = null,
        [Description("이미지 URL 추출 여부. 기본값: false")] bool? extractImages = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("웹 크롤링 시작 - URL: {Url}", url);

        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "유효하지 않은 URL 형식입니다."
                });
            }

            var depth = maxDepth ?? _options.DefaultMaxCrawlDepth;
            var includeImages = extractImages ?? _options.DefaultExtractImages;

            // HTTP 요청 수행
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; IronHive-Flux/1.0)");

            var response = await _httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);

            // HTML 파싱 (간단한 구현)
            var (title, textContent, links, images) = ParseHtml(html, uri);

            var result = new
            {
                success = true,
                url,
                title,
                contentLength = textContent.Length,
                content = textContent.Length > 5000 ? textContent[..5000] + "..." : textContent,
                linkCount = links.Count,
                links = links.Take(20),
                imageCount = includeImages ? images.Count : 0,
                images = includeImages ? images.Take(10) : null,
                crawledAt = DateTime.UtcNow.ToString("O"),
                statusCode = (int)response.StatusCode
            };

            _logger?.LogInformation("웹 크롤링 완료 - URL: {Url}, ContentLength: {Length}", url, textContent.Length);
            return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP 요청 실패 - URL: {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url,
                error = $"HTTP 요청 실패: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "웹 크롤링 실패 - URL: {Url}", url);
            return JsonSerializer.Serialize(new
            {
                success = false,
                url,
                error = ex.Message
            });
        }
    }

    private static (string Title, string Content, List<string> Links, List<string> Images) ParseHtml(string html, Uri baseUri)
    {
        // 간단한 HTML 파싱 (실제로는 HtmlAgilityPack 등 사용)
        var title = ExtractBetween(html, "<title>", "</title>") ?? "";

        // 스크립트와 스타일 제거
        var content = html;
        content = RemoveBetween(content, "<script", "</script>");
        content = RemoveBetween(content, "<style", "</style>");

        // HTML 태그 제거
        content = System.Text.RegularExpressions.Regex.Replace(content, "<[^>]+>", " ");
        content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ");
        content = System.Net.WebUtility.HtmlDecode(content).Trim();

        // 링크 추출
        var links = new List<string>();
        var linkMatches = System.Text.RegularExpressions.Regex.Matches(
            html, @"href=[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in linkMatches)
        {
            var href = match.Groups[1].Value;
            if (Uri.TryCreate(baseUri, href, out var absoluteUri) &&
                (absoluteUri.Scheme == "http" || absoluteUri.Scheme == "https"))
            {
                links.Add(absoluteUri.ToString());
            }
        }

        // 이미지 추출
        var images = new List<string>();
        var imgMatches = System.Text.RegularExpressions.Regex.Matches(
            html, @"<img[^>]+src=[""']([^""']+)[""']", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (System.Text.RegularExpressions.Match match in imgMatches)
        {
            var src = match.Groups[1].Value;
            if (Uri.TryCreate(baseUri, src, out var absoluteUri))
            {
                images.Add(absoluteUri.ToString());
            }
        }

        return (title, content, links.Distinct().ToList(), images.Distinct().ToList());
    }

    private static string? ExtractBetween(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return null;
        startIndex += start.Length;

        var endIndex = source.IndexOf(end, startIndex, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0) return null;

        return source[startIndex..endIndex].Trim();
    }

    private static string RemoveBetween(string source, string start, string end)
    {
        while (true)
        {
            var startIndex = source.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0) break;

            var endIndex = source.IndexOf(end, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0) break;

            source = source[..startIndex] + source[(endIndex + end.Length)..];
        }
        return source;
    }
}
