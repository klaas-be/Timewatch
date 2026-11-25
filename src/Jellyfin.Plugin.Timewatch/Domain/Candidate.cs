namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// A library item eligible to appear in a Suggestion for one user: a film or episode that
/// is unwatched or part-watched. Plain data, identified by <see cref="ItemId"/> — no
/// Jellyfin type is reachable from here, which is what keeps the SuggestionEngine
/// constructible in a unit test.
/// </summary>
public sealed record Candidate
{
    /// <summary>The Jellyfin item id, the only handle the adapters need.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>The item's display name, as shown on the End Card.</summary>
    public required string Title { get; init; }

    /// <summary>The owning series, or null for a film.</summary>
    public Guid? SeriesId { get; init; }

    /// <summary>The owning series' display name, or null for a film.</summary>
    public string? SeriesName { get; init; }

    /// <summary>
    /// Full runtime, or null when Jellyfin does not know it (RunTimeTicks is nullable).
    /// </summary>
    public TimeSpan? Runtime { get; init; }

    /// <summary>How far this user has already played the item; zero if untouched.</summary>
    public TimeSpan PlaybackPosition { get; init; }

    /// <summary>
    /// Whether this is the native next-up episode of its series — the episode Jellyfin
    /// would have played by itself.
    /// </summary>
    public bool IsNativeNextUp { get; init; }

    /// <summary>
    /// Effective Runtime: the time this candidate would actually consume for this user —
    /// full runtime for an unwatched item, runtime minus playback position for a
    /// part-watched one. Zero when the runtime is unknown or already consumed; never
    /// negative. Packing uses this, never <see cref="Runtime"/>.
    /// </summary>
    public TimeSpan EffectiveRuntime
    {
        get
        {
            var remaining = (Runtime ?? TimeSpan.Zero) - PlaybackPosition;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Whether the user has already started this item, which is what puts it in the
    /// in-progress ranking tier.
    /// </summary>
    public bool IsInProgress => PlaybackPosition > TimeSpan.Zero;

    /// <summary>
    /// The ranking tier this candidate belongs to.
    /// </summary>
    public CandidateTier Tier => IsInProgress
        ? CandidateTier.InProgress
        : IsNativeNextUp
            ? CandidateTier.NextUp
            : CandidateTier.Cold;
}
