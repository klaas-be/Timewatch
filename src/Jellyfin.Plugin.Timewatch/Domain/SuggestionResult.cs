namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// What the engine answers with: either Suggestions that fit, or Spent. The two are
/// separate types rather than a possibly-empty list, so no caller can mistake "nothing
/// fits" for "no answer yet" — a Spent window has to be reported to the user as a
/// stopping signal, not rendered as an empty End Card.
/// </summary>
public abstract record SuggestionResult
{
    private SuggestionResult()
    {
    }

    /// <summary>
    /// Suggestions the user can choose from, best first. Never empty; use
    /// <see cref="Offer"/> to turn a possibly-empty set into the right result.
    /// </summary>
    public sealed record Offered : SuggestionResult
    {
        /// <summary>Creates an offered result from a non-empty, already-ordered set.</summary>
        /// <exception cref="ArgumentException">If <paramref name="suggestions"/> is empty.</exception>
        public Offered(IReadOnlyList<Suggestion> suggestions)
        {
            ArgumentNullException.ThrowIfNull(suggestions);

            if (suggestions.Count == 0)
            {
                throw new ArgumentException(
                    "An offered result is never empty; a window with nothing that fits is Spent.",
                    nameof(suggestions));
            }

            Suggestions = suggestions;
        }

        /// <summary>The Suggestions, in the order the End Card should show them.</summary>
        public IReadOnlyList<Suggestion> Suggestions { get; }
    }

    /// <summary>
    /// The Watch Window's Remaining Time can no longer be filled — the end time has
    /// passed, or no Candidate fits. It offers nothing and cannot be extended.
    /// </summary>
    public sealed record Spent : SuggestionResult
    {
        /// <summary>The one Spent value; it carries no state.</summary>
        public static Spent Instance { get; } = new();
    }

    /// <summary>
    /// Offers <paramref name="suggestions"/>, or reports <see cref="Spent"/> when there
    /// are none. The only way the engine should build a result from a computed set.
    /// </summary>
    public static SuggestionResult Offer(IReadOnlyList<Suggestion> suggestions)
    {
        ArgumentNullException.ThrowIfNull(suggestions);

        return suggestions.Count == 0 ? Spent.Instance : new Offered(suggestions);
    }
}
