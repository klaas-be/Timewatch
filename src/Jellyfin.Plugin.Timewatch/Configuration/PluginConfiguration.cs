using Jellyfin.Plugin.Timewatch.Domain;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Timewatch.Configuration;

/// <summary>
/// The five admin settings, in the shape Jellyfin's XML serializer needs. Watch Window
/// state is NOT kept here: it lives as JSON in the plugin data folder, because
/// configuration is for settings edited in the dashboard.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Default Watch Window duration offered on the start page, in minutes.</summary>
    public int DefaultWindowMinutes { get; set; } =
        (int)TimewatchSettings.Default.DefaultWindowDuration.TotalMinutes;

    /// <summary>How far a Suggestion may exceed the Remaining Time, in minutes.</summary>
    public int OvershootToleranceMinutes { get; set; } =
        (int)TimewatchSettings.Default.OvershootTolerance.TotalMinutes;

    /// <summary>How many Suggestions the End Card shows.</summary>
    public int SuggestionCount { get; set; } = TimewatchSettings.Default.SuggestionCount;

    /// <summary>
    /// Libraries that Candidates are drawn from, as item id strings. Empty means all
    /// libraries the user can see.
    /// </summary>
    public string[] LibraryIds { get; set; } = [];

    /// <summary>Serve the injected script uncached, for iterating on the End Card.</summary>
    public bool DeveloperMode { get; set; } = TimewatchSettings.Default.DeveloperMode;

    /// <summary>Crosses the boundary into the Jellyfin-free domain.</summary>
    public TimewatchSettings ToSettings() => new()
    {
        DefaultWindowDuration = TimeSpan.FromMinutes(DefaultWindowMinutes),
        OvershootTolerance = TimeSpan.FromMinutes(OvershootToleranceMinutes),
        SuggestionCount = SuggestionCount,
        LibraryIds = [.. LibraryIds.Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)],
        DeveloperMode = DeveloperMode
    };
}
