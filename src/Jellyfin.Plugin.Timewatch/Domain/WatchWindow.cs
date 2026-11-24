namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// A per-user, deadline-anchored span of time the user wants to fill with watching.
/// Created from a duration but held as an absolute end time, so anything that consumes
/// real time — a pause, a break, a phone call — shrinks the Remaining Time without the
/// plugin having to observe it.
/// </summary>
/// <remarks>
/// The window never reads the clock. Every time-dependent answer takes the instant as a
/// parameter, which is what lets the engine's tests pin time.
/// </remarks>
public sealed record WatchWindow
{
    private readonly DateTimeOffset _endsAt;

    /// <summary>The user this window belongs to; exactly one user per window.</summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// The wall-clock instant the window ends, always normalised to UTC so that a
    /// round-trip through JSON cannot change which instant it means.
    /// </summary>
    public required DateTimeOffset EndsAt
    {
        get => _endsAt;
        init => _endsAt = value.ToUniversalTime();
    }

    /// <summary>
    /// Starts a window of <paramref name="duration"/> for a user at a given instant.
    /// </summary>
    public static WatchWindow StartingAt(Guid userId, DateTimeOffset startedAt, TimeSpan duration) =>
        new() { UserId = userId, EndsAt = startedAt + duration };

    /// <summary>
    /// Remaining Time: the end time minus the given instant. Negative once the end time
    /// has passed, which is deliberate — the quantity a Suggestion has to fit is the
    /// signed difference, and the same subtraction gives Slack its sign.
    /// </summary>
    public TimeSpan RemainingTimeAt(DateTimeOffset instant) => EndsAt - instant.ToUniversalTime();

    /// <summary>
    /// Whether the end time has been reached at the given instant. An expired window is
    /// Spent, but a window with time left can be Spent too when nothing fits, so this is
    /// not the whole of the Spent question.
    /// </summary>
    public bool HasExpiredAt(DateTimeOffset instant) => RemainingTimeAt(instant) <= TimeSpan.Zero;
}
