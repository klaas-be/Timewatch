using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Api;

/// <summary>
/// How the standalone start page is cached. Deliberately not
/// <see cref="ScriptCachePolicy"/>: the injected script is fetched from a URL carrying
/// <c>?v=</c>, so a copy of it can be kept for a year and still never be stale, while the
/// start page is a URL a user bookmarks. Stored for a year it would survive every plugin
/// update, so it is revalidated instead — cheaply, against the plugin version.
/// </summary>
/// <remarks>
/// A pure value rather than header writing, so it is testable on this side of the Jellyfin
/// boundary; the controller only copies it onto the response.
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

        if (settings.DeveloperMode)
        {
            return new StartPageCachePolicy { CacheControl = "no-store, no-cache, must-revalidate" };
        }

        var tag = string.IsNullOrWhiteSpace(version) ? "0" : version.Trim();

        return new StartPageCachePolicy
        {
            // private: the markup is the same for everyone, but it is a user's own page and
            // a shared proxy has no business holding it. no-cache asks for revalidation on
            // every visit, which the entity tag then answers with a 304.
            CacheControl = "private, no-cache",

            // Quoted, because an unquoted entity tag is not one.
            ETag = $"\"{tag}\""
        };
    }
}
