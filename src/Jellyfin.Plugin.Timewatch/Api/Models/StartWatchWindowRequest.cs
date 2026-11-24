namespace Jellyfin.Plugin.Timewatch.Api.Models;

/// <summary>
/// The body of a request to start a Watch Window. The body may be omitted entirely, which
/// means "the default I was offered".
/// </summary>
public class StartWatchWindowRequest
{
    /// <summary>
    /// How long the user has, in minutes — minutes because that is the unit the start page
    /// offers. Omitted or null takes the admin's configured default.
    /// </summary>
    public int? Minutes { get; set; }
}
