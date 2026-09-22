using AwesomeAssertions;
using IronHive.Core.Tools;
using IronHive.Tools.WebLookup.Extensions;
using Microsoft.Extensions.DependencyInjection;
using WebLookup;
using Xunit;

namespace IronHive.Flux.Tests.Core;

/// <summary>
/// The attach path had never attached anything: it passed the provider's Type where FunctionToolFactory expects the
/// instance. These pin that the two tools actually arrive.
/// </summary>
public class WebLookupToolExtensionsTests
{
    private static ServiceProvider BuildProvider(bool withTools)
    {
        var services = new ServiceCollection();
        services.AddWebLookup(_ => { });
        if (withTools)
            services.AddWebLookupTools();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void GetWebLookupTools_ReturnsWebSearchAndExploreSite()
    {
        using var provider = BuildProvider(withTools: true);

        provider.GetWebLookupTools().Select(t => t.UniqueName).Should().Contain(["func_web_search", "func_explore_site"]);
    }

    [Fact]
    public void AddWebLookupTools_OnAToolCollection_AttachesThem()
    {
        using var provider = BuildProvider(withTools: true);
        var tools = new ToolCollection();

        tools.AddWebLookupTools(provider);

        tools.Select(t => t.UniqueName).Should().Contain(["func_web_search", "func_explore_site"]);
    }

    [Fact]
    public void GetWebLookupTools_WithoutTheProviderRegistered_IsEmpty()
    {
        using var provider = BuildProvider(withTools: false);

        provider.GetWebLookupTools().Should().BeEmpty();
    }
}
