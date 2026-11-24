using Jellyfin.Plugin.Timewatch.Domain;
using Jellyfin.Plugin.Timewatch.Injection;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The anonymous script endpoint cannot be tested here — it is a Jellyfin controller — but
/// what it reaches for can be. A renamed file or a dropped <c>EmbeddedResource</c> entry
/// would make the End Card silently absent on a server, with a 404 behind a
/// <c>&lt;script src&gt;</c> nobody looks at.
/// </summary>
public class InjectedScriptResourceTests
{
    /// <summary>
    /// The same literal <c>TimewatchScriptController</c> passes to
    /// <c>GetManifestResourceStream</c>. It is repeated rather than referenced because the
    /// controller derives from a Jellyfin type and so cannot be loaded in this assembly.
    /// </summary>
    private const string ResourceName = "Jellyfin.Plugin.Timewatch.Injection.timewatch.js";

    [Fact]
    public void The_injected_script_is_embedded_under_the_name_the_endpoint_asks_for()
    {
        Assert.Contains(ResourceName, typeof(TimewatchSettings).Assembly.GetManifestResourceNames());
    }

    [Fact]
    public void The_injected_script_is_not_empty()
    {
        using var stream = typeof(TimewatchSettings).Assembly.GetManifestResourceStream(ResourceName);

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0, "the served script would be a zero-byte file");
    }

    /// <summary>
    /// The path the index.html transformation injects and the path the endpoint is served
    /// at have to agree on the file name, or the tag resolves to nothing.
    /// </summary>
    [Fact]
    public void The_script_is_served_at_the_path_the_injected_tag_points_at()
    {
        Assert.EndsWith(
            "/" + ResourceName.Split('.')[^2] + "." + ResourceName.Split('.')[^1],
            WebClientPatches.ScriptPath,
            StringComparison.Ordinal);
    }
}
