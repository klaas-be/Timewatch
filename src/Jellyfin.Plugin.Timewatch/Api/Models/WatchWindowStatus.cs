using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Api.Models;

/// <summary>
/// The answer to "where do I stand?": whether a Watch Window is active, when it ends and
/// how much of it is left. Free of Jellyfin types so that the translation out of the domain
/// is testable; see <c>ApiContractTests</c>.
/// </summary>
public sealed record WatchWindowStatus
{
    /// <summary>Whether a Watch Window is active for the acting user.</summary>
    public required bool Active { get; init; }

    /// <summary>
    /// The window's wall-clock end in UTC, or null when none is active. The client shows
    /// Remaining Time from this rather than counting down <see cref="RemainingSeconds"/>,
    /// so a sleeping tab cannot drift.
    /// </summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>
    /// Remaining Time in whole seconds, truncated so that it is never overstated; zero when
    /// no window is active.
    /// </summary>
    public required int RemainingSeconds { get; init; }

    /// <summary>
    /// The admin's default Watch Window duration, in minutes. It travels with the status so
    /// that the start page can prefill it: that page is deliberately usable by a non-admin,
    /// who cannot read the plugin configuration.
    /// </summary>
    public required int DefaultWindowMinutes { get; init; }

    /// <summary>
    /// Why the user's most recent Watch Window is no longer running:
    /// <see cref="Cancelled"/> when they cancelled it, <see cref="Expired"/> when its time
    /// ran out. Absent while a window is active, and absent for a user who has never
    /// started one.
    /// </summary>
    /// <remarks>
    /// It is the one thing a client cannot work out for itself: both endings read as no
    /// window at all. A client that was running a window shows the stopping signal for an
    /// expiry and goes quietly inert for a cancel, because cancelling is the user asking
    /// for normal Jellyfin behaviour back.
    /// </remarks>
    public string? EndedReason { get; init; }

    /// <summary><see cref="EndedReason"/> for a window the user cancelled.</summary>
    public const string Cancelled = "Cancelled";

    /// <summary><see cref="EndedReason"/> for a window whose end time has passed.</summary>
    public const string Expired = "Expired";

    /// <summary>
    /// The status of <paramref name="window"/> — or of having none — at an instant. Says
    /// nothing about an ending; <see cref="ForLatest"/> is the one that can.
    /// </summary>
    public static WatchWindowStatus For(
        WatchWindow? window,
        DateTimeOffset instant,
        TimewatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new WatchWindowStatus
        {
            Active = window is not null,
            EndsAt = window?.EndsAt,
            RemainingSeconds = window is null ? 0 : WholeSeconds.Of(window.RemainingTimeAt(instant)),
            DefaultWindowMinutes = (int)settings.DefaultWindowDuration.TotalMinutes
        };
    }

    /// <summary>
    /// The status of the user's most recent Watch Window at an instant, including why it
    /// ended if it has.
    /// </summary>
    public static WatchWindowStatus ForLatest(
        WatchWindowRecord? record,
        DateTimeOffset instant,
        TimewatchSettings settings)
    {
        var active = record is not null && record.IsActiveAt(instant) ? record.Window : null;

        return For(active, instant, settings) with
        {
            EndedReason = record is null || active is not null
                ? null
                : record.Cancelled ? Cancelled : Expired
        };
    }
}
