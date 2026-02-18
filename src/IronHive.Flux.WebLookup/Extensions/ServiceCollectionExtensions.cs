using IronHive.Flux.WebLookup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IronHive.Flux.WebLookup.Extensions;

/// <summary>
/// WebLookup RAG 파이프라인 서비스 등록 확장 메서드
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// WebLookup RAG 파이프라인을 DI에 등록합니다.
    /// WebLookup의 WebSearchClient와 SiteExplorer가 이미 등록되어 있어야 합니다.
    /// </summary>
    /// <param name="services">서비스 컬렉션</param>
    /// <returns>서비스 컬렉션</returns>
    public static IServiceCollection AddWebLookupRagPipeline(
        this IServiceCollection services)
    {
        services.TryAddSingleton<WebLookupRagPipeline>();
        return services;
    }
}
