namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// What the store remembers of a user's most recent Watch Window: the window itself, and
/// whether the user cancelled it rather than letting it run out.
/// </summary>
/// <remarks>
/// The distinction exists for the client, not for the engine. A cancelled window and one
/// whose end time has passed are both absent as far as Suggestions are concerned, but they
/// mean opposite things to the user: one asked for normal Jellyfin behaviour back, the
/// other is the evening being over and the whole point of the stopping signal. Nothing can
/// tell them apart from the absence alone, so it is remembered here.
/// </remarks>
public sealed record WatchWindowRecord
{
    /// <summary>The window, whether or not it is still running.</summary>
    public required WatchWindow Window { get; init; }

    /// <summary>
    /// True when the user cancelled this window. A cancelled window is never active again,
    /// whatever its end time says; starting a new one replaces the record.
    /// </summary>
    public required bool Cancelled { get; init; }

    /// <summary>
    /// Whether this window is the user's active one at the given instant: not cancelled,
    /// and its end time not yet reached.
    /// </summary>
    public bool IsActiveAt(DateTimeOffset instant) => !Cancelled && !Window.HasExpiredAt(instant);
}
