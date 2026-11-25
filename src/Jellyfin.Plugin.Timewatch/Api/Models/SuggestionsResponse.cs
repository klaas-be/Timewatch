using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Api.Models;

/// <summary>
/// The End Card's content: either Suggestions in the engine's order, or Spent. The two are
/// one response with a flag rather than two status codes, because the End Card renders both
/// — a Spent window is a stopping signal to show, not an error.
/// </summary>
public sealed record SuggestionsResponse
{
    /// <summary>
    /// The Watch Window's Remaining Time can no longer be filled. There are no Suggestions
    /// and there is no way to extend it; the End Card says the time is up.
    /// </summary>
    public required bool Spent { get; init; }

    /// <summary>Remaining Time in whole seconds, the quantity the Suggestions were priced against.</summary>
    public required int RemainingSeconds { get; init; }

    /// <summary>The Suggestions, in the order the End Card should show them. Empty when Spent.</summary>
    public IReadOnlyList<SuggestionView> Suggestions { get; init; } = [];

    /// <summary>
    /// Translates one engine answer, priced against <paramref name="remainingTime"/>.
    /// </summary>
    public static SuggestionsResponse For(SuggestionResult result, TimeSpan remainingTime)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SuggestionsResponse
        {
            Spent = result is SuggestionResult.Spent,
            RemainingSeconds = WholeSeconds.Of(remainingTime),
            Suggestions = result is SuggestionResult.Offered offered
                ? [.. offered.Suggestions.Select(SuggestionView.Of)]
                : []
        };
    }
}
