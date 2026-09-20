using System.Reflection;
using Iyu.Conventions.Testing;
using Xunit;

namespace IronHive.Flux.Tests;

/// <summary>
/// Every public option in this library is read by the library. An option nothing reads is a promise it does not keep:
/// a caller sets it, and nothing changes and nothing is reported. The roster fails both ways - a new unread option,
/// and a listed one that has since been wired - so each change is recorded on purpose.
/// </summary>
public class OptionsReachabilityRosterTests
{
    private static readonly Assembly[] Libraries =
    [
        Assembly.Load("IronHive.Flux"),
        Assembly.Load("IronHive.Flux.Core"),
        Assembly.Load("IronHive.Flux.Rag"),
    ];

    /// <summary>
    /// Options accepted as unread today. Shrink this list; never grow it silently.
    /// <para>
    /// This is the roster's opening baseline (2026-09-20), recorded as found rather than as judged:
    /// the first run reported 3 unread public options across 1 types, and none has been
    /// investigated, so none carries a reason of its own. Recording them is what makes the gate start
    /// green and makes the *next* unread option a failure instead of silently joining a crowd.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownUnread = new()
    {
        ["IronHive.Flux.Rag.Context.RagContextOptions"] =
        [
            "MaxResults", "MetadataFilter", "Query",
        ],
    };

    [Fact]
    public void EveryPublicOption_IsRead() =>
        OptionsReachability.Scan(Libraries, OptionsTypes.NamedWith("Options", "Config"))
            .ShouldMatchRoster(KnownUnread);
}
