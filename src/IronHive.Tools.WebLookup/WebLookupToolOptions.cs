namespace IronHive.Tools.WebLookup;

/// <summary>
/// WebLookup 에이전트 도구 옵션
/// </summary>
public class WebLookupToolOptions
{
    /// <summary>
    /// 기본 검색 결과 최대 수. 기본값: 10
    /// </summary>
    public int DefaultMaxResults { get; set; } = 10;

    /// <summary>
    /// sitemap 스트리밍 시 최대 엔트리 수. 기본값: 500
    /// </summary>
    public int MaxSitemapEntries { get; set; } = 500;

    /// <summary>
    /// 도구 실행 타임아웃 (초). 기본값: 60
    /// </summary>
    public int ToolTimeout { get; set; } = 60;
}
