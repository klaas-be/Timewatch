namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// The seed a single request hands the <see cref="SuggestionEngine"/>. It is derived from
/// the instant rather than fixed, because a fixed seed would make the shuffling of equally
/// fitting Candidates a permanent order: the same four items every End Card, all evening.
/// </summary>
/// <remarks>
/// Derived from the instant rather than taken from <see cref="Random"/> so that one
/// request is internally consistent — the same instant priced twice answers the same way,
/// which is what lets a caller reproduce an answer and a test assert on one.
/// </remarks>
public static class SuggestionSeed
{
    /// <summary>
    /// A seed for the request being priced at <paramref name="instant"/>. Equal instants
    /// give equal seeds; instants a millisecond apart give different ones.
    /// </summary>
    /// <param name="instant">The instant the request is priced against.</param>
    public static int FromInstant(DateTimeOffset instant)
    {
        var ticks = instant.UtcTicks;

        // Fold the high half into the low half rather than truncating: a 64-bit tick count
        // truncated to 32 bits keeps only the bottom ~7 minutes of resolution before it
        // wraps, which is fine, but folding keeps the whole instant in play for free.
        return unchecked((int)(ticks ^ (ticks >>> 32)));
    }
}
