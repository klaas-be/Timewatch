using Jellyfin.Plugin.Timewatch.Api;
using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// Developer mode's whole observable effect: whether iterating on the injected script is a
/// page refresh or a release.
/// </summary>
public class ScriptCachePolicyTests
{
    private static readonly TimewatchSettings DeveloperMode =
        TimewatchSettings.Default with { DeveloperMode = true };

    [Fact]
    public void In_developer_mode_the_script_is_never_stored()
    {
        var policy = ScriptCachePolicy.For(DeveloperMode, "0.1.0.0");

        Assert.Contains("no-store", policy.CacheControl, StringComparison.Ordinal);
    }

    /// <summary>
    /// An ETag would let the browser revalidate its way back to the copy we are trying to
    /// replace, which is the one thing developer mode exists to prevent.
    /// </summary>
    [Fact]
    public void In_developer_mode_the_script_carries_no_validator()
    {
        Assert.Null(ScriptCachePolicy.For(DeveloperMode, "0.1.0.0").ETag);
    }

    [Fact]
    public void Otherwise_the_script_is_cacheable()
    {
        var policy = ScriptCachePolicy.For(TimewatchSettings.Default, "0.1.0.0");

        Assert.DoesNotContain("no-store", policy.CacheControl, StringComparison.Ordinal);
        Assert.Contains("max-age=", policy.CacheControl, StringComparison.Ordinal);
    }

    [Fact]
    public void Otherwise_the_validator_is_keyed_on_the_plugin_version()
    {
        Assert.NotEqual(
            ScriptCachePolicy.For(TimewatchSettings.Default, "0.1.0.0").ETag,
            ScriptCachePolicy.For(TimewatchSettings.Default, "0.1.0.1").ETag);
    }

    [Theory]
    [InlineData("0.1.0.0")]
    [InlineData("")]
    [InlineData("   ")]
    public void A_validator_is_always_a_quoted_non_empty_entity_tag(string version)
    {
        var etag = ScriptCachePolicy.For(TimewatchSettings.Default, version).ETag;

        Assert.NotNull(etag);
        Assert.StartsWith("\"", etag, StringComparison.Ordinal);
        Assert.EndsWith("\"", etag, StringComparison.Ordinal);
        Assert.True(etag.Length > 2, $"an entity tag needs content, got {etag}");
    }
}
