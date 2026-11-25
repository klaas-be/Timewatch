using Jellyfin.Plugin.Timewatch.Api;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The start page itself is markup and cannot be exercised here — there is no browser and
/// no JavaScript runner in this test project. What can be is everything the page's endpoint
/// reaches for: that the markup is in the assembly under the name the controller asks for,
/// that it is served at the path the page tells users to bookmark, and that the markup still
/// names the routes, the query parameter and the PascalCase fields the API actually has.
/// Those last ones are the drift this ticket is most exposed to: a camelCase field name or a
/// JSON body would fail silently in a browser nobody is watching.
/// </summary>
public class StartPageResourceTests
{
    [Fact]
    public void The_markup_is_embedded_under_the_name_the_endpoint_asks_for()
    {
        Assert.Contains(
            StartPageResource.ResourceName,
            typeof(StartPageResource).Assembly.GetManifestResourceNames());
    }

    [Fact]
    public void The_markup_reads_as_an_html_document()
    {
        var markup = StartPageResource.ReadMarkup();

        Assert.StartsWith("<!DOCTYPE html>", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_page_is_served_from_under_the_plugins_own_route()
    {
        Assert.StartsWith("/Timewatch/", StartPageResource.Path, StringComparison.Ordinal);
    }

    /// <summary>
    /// The three endpoints the page is allowed to know about, and nothing from the parts of
    /// the API that are out of scope for v1.
    /// </summary>
    [Theory]
    [InlineData("Timewatch/WatchWindow")]
    [InlineData("minutes=")]
    public void The_page_calls_the_watch_window_endpoint_the_way_the_api_expects(string expected)
    {
        Assert.Contains(expected, StartPageResource.ReadMarkup(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Jellyfin's serializer has no naming policy, so the status fields are PascalCase and
    /// case-sensitive. Reading <c>remainingSeconds</c> yields undefined and a page that
    /// looks like it has no Watch Window — a silent failure in a browser nobody is watching.
    /// </summary>
    [Theory]
    [InlineData("Active", "active")]
    [InlineData("EndsAt", "endsAt")]
    [InlineData("RemainingSeconds", "remainingSeconds")]
    [InlineData("DefaultWindowMinutes", "defaultWindowMinutes")]
    public void The_page_reads_the_status_fields_in_pascal_case(string pascal, string camel)
    {
        var markup = StartPageResource.ReadMarkup();

        Assert.Contains("." + pascal, markup, StringComparison.Ordinal);
        Assert.DoesNotContain("." + camel, markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// The page is outside jellyfin-web, so <c>window.ApiClient</c> does not exist on it and
    /// every call has to carry the token itself. It takes that token from the credentials
    /// the web client stores on this same origin, which is only reusable while the key and
    /// the <c>MediaBrowser</c> authorization scheme are the ones it actually uses.
    /// </summary>
    [Theory]
    [InlineData("jellyfin_credentials")]
    [InlineData("Authorization")]
    [InlineData("MediaBrowser ")]
    public void The_page_authenticates_with_the_web_clients_stored_token(string expected)
    {
        Assert.Contains(expected, StartPageResource.ReadMarkup(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Out of scope for v1 — the page must not have grown a bundle view or a suggestions
    /// list by accident of copying from the End Card.
    /// </summary>
    [Theory]
    [InlineData("Timewatch/Suggestions")]
    [InlineData("Dismiss")]
    public void The_page_leaves_suggestions_alone(string absent)
    {
        Assert.DoesNotContain(absent, StartPageResource.ReadMarkup(), StringComparison.Ordinal);
    }
}
