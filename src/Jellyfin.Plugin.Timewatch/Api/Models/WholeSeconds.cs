namespace Jellyfin.Plugin.Timewatch.Api.Models;

/// <summary>
/// Durations cross the wire as whole seconds: the client labels them in minutes, and a
/// fractional second in JSON is noise that invites every reader to round it differently.
/// </summary>
internal static class WholeSeconds
{
    /// <summary>
    /// <paramref name="duration"/> truncated towards zero. Truncating rather than rounding
    /// keeps Remaining Time from ever being reported as more than it is, and keeps a
    /// Suggestion's Effective Runtime from ever being reported as more than it is either.
    /// </summary>
    public static int Of(TimeSpan duration) => (int)duration.TotalSeconds;
}
