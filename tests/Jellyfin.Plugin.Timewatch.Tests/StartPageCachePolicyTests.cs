using Jellyfin.Plugin.Timewatch.Api;
using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The start page is served from a bookmarkable, constant URL. It used to be revalidated
/// against a version entity tag, which failed on a live server: the version was frozen at
/// 0.1.0.0 across several releases, so every conditional request answered 304 and the
/// browser kept rendering a page from before the suggestions list existed.
/// </summary>
public class StartPageCachePolicyTests
{
    [Fact]
    public void The_page_is_never_stored_so_nothing_can_be_served_stale()
    {
        var policy = StartPageCachePolicy.For(TimewatchSettings.Default, "1.2.3.4");

        Assert.Contains("no-store", policy.CacheControl, StringComparison.Ordinal);
    }

    /// <summary>
    /// No entity tag at all: a validator on a constant URL has to be right every release,
    /// and once it is wrong the page is stuck until someone clears their browser.
    /// </summary>
    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData("")]
    [InlineData(null)]
    public void The_page_offers_nothing_to_revalidate_against(string? version)
    {
        var policy = StartPageCachePolicy.For(TimewatchSettings.Default, version);

        Assert.Null(policy.ETag);
    }

    [Fact]
    public void A_shared_proxy_must_not_hold_it()
    {
        var policy = StartPageCachePolicy.For(TimewatchSettings.Default, "1.2.3.4");

        Assert.Contains("private", policy.CacheControl, StringComparison.Ordinal);
    }

    [Fact]
    public void Developer_mode_changes_nothing_because_it_is_already_uncached()
    {
        var developer = StartPageCachePolicy.For(
            TimewatchSettings.Default with { DeveloperMode = true }, "1.2.3.4");
        var normal = StartPageCachePolicy.For(TimewatchSettings.Default, "1.2.3.4");

        Assert.Equal(normal.CacheControl, developer.CacheControl);
        Assert.Null(developer.ETag);
    }
}
