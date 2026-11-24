namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// The store of Watch Windows: at most one active window per user, held wherever the host
/// keeps plugin state. This is a port — it names no Jellyfin type and no storage medium, so
/// the engine's tests can drive it from memory while the server runs it against a file.
/// </summary>
/// <remarks>
/// The store never reads the clock. <see cref="GetActiveWindowAsync"/> takes the instant to
/// judge expiry against, which is both what lets tests pin time and what makes expiry a
/// property of reading rather than of a sweeper that has to run.
/// </remarks>
public interface IWatchWindowStore
{
    /// <summary>
    /// The user's window if one is active at <paramref name="asOf"/>, otherwise
    /// <see langword="null"/>. A window whose end time has passed reads as absent.
    /// </summary>
    Task<WatchWindow?> GetActiveWindowAsync(
        Guid userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes <paramref name="window"/> the active window for its user, replacing any
    /// existing one so that Remaining Time is never ambiguous.
    /// </summary>
    Task StartWindowAsync(WatchWindow window, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the user's window, returning them to normal Jellyfin behaviour at once.
    /// Cancelling when nothing is active is not an error.
    /// </summary>
    Task CancelWindowAsync(Guid userId, CancellationToken cancellationToken = default);
}
