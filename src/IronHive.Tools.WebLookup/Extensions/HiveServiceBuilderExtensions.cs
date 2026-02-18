using IronHive.Abstractions.Tools;
using IronHive.Core.Tools;
using IronHive.Tools.WebLookup;
using IronHive.Tools.WebLookup.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebLookup;

namespace IronHive.Abstractions;

/// <summary>
/// WebLookup 도구를 IronHive에 연결하는 확장 메서드
/// </summary>
public static class WebLookupBuilderExtensions
{
    /// <summary>
    /// WebLookup 웹 검색/사이트 탐색 도구를 에이전트 FunctionTool로 등록합니다.
    /// WebLookup의 WebSearchClient와 SiteExplorer가 이미 DI에 등록되어 있어야 합니다.
    /// (services.AddWebLookup(...)로 등록)
    /// </summary>
    /// <param name="builder">HiveServiceBuilder 인스턴스</param>
    /// <param name="configure">옵션 구성 액션</param>
    /// <returns>빌더 인스턴스 (fluent chaining)</returns>
    public static IHiveServiceBuilder AddWebLookupTools(
        this IHiveServiceBuilder builder,
        Action<WebLookupToolOptions>? configure = null)
    {
        var options = new WebLookupToolOptions();
        configure?.Invoke(options);

        // 옵션과 도구 프로바이더 등록
        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddSingleton<WebLookupToolProvider>();

        // FunctionTool 등록
        var tools = FunctionToolFactory.CreateFrom<WebLookupToolProvider>();
        foreach (var tool in tools)
        {
            builder.AddTool(tool);
        }

        return builder;
    }

    /// <summary>
    /// WebLookup 도구를 IServiceProvider에서 ITool 컬렉션으로 가져옵니다.
    /// </summary>
    public static IEnumerable<ITool> GetWebLookupTools(this IServiceProvider provider)
    {
        var toolProvider = provider.GetService<WebLookupToolProvider>();
        if (toolProvider is null)
            return [];

        return FunctionToolFactory.CreateFrom(toolProvider.GetType());
    }
}
