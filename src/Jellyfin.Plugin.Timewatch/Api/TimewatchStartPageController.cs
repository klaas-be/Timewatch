using Jellyfin.Plugin.Timewatch.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Timewatch.Api;

/// <summary>
/// Serves the standalone start page out of the assembly: the one surface that starts a
/// Watch Window in v1, bookmarkable, and usable by a non-admin — which is why it is this
/// rather than the dashboard configuration page.
/// </summary>
/// <remarks>
/// <para>
/// The markup is served anonymously. It carries no user data and makes no decisions: it is
/// a form and a countdown, and every call it then makes is to
/// <see cref="TimewatchController"/>, which is <c>[Authorize]</c>. Requiring
/// authentication for the document itself would not work anyway — a bookmarked URL opened
/// in a fresh tab sends no <c>Authorization</c> header, so the user would get a 401 instead
/// of a page, with nowhere to type anything.
/// </para>
/// <para>
/// On its own controller for the same reason as <see cref="TimewatchScriptController"/>:
/// the authorisation story is visible in the type, and no authenticated action can drift
/// into an anonymous controller by someone adding a method. There is no global
/// authorisation fallback in Jellyfin, so an action without <c>[Authorize]</c> is already
/// anonymous; <see cref="AllowAnonymousAttribute"/> is here to say that it is meant to be.
/// </para>
/// <para>
/// An embedded resource rather than a file in the plugin folder: Jellyfin's per-plugin load
/// context loads every DLL it finds beside the plugin, so the release zip is kept to
/// exactly the plugin assembly and meta.json.
/// </para>
/// </remarks>
[ApiController]
[Route("Timewatch")]
public class TimewatchStartPageController : ControllerBase
{
    private readonly ILogger<TimewatchStartPageController> _logger;

    public TimewatchStartPageController(ILogger<TimewatchStartPageController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The start page, served at <see cref="StartPageResource.Path"/>.
    /// </summary>
    [HttpGet("Start")]
    [AllowAnonymous]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetStartPage()
    {
        var settings = TimewatchPlugin.Instance?.Configuration.ToSettings() ?? TimewatchSettings.Default;
        var version = PluginVersion.Of(GetType().Assembly);
        var policy = StartPageCachePolicy.For(settings, version);

        var markup = GetType().Assembly.GetManifestResourceStream(StartPageResource.ResourceName);
        if (markup is null)
        {
            // Only reachable if the build stopped embedding the resource, which would leave
            // the plugin with no way to start a Watch Window at all.
            _logger.LogError(
                "Timewatch's start page is missing from the plugin assembly ({Resource}); "
                + "there is no other surface that starts a Watch Window",
                StartPageResource.ResourceName);
            return NotFound();
        }

        Response.Headers.CacheControl = policy.CacheControl;

        // Passing the entity tag rather than only setting the header lets the framework
        // answer a conditional request with 304 for us.
        return policy.ETag is { } etag
            ? File(markup, StartPageResource.ContentType, lastModified: null, entityTag: EntityTagHeaderValue.Parse(etag))
            : File(markup, StartPageResource.ContentType);
    }
}
