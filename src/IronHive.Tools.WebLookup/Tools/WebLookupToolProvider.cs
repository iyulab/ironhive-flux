using IronHive.Core.Tools;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;
using WebLookup;

namespace IronHive.Tools.WebLookup.Tools;

/// <summary>
/// WebLookup을 IronHive 에이전트 FunctionTool로 노출하는 프로바이더
/// </summary>
public partial class WebLookupToolProvider
{
    private readonly WebSearchClient _client;
    private readonly SiteExplorer _explorer;
    private readonly WebLookupToolOptions _options;
    private readonly ILogger<WebLookupToolProvider>? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public WebLookupToolProvider(
        WebSearchClient client,
        SiteExplorer explorer,
        WebLookupToolOptions options,
        ILogger<WebLookupToolProvider>? logger = null)
    {
        _client = client;
        _explorer = explorer;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 웹에서 정보를 검색하여 관련 URL과 설명을 반환합니다.
    /// </summary>
    [FunctionTool("web_search")]
    [Description("웹에서 정보를 검색하여 관련 URL과 설명을 반환합니다. 최신 정보를 찾거나 특정 주제를 조사할 때 사용하세요.")]
    public async Task<string> SearchAsync(
        [Description("검색 쿼리")] string query,
        [Description("최대 결과 수. 기본값: 10")] int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogWebSearchStarted(_logger, query);

        try
        {
            var max = maxResults ?? _options.DefaultMaxResults;
            var results = await _client.SearchAsync(query, new WebSearchOptions
            {
                MaxResultsPerProvider = max
            }, cancellationToken);

            var response = new
            {
                success = true,
                query,
                resultCount = results.Count,
                results = results.Select(r => new
                {
                    url = r.Url,
                    title = r.Title,
                    description = r.Description,
                    provider = r.Provider
                })
            };

            if (_logger is not null)
                LogWebSearchCompleted(_logger, results.Count);
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger is not null)
                LogWebSearchFailed(_logger, ex, query);
            return JsonSerializer.Serialize(new
            {
                success = false,
                query,
                error = ex.Message
            }, JsonOptions);
        }
    }

    /// <summary>
    /// 웹사이트의 robots.txt와 sitemap을 분석하여 구조를 파악합니다.
    /// </summary>
    [FunctionTool("explore_site")]
    [Description("웹사이트의 robots.txt와 sitemap을 분석하여 URL 목록과 사이트 구조를 반환합니다. 특정 사이트의 콘텐츠를 체계적으로 탐색할 때 사용하세요.")]
    public async Task<string> ExploreSiteAsync(
        [Description("사이트 기본 URL (예: https://example.com)")] string baseUrl,
        [Description("sitemap URL 목록도 조회할지 여부. 기본값: true")] bool? includeSitemap = null,
        CancellationToken cancellationToken = default)
    {
        if (_logger is not null)
            LogSiteExploreStarted(_logger, baseUrl);

        try
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    baseUrl,
                    error = "유효하지 않은 URL입니다."
                }, JsonOptions);
            }

            var robots = await _explorer.GetRobotsAsync(uri, cancellationToken);
            var fetchSitemap = includeSitemap ?? true;

            object? sitemapEntries = null;
            if (fetchSitemap && robots.Sitemaps.Count > 0)
            {
                var entries = new List<object>();
                foreach (var sitemapUrl in robots.Sitemaps)
                {
                    if (!Uri.TryCreate(sitemapUrl, UriKind.Absolute, out var sitemapUri))
                        continue;

                    var count = 0;
                    await foreach (var entry in _explorer.StreamSitemapAsync(sitemapUri, cancellationToken))
                    {
                        entries.Add(new
                        {
                            url = entry.Url,
                            lastModified = entry.LastModified?.ToString("o"),
                            changeFrequency = entry.ChangeFrequency,
                            priority = entry.Priority
                        });

                        if (++count >= _options.MaxSitemapEntries)
                            break;
                    }

                    if (count >= _options.MaxSitemapEntries)
                        break;
                }
                sitemapEntries = entries;
            }

            var response = new
            {
                success = true,
                baseUrl,
                robots = new
                {
                    sitemapUrls = robots.Sitemaps,
                    crawlDelay = robots.CrawlDelay?.TotalSeconds,
                    ruleCount = robots.Rules.Count
                },
                sitemapEntries,
                sitemapEntryCount = (sitemapEntries as List<object>)?.Count ?? 0
            };

            if (_logger is not null)
                LogSiteExploreCompleted(_logger, baseUrl, robots.Sitemaps.Count);
            return JsonSerializer.Serialize(response, JsonOptions);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_logger is not null)
                LogSiteExploreFailed(_logger, ex, baseUrl);
            return JsonSerializer.Serialize(new
            {
                success = false,
                baseUrl,
                error = ex.Message
            }, JsonOptions);
        }
    }

    #region LoggerMessage

    [LoggerMessage(Level = LogLevel.Information, Message = "웹 검색 시작 - Query: {Query}")]
    private static partial void LogWebSearchStarted(ILogger logger, string Query);

    [LoggerMessage(Level = LogLevel.Information, Message = "웹 검색 완료 - ResultCount: {Count}")]
    private static partial void LogWebSearchCompleted(ILogger logger, int Count);

    [LoggerMessage(Level = LogLevel.Error, Message = "웹 검색 실패 - Query: {Query}")]
    private static partial void LogWebSearchFailed(ILogger logger, Exception ex, string Query);

    [LoggerMessage(Level = LogLevel.Information, Message = "사이트 탐색 시작 - URL: {Url}")]
    private static partial void LogSiteExploreStarted(ILogger logger, string Url);

    [LoggerMessage(Level = LogLevel.Information, Message = "사이트 탐색 완료 - URL: {Url}, Sitemaps: {Count}")]
    private static partial void LogSiteExploreCompleted(ILogger logger, string Url, int Count);

    [LoggerMessage(Level = LogLevel.Error, Message = "사이트 탐색 실패 - URL: {Url}")]
    private static partial void LogSiteExploreFailed(ILogger logger, Exception ex, string Url);

    #endregion
}
