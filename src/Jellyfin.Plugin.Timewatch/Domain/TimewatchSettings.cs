namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// The settings the SuggestionEngine needs, free of Jellyfin types so that it can be
/// constructed in a unit test. Jellyfin's assemblies are referenced with
/// ExcludeAssets=runtime and are absent from the test output, so anything reachable from
/// the engine must stay on this side of the boundary.
/// </summary>
public sealed record TimewatchSettings
{
    /// <summary>The defaults, and the single source of truth for them.</summary>
    public static TimewatchSettings Default { get; } = new();

    /// <summary>Default Watch Window duration offered on the start page.</summary>
    public TimeSpan DefaultWindowDuration { get; init; } = TimeSpan.FromHours(2);

    /// <summary>How far a Suggestion may exceed the Remaining Time.</summary>
    public TimeSpan OvershootTolerance { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>How many Suggestions the End Card shows.</summary>
    public int SuggestionCount { get; init; } = 4;

    /// <summary>
    /// Libraries Candidates are drawn from. Empty means every library the user can see.
    /// </summary>
    public IReadOnlyList<Guid> LibraryIds { get; init; } = [];

    /// <summary>Serve the injected script uncached, for iterating on the End Card.</summary>
    public bool DeveloperMode { get; init; }
}
