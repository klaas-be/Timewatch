using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Api;

/// <summary>
/// How the standalone start page is cached: it is not. Deliberately unlike
/// <see cref="ScriptCachePolicy"/>, where the URL carries <c>?v=</c> so a copy can be kept
/// for a year and never be stale.
/// </summary>
/// <remarks>
/// <para>
/// This page is a URL a user bookmarks, so its address never changes, and revalidating it
/// against a version entity tag went wrong in practice: the version was frozen at 0.1.0.0
/// for several releases, every conditional request answered 304, and browsers rendered a
/// start page from before the suggestions list existed. The version is fixed now, but a
/// constant URL validated against a value that has to be right every single time is a trap
/// worth removing rather than re-arming.
/// </para>
/// <para>
/// The page is around 30 KB of markup on a local network and is fetched when someone
/// deliberately opens it. Not caching it costs nothing worth having.
/// </para>
/// <para>
/// A pure value rather than header writing, so it is testable on this side of the Jellyfin
/// boundary; the controller only copies it onto the response.
/// </para>
/// </remarks>
public sealed record StartPageCachePolicy
{
    /// <summary>The <c>Cache-Control</c> header value.</summary>
    public required string CacheControl { get; init; }

    /// <summary>
    /// The <c>ETag</c> header value, already quoted, or null when the response must not be
    /// validated at all.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// The policy for serving the page under <paramref name="settings"/>, for a plugin build
    /// identified by <paramref name="version"/>.
    /// </summary>
    public static StartPageCachePolicy For(TimewatchSettings settings, string? version)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // The same either way; DeveloperMode and version are kept in the signature because
        // the controller has them and a future policy may want them again.
        _ = settings;
        _ = version;

        // private: a user's own page, which no shared proxy has business holding.
        // no-store: never written to a cache, so there is nothing to serve stale and
        // nothing to revalidate. No entity tag, deliberately — see the remarks.
        return new StartPageCachePolicy { CacheControl = "private, no-store, no-cache, must-revalidate" };
    }
}
