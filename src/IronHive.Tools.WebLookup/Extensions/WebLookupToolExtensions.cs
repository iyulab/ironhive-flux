using IronHive.Abstractions.Tools;
using IronHive.Core.Tools;
using IronHive.Tools.WebLookup;
using IronHive.Tools.WebLookup.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IronHive.Tools.WebLookup.Extensions;

/// <summary>
/// WebLookup 도구를 IronHive에 연결하는 확장 메서드.
/// </summary>
/// <remarks>
/// 등록(DI)과 부착(도구 컬렉션)이 분리돼 있다 — 0.15.0에서 도구 합성이 hive 빌더에서 떨어져 나왔기 때문이다.
/// <c>IHiveServiceBuilder.Services</c>와 <c>.AddTool</c>이 사라진 자리를 각각
/// <see cref="IServiceCollection"/>과 <see cref="IToolCollection"/>이 받는다.
/// </remarks>
public static class WebLookupToolExtensions
{
    /// <summary>
    /// WebLookup 도구 프로바이더와 옵션을 DI에 등록합니다.
    /// WebLookup의 <c>WebSearchClient</c>/<c>SiteExplorer</c>가 이미 등록돼 있어야 합니다
    /// (<c>services.AddWebLookup(...)</c>).
    /// </summary>
    public static IServiceCollection AddWebLookupTools(
        this IServiceCollection services,
        Action<WebLookupToolOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new WebLookupToolOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<WebLookupToolProvider>();

        return services;
    }

    /// <summary>
    /// DI에 등록된 WebLookup 프로바이더로부터 FunctionTool을 만들어 도구 컬렉션에 부착합니다.
    /// <see cref="AddWebLookupTools(IServiceCollection, Action{WebLookupToolOptions})"/>가 선행돼야 합니다.
    /// </summary>
    public static IToolCollection AddWebLookupTools(
        this IToolCollection tools,
        IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(provider);

        foreach (var tool in provider.GetWebLookupTools())
        {
            tools.Add(tool);
        }

        return tools;
    }

    /// <summary>
    /// WebLookup 도구를 <see cref="IServiceProvider"/>에서 <see cref="ITool"/> 컬렉션으로 가져옵니다.
    /// 프로바이더가 등록돼 있지 않으면 빈 시퀀스를 반환합니다.
    /// </summary>
    public static IEnumerable<ITool> GetWebLookupTools(this IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var toolProvider = provider.GetService<WebLookupToolProvider>();
        if (toolProvider is null)
            return [];

        return FunctionToolFactory.CreateFrom(toolProvider.GetType());
    }
}
