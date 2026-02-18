using Microsoft.Extensions.Logging;
using WebLookup;

namespace IronHive.Flux.WebLookup;

/// <summary>
/// WebLookup → WebFlux → FluxIndex end-to-end RAG 파이프라인.
/// 웹 검색으로 URL을 발견하고, 콘텐츠를 처리하고, 벡터 인덱스에 색인합니다.
/// </summary>
public partial class WebLookupRagPipeline
{
    private readonly WebSearchClient _webSearch;
    private readonly SiteExplorer _siteExplorer;
    private readonly ILogger<WebLookupRagPipeline>? _logger;

    public WebLookupRagPipeline(
        WebSearchClient webSearch,
        SiteExplorer siteExplorer,
        ILogger<WebLookupRagPipeline>? logger = null)
    {
        _webSearch = webSearch ?? throw new ArgumentNullException(nameof(webSearch));
        _siteExplorer = siteExplorer ?? throw new ArgumentNullException(nameof(siteExplorer));
        _logger = logger;
    }

    /// <summary>
    /// 검색 쿼리로 URL을 발견합니다.
    /// </summary>
    /// <param name="query">검색 쿼리</param>
    /// <param name="maxResults">최대 결과 수</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>발견된 URL 목록</returns>
    public async Task<IReadOnlyList<string>> DiscoverUrlsAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogUrlDiscoveryStarted(_logger, query, maxResults);

        var results = await _webSearch.SearchAsync(query, new WebSearchOptions
        {
            MaxResultsPerProvider = maxResults
        }, cancellationToken);

        var urls = results
            .Select(r => r.Url)
            .Take(maxResults)
            .ToList();

        if (_logger is not null)
            LogUrlDiscoveryCompleted(_logger, urls.Count);
        return urls;
    }

    /// <summary>
    /// 사이트의 sitemap에서 URL을 발견합니다.
    /// </summary>
    /// <param name="baseUrl">사이트 기본 URL</param>
    /// <param name="maxUrls">최대 URL 수</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>sitemap에서 발견된 URL 목록</returns>
    public async Task<IReadOnlyList<string>> DiscoverSitemapUrlsAsync(
        string baseUrl,
        int maxUrls = 100,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogSitemapDiscoveryStarted(_logger, baseUrl);

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException($"유효하지 않은 URL: {baseUrl}", nameof(baseUrl));

        var robots = await _siteExplorer.GetRobotsAsync(uri, cancellationToken);
        var urls = new List<string>();

        foreach (var sitemapUrl in robots.Sitemaps)
        {
            if (!Uri.TryCreate(sitemapUrl, UriKind.Absolute, out var sitemapUri))
                continue;

            await foreach (var entry in _siteExplorer.StreamSitemapAsync(sitemapUri, cancellationToken))
            {
                urls.Add(entry.Url);
                if (urls.Count >= maxUrls)
                    break;
            }

            if (urls.Count >= maxUrls)
                break;
        }

        if (_logger is not null)
            LogSitemapDiscoveryCompleted(_logger, urls.Count);
        return urls;
    }

    /// <summary>
    /// 검색과 sitemap을 결합하여 URL을 발견합니다.
    /// </summary>
    /// <param name="query">검색 쿼리</param>
    /// <param name="siteBaseUrl">sitemap을 조회할 사이트 URL (null이면 검색만 수행)</param>
    /// <param name="maxUrls">최대 URL 수</param>
    /// <param name="cancellationToken">취소 토큰</param>
    /// <returns>중복 제거된 URL 목록</returns>
    public async Task<IReadOnlyList<string>> DiscoverCombinedUrlsAsync(
        string query,
        string? siteBaseUrl = null,
        int maxUrls = 50,
        CancellationToken cancellationToken = default)
    {
        var tasks = new List<Task<IReadOnlyList<string>>>
        {
            DiscoverUrlsAsync(query, maxUrls, cancellationToken)
        };

        if (siteBaseUrl is not null)
        {
            tasks.Add(DiscoverSitemapUrlsAsync(siteBaseUrl, maxUrls, cancellationToken));
        }

        var results = await Task.WhenAll(tasks);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var combined = new List<string>();

        foreach (var urlList in results)
        {
            foreach (var url in urlList)
            {
                if (seen.Add(url))
                {
                    combined.Add(url);
                    if (combined.Count >= maxUrls)
                        return combined;
                }
            }
        }

        return combined;
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "URL 발견 시작 - Query: {Query}, MaxResults: {Max}")]
    private static partial void LogUrlDiscoveryStarted(ILogger logger, string Query, int Max);

    [LoggerMessage(Level = LogLevel.Information, Message = "URL 발견 완료 - {Count}개 URL")]
    private static partial void LogUrlDiscoveryCompleted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sitemap URL 발견 시작 - BaseUrl: {Url}")]
    private static partial void LogSitemapDiscoveryStarted(ILogger logger, string Url);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sitemap URL 발견 완료 - {Count}개 URL")]
    private static partial void LogSitemapDiscoveryCompleted(ILogger logger, int Count);

    #endregion
}
