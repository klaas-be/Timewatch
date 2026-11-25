using System.Text;
using Jellyfin.Plugin.Timewatch.Domain;
using Jellyfin.Plugin.Timewatch.Injection;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// Guards over the injected script as shipped. Its behaviour needs a browser and a
/// Jellyfin and is verified by hand; what can be checked here are the few textual facts
/// that make the difference between the End Card working on a server and failing silently,
/// and the one approach that was explicitly rejected.
/// </summary>
/// <remarks>
/// The script's own decisions — the teaser moment, what a 404 means, how a Suggestion's fit
/// is worded — are unit tested with node under <c>tests/js</c>, which is where the real
/// coverage of this file lives.
/// </remarks>
public class InjectedScriptContractTests
{
    private const string ResourceName = "Jellyfin.Plugin.Timewatch.Injection.timewatch.js";

    private static string Script()
    {
        using var stream = typeof(TimewatchSettings).Assembly.GetManifestResourceStream(ResourceName);
        Assert.NotNull(stream);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// The one name jellyfin-web looks up. If the assignment is renamed while
    /// <see cref="WebClientPatches.ClientPluginName"/> is not, the client plugin is never
    /// constructed and the plugin is silently inert.
    /// </summary>
    [Fact]
    public void The_script_registers_the_client_plugin_factory_under_the_injected_name()
    {
        Assert.Contains(
            "window." + WebClientPatches.ClientPluginName + " = async",
            Script(),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// It is served as a plain <c>&lt;script src&gt;</c>, not a module, so module syntax
    /// would be a parse error that takes the whole file with it.
    /// </summary>
    [Theory]
    [InlineData("\nimport ")]
    [InlineData("\nexport ")]
    public void The_script_stays_a_classic_script(string moduleSyntax)
    {
        Assert.DoesNotContain(moduleSyntax, Script(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Recognising minified identifiers is the approach this design exists to avoid: it
    /// needs a per-release table of mangled names and breaks on every jellyfin-web
    /// rebuild. The client-plugin seam hands us real objects instead.
    /// </summary>
    [Theory]
    [InlineData("__webpack_require__")]
    [InlineData("webpackChunk")]
    public void The_script_never_reaches_into_the_minified_bundle(string bundleIdentifier)
    {
        Assert.DoesNotContain(bundleIdentifier, Script(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The two suppression switches, which live in different places and are easy to lose
    /// in a refactor: the per-user overlay flag in localStorage, and playbackManager's
    /// in-session auto-advance field.
    /// </summary>
    [Theory]
    [InlineData("-enableNextVideoInfoOverlay")]
    [InlineData("_playNextAfterEnded")]
    public void The_script_holds_both_native_suppression_switches(string switchName)
    {
        Assert.Contains(switchName, Script(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Without <c>playingItemId</c> the engine ranks the episode already on screen as a
    /// part-watched Candidate and the teaser suggests it back to the user.
    /// </summary>
    [Theory]
    [InlineData("playingItemId")]
    [InlineData("playingSeriesId")]
    public void The_script_tells_the_engine_what_is_playing(string parameterName)
    {
        Assert.Contains(parameterName, Script(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Jellyfin's serializer is case-sensitive and writes PascalCase, so a camelCase read
    /// of a response field yields undefined rather than an error.
    /// </summary>
    [Theory]
    [InlineData("Suggestions")]
    [InlineData("EffectiveRuntimeSeconds")]
    [InlineData("SlackSeconds")]
    [InlineData("RemainingSeconds")]
    [InlineData("Spent")]
    public void The_script_reads_the_response_in_the_case_jellyfin_writes_it(string property)
    {
        Assert.Contains("." + property, Script(), StringComparison.Ordinal);
    }

    /// <summary>
    /// A Spent Watch Window offers nothing and cannot be extended; that absence is the
    /// feature. A control that hands time back would undo the whole point.
    /// </summary>
    [Fact]
    public void The_script_offers_no_way_to_extend_a_spent_window()
    {
        var labels = System.Text.RegularExpressions.Regex
            .Matches(Script(), @"button\('([^']*)'")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(labels);
        Assert.DoesNotContain(
            labels,
            label => label.Contains("extend", StringComparison.OrdinalIgnoreCase)
                || label.Contains("more time", StringComparison.OrdinalIgnoreCase));
    }
}
