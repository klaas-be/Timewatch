using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Api.Models;

/// <summary>
/// One Suggestion on the wire: the item, and how it fits. Deliberately not a Jellyfin item
/// DTO — the End Card needs an id to play, a name to show and the two fit labels, and
/// anything else would be a second library query the client already knows how to make.
/// </summary>
public sealed record SuggestionView
{
    /// <summary>The Jellyfin item id: what the client plays, and what Dismiss names.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>The item's display name.</summary>
    public required string Title { get; init; }

    /// <summary>The owning series, or null for a film.</summary>
    public Guid? SeriesId { get; init; }

    /// <summary>The owning series' display name, or null for a film.</summary>
    public string? SeriesName { get; init; }

    /// <summary>
    /// Effective Runtime in whole seconds: what the user is committing to — the whole item
    /// when it is unwatched, what is left of it when it is part-watched.
    /// </summary>
    public required int EffectiveRuntimeSeconds { get; init; }

    /// <summary>
    /// Slack in whole seconds: the Remaining Time this was priced against minus the
    /// Effective Runtime. Negative means overshoot, which is offered only within the
    /// Overshoot Tolerance and which the End Card should label as such.
    /// </summary>
    public required int SlackSeconds { get; init; }

    /// <summary>
    /// Whether this is the episode Jellyfin itself would have played next, which the End
    /// Card may mark as the obvious choice.
    /// </summary>
    public required bool IsNativeNextUp { get; init; }

    /// <summary>Translates one <see cref="Suggestion"/>.</summary>
    public static SuggestionView Of(Suggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        return new SuggestionView
        {
            ItemId = suggestion.Candidate.ItemId,
            Title = suggestion.Candidate.Title,
            SeriesId = suggestion.Candidate.SeriesId,
            SeriesName = suggestion.Candidate.SeriesName,
            EffectiveRuntimeSeconds = WholeSeconds.Of(suggestion.EffectiveRuntime),
            SlackSeconds = WholeSeconds.Of(suggestion.Slack),
            IsNativeNextUp = suggestion.Candidate.IsNativeNextUp
        };
    }
}
