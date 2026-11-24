namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// Where Candidates come from: everything in the user's libraries that is still worth
/// offering — unwatched or part-watched films and episodes, never a fully watched item.
/// </summary>
/// <remarks>
/// This is the port the SuggestionEngine consumes, so it is free of Jellyfin types and can
/// be faked in a unit test. It deliberately offers no filtering, ordering or paging: an
/// implementation returns the whole eligible set in no particular order and the engine makes
/// every choice about it — which tier a Candidate is in, which fit, which are Fillable and
/// which one wins. Anything an implementation decided for itself would be a decision no test
/// could reach.
/// </remarks>
public interface ICandidateSource
{
    /// <summary>
    /// Every Candidate for one user, drawn from <see cref="TimewatchSettings.LibraryIds"/>
    /// when that is non-empty and from every library the user can see when it is not.
    /// </summary>
    /// <param name="userId">The user whose watched state and libraries apply.</param>
    /// <param name="settings">The settings supplying the library restriction.</param>
    /// <returns>
    /// The eligible items, in no meaningful order; empty when the user is unknown or has
    /// nothing left to watch.
    /// </returns>
    IReadOnlyList<Candidate> GetCandidates(Guid userId, TimewatchSettings settings);
}
