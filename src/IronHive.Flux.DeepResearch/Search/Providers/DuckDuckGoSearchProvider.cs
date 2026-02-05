using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using IronHive.Flux.DeepResearch.Abstractions;
using IronHive.Flux.DeepResearch.Models.Search;
using IronHive.Flux.DeepResearch.Options;
using IronHive.Flux.DeepResearch.Search.Caching;
using Microsoft.Extensions.Logging;

namespace IronHive.Flux.DeepResearch.Search.Providers;

/// <summary>
/// DuckDuckGo Search Provider (무료, API 키 불필요)
/// DuckDuckGo HTML 검색 결과를 파싱하여 사용
/// </summary>
public class DuckDuckGoSearchProvider : ISearchProvider
{
    private readonly HttpClient _httpClient;
    private readonly ISearchResultCache _cache;
    private readonly DeepResearchOptions _options;
    private readonly ILogger<DuckDuckGoSearchProvider> _logger;

    private const string BaseUrl = "https://html.duckduckgo.com/html/";

    public string ProviderId => "duckduckgo";

    public SearchCapabilities Capabilities => SearchCapabilities.WebSearch;

    // 봇 보호 재시도 설정
    private const int MaxRetries = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);
    private static readonly Random _random = new();

    public DuckDuckGoSearchProvider(
        HttpClient httpClient,
        ISearchResultCache cache,
        DeepResearchOptions options,
        ILogger<DuckDuckGoSearchProvider> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options;
        _logger = logger;

        // 브라우저처럼 보이기 위한 헤더 설정
        ConfigureBrowserHeaders();
    }

    private void ConfigureBrowserHeaders()
    {
        var headers = _httpClient.DefaultRequestHeaders;

        if (!headers.Contains("User-Agent"))
        {
            // 최신 Chrome User-Agent
            headers.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
        }

        if (!headers.Contains("Accept"))
        {
            headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        }

        if (!headers.Contains("Accept-Language"))
        {
            headers.Add("Accept-Language", "en-US,en;q=0.9,ko;q=0.8");
        }

        if (!headers.Contains("Sec-Fetch-Dest"))
        {
            headers.Add("Sec-Fetch-Dest", "document");
            headers.Add("Sec-Fetch-Mode", "navigate");
            headers.Add("Sec-Fetch-Site", "none");
            headers.Add("Sec-Fetch-User", "?1");
        }
    }

    public async Task<SearchResult> SearchAsync(
        SearchQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. 캐시 확인
        var cacheKey = _cache.GenerateKey(query);
        if (_cache.TryGet(cacheKey, out var cached) && cached != null)
        {
            _logger.LogInformation("Returning cached DuckDuckGo result for: {Query}", query.Query);
            return cached;
        }

        _logger.LogInformation("Executing DuckDuckGo search: {Query}", query.Query);

        // 2. 재시도 로직이 포함된 검색 실행
        List<SearchSource> sources = [];
        var retryCount = 0;

        while (retryCount <= MaxRetries && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var (statusCode, html) = await ExecuteSearchRequestAsync(query.Query, cancellationToken);

                // HTTP 202 = 봇 보호 (Accepted but not processed)
                if (statusCode == System.Net.HttpStatusCode.Accepted)
                {
                    retryCount++;
                    if (retryCount <= MaxRetries)
                    {
                        var delay = RetryDelay + TimeSpan.FromMilliseconds(_random.Next(500, 1500));
                        _logger.LogWarning(
                            "DuckDuckGo returned 202 (bot protection), retry {Retry}/{Max} after {Delay}ms",
                            retryCount, MaxRetries, delay.TotalMilliseconds);
                        await Task.Delay(delay, cancellationToken);
                        continue;
                    }
                    _logger.LogWarning("DuckDuckGo bot protection: max retries exceeded");
                    break;
                }

                // HTML 파싱
                sources = ParseSearchResults(html, query.MaxResults);

                // 결과가 0개면 봇 보호일 가능성 (HTML은 200으로 왔지만 내용이 없음)
                if (sources.Count == 0 && retryCount < MaxRetries)
                {
                    retryCount++;
                    var delay = RetryDelay + TimeSpan.FromMilliseconds(_random.Next(500, 1500));
                    _logger.LogWarning(
                        "DuckDuckGo returned 0 results (possible bot protection), retry {Retry}/{Max} after {Delay}ms",
                        retryCount, MaxRetries, delay.TotalMilliseconds);
                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                break; // 성공하거나 재시도 한도 도달
            }
            catch (HttpRequestException ex)
            {
                retryCount++;
                if (retryCount <= MaxRetries)
                {
                    _logger.LogWarning(ex, "DuckDuckGo request failed, retry {Retry}/{Max}", retryCount, MaxRetries);
                    await Task.Delay(RetryDelay, cancellationToken);
                    continue;
                }
                throw;
            }
        }

        var result = new SearchResult
        {
            Query = query,
            Provider = ProviderId,
            Sources = sources,
            SearchedAt = DateTimeOffset.UtcNow
        };

        // 결과가 있을 때만 캐시 저장
        if (sources.Count > 0)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
        }

        _logger.LogInformation("DuckDuckGo search completed: {ResultCount} results after {Retries} retries",
            sources.Count, retryCount);

        return result;
    }

    private async Task<(System.Net.HttpStatusCode statusCode, string html)> ExecuteSearchRequestAsync(
        string query,
        CancellationToken cancellationToken)
    {
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("q", query),
            new KeyValuePair<string, string>("kl", "wt-wt"), // 전 세계 검색
        });

        var response = await _httpClient.PostAsync(BaseUrl, formContent, cancellationToken);

        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            _logger.LogError("DuckDuckGo search failed: {StatusCode}", response.StatusCode);
            throw new HttpRequestException($"DuckDuckGo search failed: {response.StatusCode}");
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return (response.StatusCode, html);
    }

    public async Task<IReadOnlyList<SearchResult>> SearchBatchAsync(
        IEnumerable<SearchQuery> queries,
        CancellationToken cancellationToken = default)
    {
        var queryList = queries.ToList();
        _logger.LogInformation("Executing DuckDuckGo batch search: {Count} queries (sequential to avoid bot protection)", queryList.Count);

        // 순차 실행 (DuckDuckGo는 병렬 요청 시 차단됨)
        var results = new List<SearchResult>();
        for (var i = 0; i < queryList.Count; i++)
        {
            var query = queryList[i];
            try
            {
                var result = await SearchAsync(query, cancellationToken);
                results.Add(result);

                // Rate limiting - 랜덤화된 지연으로 봇 탐지 회피
                if (i < queryList.Count - 1) // 마지막이 아니면
                {
                    var delay = 1000 + _random.Next(500, 1500); // 1.5~2.5초
                    await Task.Delay(delay, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DuckDuckGo search failed for: {Query}", query.Query);
                results.Add(new SearchResult
                {
                    Query = query,
                    Provider = ProviderId,
                    Sources = [],
                    SearchedAt = DateTimeOffset.UtcNow
                });
            }
        }

        return results;
    }

    private List<SearchSource> ParseSearchResults(string html, int maxResults)
    {
        var sources = new List<SearchSource>();

        // DuckDuckGo HTML 결과 파싱 (정규식 사용)
        // 결과는 <div class="result..."> 안에 있음
        var resultPattern = new Regex(
            @"<a\s+rel=""nofollow""\s+class=""result__a""\s+href=""([^""]+)""[^>]*>([^<]+)</a>.*?" +
            @"<a\s+class=""result__snippet""[^>]*>([^<]*(?:<[^>]+>[^<]*)*)</a>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        var matches = resultPattern.Matches(html);

        foreach (Match match in matches)
        {
            if (sources.Count >= maxResults) break;

            var url = ExtractUrl(match.Groups[1].Value);
            var title = HttpUtility.HtmlDecode(match.Groups[2].Value.Trim());
            var snippet = CleanSnippet(match.Groups[3].Value);

            if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(title))
            {
                sources.Add(new SearchSource
                {
                    Url = url,
                    Title = title,
                    Snippet = snippet,
                    Score = 1.0 - (sources.Count * 0.05) // 순서 기반 점수
                });
            }
        }

        // 대체 파싱 패턴 (결과가 없을 경우)
        if (sources.Count == 0)
        {
            var altPattern = new Regex(
                @"<a[^>]+href=""(https?://[^""]+)""[^>]*class=""[^""]*result[^""]*""[^>]*>([^<]+)</a>",
                RegexOptions.IgnoreCase);

            var altMatches = altPattern.Matches(html);
            foreach (Match match in altMatches)
            {
                if (sources.Count >= maxResults) break;

                var url = match.Groups[1].Value;
                var title = HttpUtility.HtmlDecode(match.Groups[2].Value.Trim());

                if (IsValidUrl(url) && !string.IsNullOrEmpty(title))
                {
                    sources.Add(new SearchSource
                    {
                        Url = url,
                        Title = title,
                        Score = 1.0 - (sources.Count * 0.05)
                    });
                }
            }
        }

        return sources;
    }

    private static string ExtractUrl(string ddgUrl)
    {
        // DuckDuckGo는 리다이렉트 URL을 사용
        // //duckduckgo.com/l/?uddg=https%3A%2F%2Fexample.com&... 형식
        if (ddgUrl.Contains("uddg="))
        {
            var match = Regex.Match(ddgUrl, @"uddg=([^&]+)");
            if (match.Success)
            {
                return HttpUtility.UrlDecode(match.Groups[1].Value);
            }
        }

        // 직접 URL인 경우
        if (ddgUrl.StartsWith("http"))
        {
            return ddgUrl;
        }

        return string.Empty;
    }

    private static string CleanSnippet(string html)
    {
        // HTML 태그 제거
        var text = Regex.Replace(html, @"<[^>]+>", " ");
        text = HttpUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
               !url.Contains("duckduckgo.com");
    }

    private static string GetRegion(string? language)
    {
        // DuckDuckGo 지역 코드
        return language?.ToLower() switch
        {
            "ko" => "kr-kr",
            "en" => "us-en",
            "ja" => "jp-jp",
            "zh" => "cn-zh",
            _ => "wt-wt" // 전 세계
        };
    }
}
