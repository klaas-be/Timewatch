namespace Jellyfin.Plugin.Timewatch.Api;

/// <summary>
/// Where the standalone start page lives and how it is served. Free of Jellyfin types, so
/// the facts the endpoint depends on — the resource name, the path, the content type — are
/// testable without a server; the controller is only the adapter that writes them onto a
/// response.
/// </summary>
public static class StartPageResource
{
    /// <summary>
    /// The logical name the markup is embedded under: root namespace, folder, file name.
    /// </summary>
    public const string ResourceName = "Jellyfin.Plugin.Timewatch.Api.startPage.html";

    /// <summary>
    /// The path the page is served at under the server's base URL — the URL a user
    /// bookmarks. Routing is case-insensitive, so <c>/Timewatch/start</c> resolves too.
    /// </summary>
    public const string Path = "/Timewatch/Start";

    /// <summary>The response content type. Charset stated, because the page is not ASCII.</summary>
    public const string ContentType = "text/html; charset=utf-8";

    /// <summary>
    /// The page's markup, read out of the assembly.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The build stopped embedding the resource — which would make the only surface that
    /// starts a Watch Window a 404.
    /// </exception>
    public static string ReadMarkup()
    {
        using var stream = typeof(StartPageResource).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The Timewatch start page is missing from the plugin assembly ({ResourceName}).");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
