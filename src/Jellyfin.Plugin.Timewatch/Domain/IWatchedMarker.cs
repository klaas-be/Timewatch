namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// The write behind Dismiss: marking one item watched for one user, for real, so that it
/// stops being a Candidate here and also leaves Next Up and Continue Watching on every
/// client.
/// </summary>
/// <remarks>
/// A port, so free of Jellyfin types. It takes a single item id and has no bulk or
/// recursive form on purpose — the End Card must not be able to mark a season or a series
/// watched, and the narrowest possible interface is what guarantees that rather than a
/// check somewhere in an adapter. The rest of Dismiss — the dismissed Candidate dropping
/// out of the next Suggestion set and its slot being refilled — is engine behaviour and
/// follows from the item no longer being returned by <see cref="ICandidateSource"/>.
/// </remarks>
public interface IWatchedMarker
{
    /// <summary>
    /// Marks exactly the one item watched for the one user.
    /// </summary>
    /// <param name="userId">The user whose watched state changes.</param>
    /// <param name="itemId">The single item to mark; never a season or series id.</param>
    /// <returns>
    /// True when the flag was written. False when the user or the item is unknown, or when
    /// the id does not name a single playable item — so a caller can answer "not found"
    /// rather than reporting a success that did not happen.
    /// </returns>
    bool MarkWatched(Guid userId, Guid itemId);
}
