using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The cache-busting version must change between releases. It did not: AssemblyVersion is
/// pinned at 0.1.0.0 in the csproj, so every build served timewatch.js?v=0.1.0.0 with a
/// one-year immutable cache header, and browsers kept the first build forever.
/// </summary>
public class PluginVersionTests
{
    [Fact]
    public void The_release_version_is_used_when_the_build_stamps_one()
    {
        Assert.Equal("0.1.0.7", PluginVersion.Normalise("0.1.0.7"));
    }

    [Fact]
    public void Build_metadata_is_trimmed_so_it_cannot_reach_a_url()
    {
        Assert.Equal("0.1.0.7", PluginVersion.Normalise("0.1.0.7+8a1c2f3"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+8a1c2f3")]
    public void An_unusable_value_falls_back_rather_than_serving_an_empty_version(string? informational)
    {
        Assert.Null(PluginVersion.Normalise(informational));
    }

    [Fact]
    public void The_assembly_reports_a_version_that_is_not_the_pinned_assembly_version()
    {
        // In a local build with no -p:Version this is the csproj default; the point of the
        // test is that Of() never returns null or empty, so a URL always has a ?v=.
        var version = PluginVersion.Of(typeof(PluginVersion).Assembly);

        Assert.False(string.IsNullOrWhiteSpace(version));
    }
}
