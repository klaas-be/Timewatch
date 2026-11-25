using Jellyfin.Plugin.Timewatch.Api;
using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The start page is a bookmarked URL with no cache-busting version in it, unlike the
/// injected script's. It must therefore be revalidated rather than stored for a year, or a
/// user who bookmarked it would keep the page of whichever plugin version they first
/// visited, forever.
/// </summary>
public class StartPageCachePolicyTests
{
    private static TimewatchSettings Settings(bool developerMode) =>
        TimewatchSettings.Default with { DeveloperMode = developerMode };

    [Fact]
    public void The_bookmarked_page_is_always_revalidated()
    {
        var policy = StartPageCachePolicy.For(Settings(developerMode: false), "1.2.3.4");

        Assert.Contains("no-cache", policy.CacheControl, StringComparison.Ordinal);
        Assert.DoesNotContain("immutable", policy.CacheControl, StringComparison.Ordinal);
    }

    [Fact]
    public void Revalidation_is_cheap_because_the_page_is_tagged_with_the_plugin_version()
    {
        var policy = StartPageCachePolicy.For(Settings(developerMode: false), "1.2.3.4");

        Assert.Equal("\"1.2.3.4\"", policy.ETag);
    }

    [Fact]
    public void An_unknown_plugin_version_still_produces_a_usable_entity_tag()
    {
        var policy = StartPageCachePolicy.For(Settings(developerMode: false), null);

        Assert.NotNull(policy.ETag);
        Assert.NotEqual("\"\"", policy.ETag);
    }

    /// <summary>
    /// An entity tag in developer mode would let the browser revalidate its way back to the
    /// copy the admin is replacing.
    /// </summary>
    [Fact]
    public void Developer_mode_stores_nothing_and_offers_nothing_to_validate()
    {
        var policy = StartPageCachePolicy.For(Settings(developerMode: true), "1.2.3.4");

        Assert.Contains("no-store", policy.CacheControl, StringComparison.Ordinal);
        Assert.Null(policy.ETag);
    }

    /// <summary>
    /// The page is served anonymously but is personal once it has the user's token in it;
    /// a shared proxy must not keep a copy.
    /// </summary>
    [Fact]
    public void No_shared_cache_keeps_a_copy()
    {
        Assert.Contains(
            "private",
            StartPageCachePolicy.For(Settings(developerMode: false), "1.2.3.4").CacheControl,
            StringComparison.Ordinal);
    }
}
