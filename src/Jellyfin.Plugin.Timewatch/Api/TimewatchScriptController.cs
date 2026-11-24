using Jellyfin.Plugin.Timewatch.Domain;
using Jellyfin.Plugin.Timewatch.Injection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Timewatch.Api;

/// <summary>
/// Serves the injected client script out of the assembly. The one anonymous endpoint
/// Timewatch has, and the reason it is anonymous: the tag our index.html transformation
/// injects is a <c>&lt;script src&gt;</c>, and a script tag carries no access token.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="TimewatchController"/> so that the authorisation story is
/// visible in the type: everything on that controller is <c>[Authorize]</c>, everything on
/// this one is anonymous, and neither can drift into the other by someone adding an action.
/// There is no global authorisation fallback in Jellyfin, so an action without
/// <c>[Authorize]</c> is already anonymous; <see cref="AllowAnonymousAttribute"/> is here to
/// say that it is meant to be.
/// </para>
/// <para>
/// The script is an embedded resource rather than a file in the plugin folder: Jellyfin's
/// per-plugin load context loads every DLL it finds beside the plugin, so the release zip
/// is kept to exactly the plugin assembly and meta.json.
/// </para>
/// </remarks>
[ApiController]
[Route("Timewatch")]
public class TimewatchScriptController : ControllerBase
{
    private const string ResourceName = "Jellyfin.Plugin.Timewatch.Injection.timewatch.js";
    private const string ContentType = "application/javascript; charset=utf-8";

    private readonly ILogger<TimewatchScriptController> _logger;

    public TimewatchScriptController(ILogger<TimewatchScriptController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// The injected client script, served at <see cref="WebClientPatches.ScriptPath"/>.
    /// </summary>
    /// <remarks>
    /// The <c>?v=</c> the injected tag carries is deliberately not a parameter here: it
    /// exists to give each build its own URL in the browser's cache, and the response is
    /// the same whatever it says. Caching is decided by
    /// <see cref="TimewatchSettings.DeveloperMode"/> through
    /// <see cref="ScriptCachePolicy"/> — nothing stored while an admin is iterating on the
    /// End Card, a year of cache keyed on the plugin version otherwise.
    /// </remarks>
    [HttpGet("timewatch.js")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult GetScript()
    {
        var settings = TimewatchPlugin.Instance?.Configuration.ToSettings() ?? TimewatchSettings.Default;
        var version = GetType().Assembly.GetName().Version?.ToString();
        var policy = ScriptCachePolicy.For(settings, version);

        var stream = GetType().Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            // Only reachable if the build stopped embedding the resource, which would make
            // the whole client side of the plugin silently absent.
            _logger.LogError(
                "Timewatch's injected script is missing from the plugin assembly ({Resource}); "
                + "the End Card cannot load",
                ResourceName);
            return NotFound();
        }

        Response.Headers.CacheControl = policy.CacheControl;

        // Passing the entity tag rather than only setting the header lets the framework
        // answer a conditional request with 304 for us.
        return policy.ETag is { } etag
            ? File(stream, ContentType, lastModified: null, entityTag: EntityTagHeaderValue.Parse(etag))
            : File(stream, ContentType);
    }
}
