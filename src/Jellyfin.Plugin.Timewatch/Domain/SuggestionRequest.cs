namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// Everything the <see cref="SuggestionEngine"/> needs for one answer. The instant is a
/// parameter rather than a clock read, so a test can pin time; the settings travel with
/// the request rather than with the engine, so an admin's change takes effect on the next
/// request without anyone holding a stale engine.
/// </summary>
public sealed record SuggestionRequest
{
    /// <summary>The user's active Watch Window.</summary>
    public required WatchWindow Window { get; init; }

    /// <summary>The instant the request is priced against — "now", supplied, never read.</summary>
    public required DateTimeOffset Instant { get; init; }

    /// <summary>The admin settings in force.</summary>
    public required TimewatchSettings Settings { get; init; }

    /// <summary>The Candidates to choose from; fully watched items are not among them.</summary>
    public IReadOnlyList<Candidate> Candidates { get; init; } = [];

    /// <summary>
    /// The series currently playing, when an episode is playing. The native next-up episode
    /// *of this series* is the one that takes the top slot, which is why it is a request
    /// input: <see cref="Candidate.IsNativeNextUp"/> is true for the next-up episode of
    /// every followed series, not just this one.
    /// </summary>
    public Guid? PlayingSeriesId { get; init; }
}
