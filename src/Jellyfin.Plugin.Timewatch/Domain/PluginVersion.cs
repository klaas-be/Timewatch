using System.Reflection;

namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// The version string used to cache-bust the files we serve.
/// </summary>
/// <remarks>
/// <para>
/// NOT <c>Assembly.GetName().Version</c>. That is <c>AssemblyVersion</c>, which is pinned
/// in the csproj so that assembly binding stays stable, and so it reads 0.1.0.0 in every
/// release. Serving the script as <c>timewatch.js?v=0.1.0.0</c> with
/// <c>Cache-Control: immutable, max-age=1 year</c> meant a browser pinned the first build
/// it ever saw and never picked up another, however many times the plugin was updated.
/// </para>
/// <para>
/// The release version arrives as <c>AssemblyInformationalVersion</c>, because the
/// packaging step passes <c>-p:Version=</c> to <c>dotnet publish</c>. Source-link builds
/// append <c>+&lt;commit&gt;</c> to it, which is noise in a URL, so it is trimmed.
/// </para>
/// </remarks>
public static class PluginVersion
{
    /// <summary>Reads the cache-busting version from an assembly's attributes.</summary>
    /// <param name="assembly">The assembly to read.</param>
    public static string Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return Normalise(informational) ?? assembly.GetName().Version?.ToString() ?? "0";
    }

    /// <summary>
    /// Trims build metadata and rejects an empty or absent value, so that a caller falls
    /// back rather than serving "?v=".
    /// </summary>
    /// <param name="informational">The raw informational version, if any.</param>
    public static string? Normalise(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational))
        {
            return null;
        }

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        var trimmed = (plus < 0 ? informational : informational[..plus]).Trim();

        return trimmed.Length == 0 ? null : trimmed;
    }
}
