using Jellyfin.Plugin.Timewatch.Api.Models;
using Jellyfin.Plugin.Timewatch.Domain;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Timewatch.Api;

/// <summary>
/// Timewatch's authenticated API: start, read and cancel a Watch Window, read Suggestions,
/// Dismiss an item. A thin adapter — it reads the acting user off the request, fetches the
/// settings once, and hands everything to the <see cref="SuggestionEngine"/>, the store and
/// the ports. Every decision it looks like it makes lives behind the domain boundary, where
/// it can be tested without a Jellyfin server.
/// </summary>
/// <remarks>
/// <para>
/// The route is declared explicitly because Jellyfin's base route convention is
/// <c>[controller]</c>, which would make this <c>/Timewatch</c> only by accident of the
/// type name.
/// </para>
/// <para>
/// <c>[Authorize]</c> is on the controller, not on each action: there is no global
/// authorisation fallback in Jellyfin, so an action that forgot it would be anonymous. The
/// one anonymous endpoint Timewatch has lives on
/// <see cref="TimewatchScriptController"/> instead of being an exception here.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("Timewatch")]
[Produces("application/json")]
public class TimewatchController : ControllerBase
{
    private readonly IWatchWindowStore _windowStore;
    private readonly ICandidateSource _candidateSource;
    private readonly IWatchedMarker _watchedMarker;
    private readonly IAuthorizationContext _authorizationContext;
    private readonly ILogger<TimewatchController> _logger;

    public TimewatchController(
        IWatchWindowStore windowStore,
        ICandidateSource candidateSource,
        IWatchedMarker watchedMarker,
        IAuthorizationContext authorizationContext,
        ILogger<TimewatchController> logger)
    {
        _windowStore = windowStore;
        _candidateSource = candidateSource;
        _watchedMarker = watchedMarker;
        _authorizationContext = authorizationContext;
        _logger = logger;
    }

    /// <summary>
    /// The acting user's Watch Window: whether one is active, when it ends and how much of
    /// it is left. Also carries the admin's default duration, so the start page can prefill
    /// it without being able to read the plugin configuration.
    /// </summary>
    [HttpGet("WatchWindow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WatchWindowStatus>> GetWatchWindow(CancellationToken cancellationToken)
    {
        if (await ActingUserIdAsync().ConfigureAwait(false) is not { } userId)
        {
            return Unauthorized();
        }

        var settings = Settings();
        var now = DateTimeOffset.UtcNow;
        var record = await _windowStore
            .GetLatestWindowAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        // The most recent window rather than the active one: when none is active, this is
        // where a client learns whether that is a cancel or the time having run out.
        return WatchWindowStatus.ForLatest(record, now, settings);
    }

    /// <summary>
    /// Starts a Watch Window for the acting user, replacing any window they already have so
    /// that Remaining Time is never ambiguous.
    /// </summary>
    /// <param name="minutes">
    /// How long the user has, in minutes — minutes because that is the unit the start page
    /// offers. Omit it to take the admin's configured default.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <remarks>
    /// A query parameter rather than a JSON body on purpose. Jellyfin's serializer is
    /// configured case-sensitively, so a body of <c>{ "minutes": 120 }</c> would bind to
    /// nothing and silently start a window of the default length instead; query binding is
    /// case-insensitive, and a start page then needs no request body at all.
    /// </remarks>
    [HttpPost("WatchWindow")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WatchWindowStatus>> StartWatchWindow(
        [FromQuery] int? minutes,
        CancellationToken cancellationToken)
    {
        if (await ActingUserIdAsync().ConfigureAwait(false) is not { } userId)
        {
            return Unauthorized();
        }

        var settings = Settings();

        if (WindowDuration.Resolve(minutes, settings) is not { } duration)
        {
            return BadRequest(
                $"A Watch Window lasts between a minute and {WindowDuration.Longest.TotalHours} hours.");
        }

        var now = DateTimeOffset.UtcNow;
        var window = WatchWindow.StartingAt(userId, now, duration);

        await _windowStore.StartWindowAsync(window, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug(
            "Started a {Duration} Watch Window for user {UserId}, ending {EndsAt}",
            duration,
            userId,
            window.EndsAt);

        return WatchWindowStatus.For(window, now, settings);
    }

    /// <summary>
    /// Cancels the acting user's Watch Window, returning them to normal Jellyfin behaviour
    /// at once. Cancelling when nothing is active is not an error.
    /// </summary>
    [HttpDelete("WatchWindow")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CancelWatchWindow(CancellationToken cancellationToken)
    {
        if (await ActingUserIdAsync().ConfigureAwait(false) is not { } userId)
        {
            return Unauthorized();
        }

        await _windowStore.CancelWindowAsync(userId, cancellationToken).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// The Suggestions that fit what is left of the acting user's Watch Window, best first,
    /// or Spent when nothing does.
    /// </summary>
    /// <param name="playingItemId">
    /// The item playing right now, when something is; omit it otherwise. It is never
    /// offered back — the teaser fires while the item still has forty seconds left, and a
    /// part-watched item is otherwise a perfectly good Candidate.
    /// </param>
    /// <param name="playingSeriesId">
    /// The series playing right now, when an episode is; omit it otherwise. Its native
    /// next-up episode takes the first slot whenever it fits.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// 404 when no Watch Window is active — which is also how a client whose window has
    /// just run out learns that it has: an expired window reads as absent.
    /// </returns>
    [HttpGet("Suggestions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SuggestionsResponse>> GetSuggestions(
        [FromQuery] Guid? playingItemId,
        [FromQuery] Guid? playingSeriesId,
        CancellationToken cancellationToken)
    {
        if (await ActingUserIdAsync().ConfigureAwait(false) is not { } userId)
        {
            return Unauthorized();
        }

        // Once per request, and the same instance to the candidate source and the engine:
        // an admin saving the dashboard mid-request must not give one answer two settings.
        var settings = Settings();
        var now = DateTimeOffset.UtcNow;

        var window = await _windowStore
            .GetActiveWindowAsync(userId, now, cancellationToken)
            .ConfigureAwait(false);

        if (window is null)
        {
            return NotFound();
        }

        // The seed comes from the instant, so two End Cards in one evening shuffle equally
        // fitting Suggestions differently instead of showing the same four things.
        var engine = new SuggestionEngine(SuggestionSeed.FromInstant(now));

        var result = engine.Suggest(new SuggestionRequest
        {
            Window = window,
            Instant = now,
            Settings = settings,
            Candidates = _candidateSource.GetCandidates(userId, settings),
            PlayingItemId = Identifying(playingItemId),
            PlayingSeriesId = Identifying(playingSeriesId)
        });

        return SuggestionsResponse.For(result, window.RemainingTimeAt(now));
    }

    /// <summary>
    /// Dismisses one Suggestion: marks exactly that item watched for the acting user, for
    /// real, so it leaves Next Up and Continue Watching everywhere and stops being a
    /// Candidate. The freed slot refills by itself on the next read, because the item is no
    /// longer a Candidate.
    /// </summary>
    /// <param name="itemId">The single film or episode to dismiss; never a season or series.</param>
    /// <returns>
    /// 404 when the id does not name a single film or episode the user can have — the
    /// narrow <see cref="IWatchedMarker"/> port is what guarantees an End Card cannot mark a
    /// whole series watched, so refusing here is a report, not a guard.
    /// </returns>
    [HttpPost("Suggestions/{itemId}/Dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DismissSuggestion([FromRoute] Guid itemId)
    {
        if (await ActingUserIdAsync().ConfigureAwait(false) is not { } userId)
        {
            return Unauthorized();
        }

        return _watchedMarker.MarkWatched(userId, itemId) ? NoContent() : NotFound();
    }

    /// <summary>
    /// The user the request is authenticated as, or null when it is not authenticated as
    /// one — an API key, for instance, is authenticated but is nobody. Never a user id from
    /// the client: a Watch Window belongs to one account, and whose it is cannot be a
    /// parameter.
    /// </summary>
    private async Task<Guid?> ActingUserIdAsync()
    {
        var authorization = await _authorizationContext
            .GetAuthorizationInfo(Request)
            .ConfigureAwait(false);

        return authorization.UserId.Equals(Guid.Empty) ? null : authorization.UserId;
    }

    /// <summary>
    /// The settings in force for this request, read once. Reading them per request rather
    /// than holding them is what makes an admin's change take effect on the next request.
    /// </summary>
    private static TimewatchSettings Settings() =>
        TimewatchPlugin.Instance?.Configuration.ToSettings() ?? TimewatchSettings.Default;

    /// <summary>
    /// Jellyfin spells "no id" as an empty guid and clients pass it around, so an empty one
    /// means the same as omitting the parameter.
    /// </summary>
    private static Guid? Identifying(Guid? id) =>
        id is { } value && !value.Equals(Guid.Empty) ? value : null;
}
