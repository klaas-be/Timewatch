using Jellyfin.Plugin.Timewatch.Domain;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Timewatch.Adapters;

/// <summary>
/// <see cref="IWatchedMarker"/> over Jellyfin's user data. One item id in, one played flag
/// written, no recursion and no bulk form — the End Card cannot reach a whole season or
/// series through this. It writes <see cref="IUserDataManager.SaveUserData"/> directly
/// rather than calling <c>BaseItem.MarkPlayed</c>, which on a folder would walk its
/// children; declining to have that code path at all is cheaper than guarding it.
/// Jellyfin's assemblies are absent from the test output by design, so this type is
/// verified by hand against a live 12.4 server.
/// </summary>
public sealed class JellyfinWatchedMarker : IWatchedMarker
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly ILogger<JellyfinWatchedMarker> _logger;

    public JellyfinWatchedMarker(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        ILogger<JellyfinWatchedMarker> logger)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool MarkWatched(Guid userId, Guid itemId)
    {
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            _logger.LogWarning("No user {UserId}; not marking {ItemId} watched.", userId, itemId);
            return false;
        }

        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            _logger.LogWarning("No item {ItemId}; nothing to mark watched.", itemId);
            return false;
        }

        // Only the two kinds that can be a Candidate. A season, a series or any other
        // folder id reaching this far is a bug upstream, and refusing it keeps the promise
        // that Dismiss can never mark more than the one item the user saw.
        if (item is not (Movie or Episode))
        {
            _logger.LogWarning(
                "Refusing to mark {ItemType} {ItemId} watched; Dismiss is for a single film or episode.",
                item.GetType().Name,
                itemId);
            return false;
        }

        var userData = _userDataManager.GetUserData(user, item);
        if (userData is null)
        {
            _logger.LogWarning("No user data for {ItemId} and user {UserId}.", itemId, userId);
            return false;
        }

        userData.Played = true;

        // Leaving a resume position behind would keep the item in Continue Watching, which
        // is half of what the user asked Dismiss to undo.
        userData.PlaybackPositionTicks = 0;

        _userDataManager.SaveUserData(
            user,
            item,
            userData,
            UserDataSaveReason.TogglePlayed,
            CancellationToken.None);

        _logger.LogDebug("Marked {ItemId} watched for user {UserId}.", itemId, userId);
        return true;
    }
}
