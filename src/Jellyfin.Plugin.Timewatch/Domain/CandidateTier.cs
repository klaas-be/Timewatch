namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// The ranking tiers a Candidate falls into, best first — the numeric order is the
/// ranking order, so the engine can sort on it directly. The engine lifts the next-up
/// episode of the series currently playing above all of these; that depends on what is
/// playing and so is not a property of the Candidate.
/// </summary>
public enum CandidateTier
{
    /// <summary>Already started: finishing things is easier than starting things.</summary>
    InProgress = 0,

    /// <summary>The native next-up episode of a series the user is following.</summary>
    NextUp = 1,

    /// <summary>A cold start: an unwatched film or a series pilot.</summary>
    Cold = 2
}
