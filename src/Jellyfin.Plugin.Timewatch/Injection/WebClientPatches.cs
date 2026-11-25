using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Timewatch.Injection;

/// <summary>
/// The string surgery File Transformation calls back into, as pure functions of their
/// input. Free of Jellyfin types on purpose: Jellyfin's assemblies are referenced with
/// ExcludeAssets=runtime and are absent from the test output, so everything a unit test
/// touches has to stay on this side of the boundary.
/// </summary>
public static class WebClientPatches
{
    /// <summary>
    /// Marks our tag so that patching already-patched content stays idempotent, whatever
    /// the configured base URL or cache-busting version happens to be.
    /// </summary>
    public const string ScriptElementId = "timewatch-injected-script";

    /// <summary>
    /// The name jellyfin-web looks up on <c>window</c> to construct our client plugin, and
    /// therefore the entry added to the web client's <c>plugins</c> array.
    /// </summary>
    public const string ClientPluginName = "Timewatch";

    /// <summary>The path the injected script is served from, under the server's base URL.</summary>
    public const string ScriptPath = "/Timewatch/timewatch.js";

    private static string _scriptUrl = BuildScriptUrl(null, "0");

    /// <summary>
    /// Records the <c>src</c> the <c>index.html</c> callback should inject. The callbacks
    /// are invoked by name through reflection and so cannot be handed any state; the
    /// registration task calls this before registering them.
    /// </summary>
    public static void Configure(string scriptUrl) => _scriptUrl = scriptUrl;

    /// <summary>
    /// The <c>index.html</c> callback. Signature is the contract: public static, one
    /// payload parameter, returns the whole new file contents.
    /// </summary>
    public static string TransformIndexHtml(FileTransformationPayload payload) =>
        InjectScriptTag(payload.Contents ?? string.Empty, _scriptUrl);

    /// <summary>The <c>config.json</c> callback. Same contract.</summary>
    public static string TransformConfigJson(FileTransformationPayload payload) =>
        AddClientPlugin(payload.Contents ?? string.Empty, ClientPluginName);

    /// <summary>
    /// Builds the script's <c>src</c>: the server's configured base URL (the whole static
    /// file pipeline is mapped under it), our path, and <c>?v=</c> for cache busting.
    /// </summary>
    public static string BuildScriptUrl(string? baseUrl, string version)
    {
        var prefix = (baseUrl ?? string.Empty).Trim().Trim('/');
        if (prefix.Length > 0)
        {
            prefix = "/" + prefix;
        }

        return $"{prefix}{ScriptPath}?v={Uri.EscapeDataString(version)}";
    }

    /// <summary>
    /// Puts the script tag immediately before the last <c>&lt;/body&gt;</c>, or appends it
    /// when there is none. Returns the content unchanged if it is already patched.
    /// </summary>
    public static string InjectScriptTag(string contents, string scriptUrl)
    {
        if (contents.Contains(ScriptElementId, StringComparison.Ordinal))
        {
            return contents;
        }

        var tag = $"<script id=\"{ScriptElementId}\" defer src=\"{scriptUrl.Replace("&", "&amp;", StringComparison.Ordinal)}\"></script>";

        var closingBody = contents.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return closingBody < 0
            ? contents + tag
            : contents[..closingBody] + tag + contents[closingBody..];
    }

    /// <summary>
    /// Appends <paramref name="pluginName"/> to the web client's <c>plugins</c> array,
    /// which is what makes jellyfin-web look the name up on <c>window</c> and construct our
    /// class with <c>playbackManager</c>. Already present, or unparseable: unchanged.
    /// </summary>
    public static string AddClientPlugin(string contents, string pluginName)
    {
        JObject config;
        try
        {
            config = JObject.Parse(contents);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return contents;
        }

        if (config["plugins"] is not JArray plugins)
        {
            plugins = [];
            config["plugins"] = plugins;
        }
        else if (plugins.Any(entry => IsName(entry, pluginName)))
        {
            return contents;
        }

        plugins.Add(pluginName);
        return config.ToString();
    }

    /// <summary>
    /// Whether an entry of the <c>plugins</c> array is exactly this name. The type is
    /// checked rather than cast: another plugin or a future jellyfin-web may put an object
    /// or a number in that array, and casting one to a string throws past the
    /// <see cref="Newtonsoft.Json.JsonException"/> this method promises to absorb — out of
    /// File Transformation's serving path, where it is a broken web client rather than an
    /// unpatched one.
    /// </summary>
    private static bool IsName(JToken entry, string pluginName) =>
        entry.Type == JTokenType.String
        && string.Equals(entry.Value<string>(), pluginName, StringComparison.Ordinal);
}
