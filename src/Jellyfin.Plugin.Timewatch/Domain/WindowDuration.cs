namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// Turns "how long have I got?" — a client's optional minute count — into the duration a
/// <see cref="WatchWindow"/> is started with. The only decision in starting a Window, kept
/// here rather than in the controller so that it is testable without a server.
/// </summary>
public static class WindowDuration
{
    /// <summary>
    /// The longest Window that will be accepted. A Watch Window is an evening, and a
    /// deadline further away than a day cannot be one; refusing it also keeps a typo in a
    /// bookmarked URL from parking a window in the next decade.
    /// </summary>
    public static TimeSpan Longest { get; } = TimeSpan.FromDays(1);

    /// <summary>
    /// The duration to start a Window with, or <see langword="null"/> when the request does
    /// not name one a Window could have — which a caller should answer as a bad request
    /// rather than quietly substituting something.
    /// </summary>
    /// <param name="minutes">
    /// The requested length in minutes, or <see langword="null"/> to take the admin's
    /// default. The client asks in minutes because that is what it offers the user.
    /// </param>
    /// <param name="settings">The settings supplying the default.</param>
    public static TimeSpan? Resolve(int? minutes, TimewatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var duration = minutes is { } requested
            ? TimeSpan.FromMinutes(requested)
            : settings.DefaultWindowDuration;

        return duration > TimeSpan.Zero && duration <= Longest ? duration : null;
    }
}
