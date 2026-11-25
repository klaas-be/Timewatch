using Jellyfin.Plugin.Timewatch.Injection;

namespace Jellyfin.Plugin.Timewatch.Tests;

public class InjectScriptTagTests
{
    private const string Html =
        "<html><head><title>Jellyfin</title></head><body><div id=\"app\"></div></body></html>";

    [Fact]
    public void PutsTheScriptTagImmediatelyBeforeTheClosingBodyTag()
    {
        var patched = WebClientPatches.InjectScriptTag(Html, "/Timewatch/timewatch.js?v=1");

        Assert.Contains("<script", patched, StringComparison.Ordinal);
        Assert.Contains("src=\"/Timewatch/timewatch.js?v=1\"", patched, StringComparison.Ordinal);
        Assert.EndsWith("></script></body></html>", patched, StringComparison.Ordinal);
    }

    [Fact]
    public void LeavesAlreadyPatchedContentAlone()
    {
        var once = WebClientPatches.InjectScriptTag(Html, "/Timewatch/timewatch.js?v=1");

        var twice = WebClientPatches.InjectScriptTag(once, "/Timewatch/timewatch.js?v=2");

        Assert.Equal(once, twice);
    }

    [Fact]
    public void AppendsTheTagWhenThereIsNoClosingBodyTag()
    {
        var patched = WebClientPatches.InjectScriptTag("<html>", "/Timewatch/timewatch.js");

        Assert.StartsWith("<html><script", patched, StringComparison.Ordinal);
    }
}

public class AddClientPluginTests
{
    private const string ConfigJson = """
        {
            "multiserver": false,
            "plugins": [
                "playAccessValidation/plugin",
                "experimentalWarnings/plugin"
            ]
        }
        """;

    private static IReadOnlyList<string> PluginsOf(string json) =>
        Newtonsoft.Json.Linq.JObject.Parse(json)["plugins"]!.Select(t => (string)t!).ToList();

    [Fact]
    public void AppendsExactlyOneEntryAndKeepsTheExistingOnes()
    {
        var patched = WebClientPatches.AddClientPlugin(ConfigJson, "Timewatch");

        Assert.Equal(
            ["playAccessValidation/plugin", "experimentalWarnings/plugin", "Timewatch"],
            PluginsOf(patched));
    }

    [Fact]
    public void LeavesAlreadyPatchedContentAlone()
    {
        var once = WebClientPatches.AddClientPlugin(ConfigJson, "Timewatch");

        var twice = WebClientPatches.AddClientPlugin(once, "Timewatch");

        Assert.Single(PluginsOf(twice), p => p == "Timewatch");
        Assert.Equal(once, twice);
    }

    [Fact]
    public void CreatesThePluginsArrayWhenTheConfigHasNone()
    {
        var patched = WebClientPatches.AddClientPlugin("{\"multiserver\": false}", "Timewatch");

        Assert.Equal(["Timewatch"], PluginsOf(patched));
    }

    [Fact]
    public void ReturnsUnparseableContentUnchangedRatherThanThrowing()
    {
        Assert.Equal("not json", WebClientPatches.AddClientPlugin("not json", "Timewatch"));
    }

    /// <summary>
    /// Another plugin, or a future jellyfin-web, may put something other than a string in
    /// the array. Reading it as one threw into File Transformation's serving path, which
    /// is a broken web client rather than an unpatched one.
    /// </summary>
    [Theory]
    [InlineData("""{ "plugins": [ { "name": "somePlugin" } ] }""")]
    [InlineData("""{ "plugins": [ 7, null, [ "nested" ] ] }""")]
    public void AddsThePluginAlongsideEntriesThatAreNotStrings(string json)
    {
        var patched = WebClientPatches.AddClientPlugin(json, "Timewatch");

        var plugins = Newtonsoft.Json.Linq.JObject.Parse(patched)["plugins"]!;
        Assert.Contains(
            plugins,
            entry => entry.Type == Newtonsoft.Json.Linq.JTokenType.String
                && string.Equals((string?)entry, "Timewatch", StringComparison.Ordinal));
    }
}

public class CallbackTests
{
    [Fact]
    public void TransformIndexHtmlInjectsTheConfiguredScriptUrl()
    {
        WebClientPatches.Configure("/jellyfin/Timewatch/timewatch.js?v=7");

        var patched = WebClientPatches.TransformIndexHtml(
            new FileTransformationPayload { Contents = "<body></body>" });

        Assert.Contains("src=\"/jellyfin/Timewatch/timewatch.js?v=7\"", patched, StringComparison.Ordinal);
    }

    [Fact]
    public void TransformConfigJsonRegistersTheClientPluginUnderItsWindowName()
    {
        var patched = WebClientPatches.TransformConfigJson(
            new FileTransformationPayload { Contents = "{\"plugins\": []}" });

        Assert.Contains(WebClientPatches.ClientPluginName, patched, StringComparison.Ordinal);
    }

    [Fact]
    public void CallbacksTreatMissingContentsAsEmptyRatherThanThrowing()
    {
        Assert.NotNull(WebClientPatches.TransformIndexHtml(new FileTransformationPayload()));
        Assert.Equal(string.Empty, WebClientPatches.TransformConfigJson(new FileTransformationPayload()));
    }
}

public class BuildScriptUrlTests
{
    [Theory]
    [InlineData(null, "/Timewatch/timewatch.js?v=0.1.0.0")]
    [InlineData("", "/Timewatch/timewatch.js?v=0.1.0.0")]
    [InlineData("/jellyfin", "/jellyfin/Timewatch/timewatch.js?v=0.1.0.0")]
    [InlineData("jellyfin", "/jellyfin/Timewatch/timewatch.js?v=0.1.0.0")]
    [InlineData("/jellyfin/", "/jellyfin/Timewatch/timewatch.js?v=0.1.0.0")]
    public void HangsThePathOffTheConfiguredBaseUrl(string? baseUrl, string expected)
    {
        Assert.Equal(expected, WebClientPatches.BuildScriptUrl(baseUrl, "0.1.0.0"));
    }

    [Fact]
    public void EscapesTheCacheBustingVersion()
    {
        Assert.Equal(
            "/Timewatch/timewatch.js?v=a%20b%26c",
            WebClientPatches.BuildScriptUrl(null, "a b&c"));
    }
}
