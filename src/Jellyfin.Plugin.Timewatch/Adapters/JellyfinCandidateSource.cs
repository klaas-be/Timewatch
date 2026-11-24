using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.Timewatch.Domain;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Timewatch.Adapters;

/// <summary>
/// <see cref="ICandidateSource"/> over Jellyfin's library. Three queries and a copy into
/// <see cref="Candidate"/>: the eligible items, their resume positions in one batch, and the
/// set of episodes Jellyfin itself would play next. No ordering, no scoring, no dropping of
/// items beyond what the library query itself excludes — all of that is the engine's, which
/// is the only place it can be tested. Jellyfin's assemblies are absent from the test output
/// by design, so this type is verified by hand against a live 12.4 server.
/// </summary>
public sealed class JellyfinCandidateSource : ICandidateSource
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IUserManager _userManager;
    private readonly ITVSeriesManager _seriesManager;
    private readonly ILogger<JellyfinCandidateSource> _logger;

    public JellyfinCandidateSource(
        ILibraryManager libraryManager,
        IUserDataManager userDataManager,
        IUserManager userManager,
        ITVSeriesManager seriesManager,
        ILogger<JellyfinCandidateSource> logger)
    {
        _libraryManager = libraryManager;
        _userDataManager = userDataManager;
        _userManager = userManager;
        _seriesManager = seriesManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<Candidate> GetCandidates(Guid userId, TimewatchSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            _logger.LogWarning("No user {UserId}; no candidates.", userId);
            return [];
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery(user)
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Episode],

            // Played items are not Candidates at all; a part-watched item is still
            // IsPlayed = false, so this keeps the in-progress tier.
            IsPlayed = false,

            // Episodes Jellyfin knows about but does not have a file for.
            IsVirtualItem = false,

            Recursive = true,

            // Empty means every library the user can see, which is what Jellyfin does with
            // an empty AncestorIds, so the setting needs no translation.
            AncestorIds = [.. settings.LibraryIds],

            // Nothing here reads a DTO field, and asking for none keeps the query cheap.
            DtoOptions = new DtoOptions(false),
            EnableTotalRecordCount = false
        });

        // One round trip for the whole set. The per-item call is an N+1 over the library.
        var resumeData = _userDataManager.GetResumeUserDataBatch(items, user);
        var nativeNextUpIds = NativeNextUpIds(user);

        var candidates = new List<Candidate>(items.Count);
        foreach (var item in items)
        {
            var episode = item as Episode;

            candidates.Add(new Candidate
            {
                ItemId = item.Id,
                Title = item.Name ?? string.Empty,

                // Jellyfin spells "no series" as an empty guid; the Candidate spells it as
                // null, and the engine's one-per-series rule reads it.
                SeriesId = episode?.SeriesId is { } seriesId && !seriesId.Equals(Guid.Empty)
                    ? seriesId
                    : null,
                SeriesName = episode?.SeriesName,

                Runtime = MediaTicks.ToRuntime(item.RunTimeTicks),
                PlaybackPosition = MediaTicks.ToPlaybackPosition(
                    resumeData.TryGetValue(item.Id, out var resume)
                        ? resume.UserData?.PlaybackPositionTicks
                        : null),

                IsNativeNextUp = nativeNextUpIds.Contains(item.Id)
            });
        }

        _logger.LogDebug(
            "{Count} candidates for user {UserId}, {NextUpCount} of them native next-up.",
            candidates.Count,
            userId,
            nativeNextUpIds.Count);

        return candidates;
    }

    /// <summary>
    /// The episode Jellyfin would play next in each series the user is following, asked for
    /// in one query with no series filter. Marking them is all this does; whether a next-up
    /// episode outranks anything is the engine's call.
    /// </summary>
    private HashSet<Guid> NativeNextUpIds(User user)
    {
        var query = new NextUpQuery
        {
            User = user,

            // A resumable episode is next-up as far as Jellyfin is concerned, and the
            // engine wants to know that as well as that it is in progress.
            EnableResumable = true
        };

        var nextUp = _seriesManager.GetNextUp(query, new DtoOptions(false));
        return [.. nextUp.Items.Select(item => item.Id)];
    }
}
