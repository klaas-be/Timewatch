namespace Jellyfin.Plugin.Timewatch.Domain;

/// <summary>
/// The whole of Timewatch's domain logic: fit, Fillable, ranking and Suggestion count.
/// Pure — no I/O, no clock, no unseeded randomness — which is what makes it the project's
/// one designed test seam.
/// </summary>
public sealed class SuggestionEngine
{
    private readonly int _randomSeed;

    /// <summary>
    /// Creates an engine whose tie-breaking between otherwise-equal Candidates is driven by
    /// <paramref name="randomSeed"/>. The same seed always yields the same order, so tests
    /// can assert it; production varies the seed per request to vary equal Suggestions.
    /// </summary>
    public SuggestionEngine(int randomSeed) => _randomSeed = randomSeed;

    /// <summary>
    /// Answers one request with the Suggestions that fit, best first, or Spent.
    /// </summary>
    public SuggestionResult Suggest(SuggestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var remaining = request.Window.RemainingTimeAt(request.Instant);
        if (request.Window.HasExpiredAt(request.Instant))
        {
            return SuggestionResult.Spent.Instance;
        }

        var tolerance = request.Settings.OvershootTolerance;

        var fitting = request.Candidates
            .Where(candidate => candidate.ItemId != request.PlayingItemId)
            .Where(candidate => candidate.EffectiveRuntime > TimeSpan.Zero)
            .Where(candidate => Fits(candidate, remaining, tolerance))
            .ToList();

        var offerable = fitting
            .Where(candidate => IsFillable(candidate, fitting, remaining, tolerance))
            .OrderBy(candidate => Rank(candidate, request.PlayingSeriesId))
            // Least Slack by MAGNITUDE: Overshoot Tolerance exists so that a small overrun can
            // beat a large gap, not so that overrunning beats fitting exactly. Ordering by
            // signed Slack would rank a candidate that runs 5 minutes past the end of the
            // window above one that fits it perfectly.
            .ThenBy(candidate => (remaining - candidate.EffectiveRuntime).Duration())
            .ThenBy(candidate => TieBreak(candidate.ItemId, _randomSeed))
            .ToList();

        var offered = SpreadAcrossLengthBands(
                BestPerSeries(offerable).ToList(),
                remaining,
                Math.Max(request.Settings.SuggestionCount, 0))
            .Select(candidate => Suggestion.For(candidate, remaining))
            .ToList();

        return SuggestionResult.Offer(offered);
    }

    /// <summary>
    /// How many Length Bands the Remaining Time is cut into. Four, to match the default
    /// Suggestion count: the ideal End Card is one Candidate from each.
    /// </summary>
    private const int LengthBands = 4;

    /// <summary>
    /// Fills the Suggestion slots from an already-ranked sequence, preferring a Candidate
    /// from a Length Band not yet represented. Ranking alone fills every slot with the
    /// longest things that fit — four near-perfect fits of the whole Window — which is a
    /// choice between four ways to spend the entire evening and no choice at all about how
    /// much of it to spend. The best-ranked Candidate still takes the first slot; the
    /// spread only decides which *others* are offered, and the answer stays in ranking
    /// order so that the least-wasteful Suggestion is still the one read first.
    /// </summary>
    private static IReadOnlyList<Candidate> SpreadAcrossLengthBands(
        IReadOnlyList<Candidate> ranked,
        TimeSpan remaining,
        int count)
    {
        if (ranked.Count <= count)
        {
            return ranked;
        }

        var chosen = new bool[ranked.Count];
        var bands = new HashSet<int>();

        for (var slot = 0; slot < count; slot++)
        {
            // A new band first, falling back to the next by rank once every band that is
            // represented in the ranked list already has a Suggestion.
            var index = NextIndex(fromNewBand: true) ?? NextIndex(fromNewBand: false);
            if (index is not { } pick)
            {
                break;
            }

            chosen[pick] = true;
            bands.Add(BandOf(ranked[pick], remaining));
        }

        return ranked.Where((_, index) => chosen[index]).ToList();

        int? NextIndex(bool fromNewBand)
        {
            for (var index = 0; index < ranked.Count; index++)
            {
                if (chosen[index])
                {
                    continue;
                }

                if (!fromNewBand || !bands.Contains(BandOf(ranked[index], remaining)))
                {
                    return index;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// The Length Band a Candidate falls into: which <see cref="LengthBands"/>th of the
    /// Remaining Time its Effective Runtime reaches, 1 for the shortest. Bands are slices
    /// of the Remaining Time rather than fixed minute counts so that "short" means short
    /// *for this evening* — a 45 minute episode is a long option in an hour's Window and a
    /// quick one in three hours'. Anything that overshoots lands in the top band.
    /// </summary>
    private static int BandOf(Candidate candidate, TimeSpan remaining) =>
        Math.Clamp(
            (int)Math.Ceiling(candidate.EffectiveRuntime / (remaining / LengthBands)),
            1,
            LengthBands);

    /// <summary>
    /// Keeps the first Candidate of each series from an already-ranked sequence, so an End
    /// Card is never four episodes of the same show. Films carry no series and all survive.
    /// </summary>
    private static IEnumerable<Candidate> BestPerSeries(IEnumerable<Candidate> ranked)
    {
        var seen = new HashSet<Guid>();

        foreach (var candidate in ranked)
        {
            if (candidate.SeriesId is not { } series || seen.Add(series))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>
    /// A Candidate fits when its Effective Runtime is within the Remaining Time plus the
    /// Overshoot Tolerance.
    /// </summary>
    private static bool Fits(Candidate candidate, TimeSpan remaining, TimeSpan tolerance) =>
        candidate.EffectiveRuntime <= remaining + tolerance;

    /// <summary>
    /// The shuffling key for Candidates that are otherwise equal: a deterministic mix of the
    /// item id and the seed. It is spelled out rather than taken from <see cref="Random"/> or
    /// <see cref="HashCode"/> because both are seeded per process, which would make the order
    /// differ between runs of the same test. Deriving it from the id rather than from the
    /// position in the list also makes the answer independent of the order Candidates arrive
    /// in, so an adapter's query order cannot leak into the ranking.
    /// </summary>
    private static uint TieBreak(Guid itemId, int seed)
    {
        Span<byte> bytes = stackalloc byte[16];
        itemId.TryWriteBytes(bytes);

        // FNV-1a, seeded: cheap, stable across processes and good enough to scatter equals.
        var hash = 2166136261u ^ unchecked((uint)seed);
        foreach (var value in bytes)
        {
            hash = unchecked((hash ^ value) * 16777619u);
        }

        return hash;
    }

    /// <summary>
    /// The ranking slot, best first: the native next-up episode of the series now playing,
    /// then the Candidate's own tier. The top slot is the engine's because it depends on
    /// what is playing, which no Candidate can know.
    /// </summary>
    private static int Rank(Candidate candidate, Guid? playingSeriesId) =>
        candidate.IsNativeNextUp
        && playingSeriesId is { } playing
        && candidate.SeriesId == playing
            ? -1
            : (int)candidate.Tier;

    /// <summary>
    /// Fillable, one step only: after watching <paramref name="candidate"/> either the
    /// remainder is small enough to count as spent, or some *other* Candidate fits it. The
    /// step is deliberately not recursive — the user chooses again at the next End Card, by
    /// which time the Remaining Time and the library have both moved on.
    /// </summary>
    private static bool IsFillable(
        Candidate candidate,
        IReadOnlyList<Candidate> others,
        TimeSpan remaining,
        TimeSpan tolerance)
    {
        var remainder = remaining - candidate.EffectiveRuntime;

        // A remainder no larger than the tolerance the admin already accepts as mis-fit is
        // not worth filling: that window is spent, and the candidate is a clean finish.
        if (remainder <= tolerance)
        {
            return true;
        }

        return others.Any(other =>
            other.ItemId != candidate.ItemId && Fits(other, remainder, tolerance));
    }
}
