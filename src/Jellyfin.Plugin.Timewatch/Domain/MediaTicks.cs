namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// Runtimes and resume positions are stored as counts of 100-nanosecond ticks, nullable and
/// occasionally nonsense. Converting them into the <see cref="Candidate"/> contract — an
/// unknown runtime is null, a playback position is never negative — is the only decision in
/// the CandidateSource adapter, so it lives here where it can be tested.
/// </summary>
public static class MediaTicks
{
    /// <summary>
    /// A full runtime, or null when it is not known. Zero and negative counts mean "not
    /// known" rather than "zero seconds long": an item mid-scan reports no runtime, and the
    /// engine must be able to tell that apart from an item that would consume no time.
    /// </summary>
    public static TimeSpan? ToRuntime(long? ticks) =>
        ticks is > 0 ? TimeSpan.FromTicks(ticks.Value) : null;

    /// <summary>
    /// How far a user has played an item; zero when there is no resume data for it.
    /// Never negative, because <see cref="Candidate.EffectiveRuntime"/> subtracts it from
    /// the runtime and a negative position would price the item above its own length.
    /// </summary>
    public static TimeSpan ToPlaybackPosition(long? ticks) =>
        ticks is > 0 ? TimeSpan.FromTicks(ticks.Value) : TimeSpan.Zero;
}
