using System.Globalization;
using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Api;

/// <summary>
/// How the injected script is cached, which is the whole of what
/// <see cref="TimewatchSettings.DeveloperMode"/> does: off, the script is a versioned,
/// long-lived asset; on, the browser is told to keep nothing, so iterating on the End Card
/// is a page refresh instead of a release.
/// </summary>
/// <remarks>
/// A pure value rather than header writing, so it is testable on this side of the Jellyfin
/// boundary; the controller only copies it onto the response.
/// </remarks>
public sealed record ScriptCachePolicy
{
    /// <summary>
    /// A year, which is what a URL keyed on a version is worth. The <c>?v=</c> the injected
    /// tag carries changes with every build, so a cached copy is only ever reused for the
    /// build it belongs to.
    /// </summary>
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(365);

    /// <summary>The <c>Cache-Control</c> header value.</summary>
    public required string CacheControl { get; init; }

    /// <summary>
    /// The <c>ETag</c> header value, already quoted, or null when the response must not be
    /// validated at all — an ETag in developer mode would let the browser revalidate its
    /// way back to the copy we are replacing.
    /// </summary>
    public string? ETag { get; init; }

    /// <summary>
    /// The policy for serving the script under <paramref name="settings"/>, for a plugin
    /// build identified by <paramref name="version"/>.
    /// </summary>
    public static ScriptCachePolicy For(TimewatchSettings settings, string? version)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.DeveloperMode)
        {
            return new ScriptCachePolicy { CacheControl = "no-store, no-cache, must-revalidate" };
        }

        var tag = string.IsNullOrWhiteSpace(version) ? "0" : version.Trim();

        return new ScriptCachePolicy
        {
            CacheControl = string.Create(
                CultureInfo.InvariantCulture,
                $"public, max-age={(long)CacheLifetime.TotalSeconds}, immutable"),

            // Quoted, because an unquoted entity tag is not one.
            ETag = $"\"{tag}\""
        };
    }
}
