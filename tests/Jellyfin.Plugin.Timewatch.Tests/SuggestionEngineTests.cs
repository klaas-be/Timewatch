using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The engine is the project's only designed seam, so these tests drive it directly and
/// assert only on what is offered and in what order — never on scores or helpers.
/// </summary>
public class SuggestionEngineTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 20, 0, 0, TimeSpan.Zero);

    private static readonly SuggestionEngine Engine = new(randomSeed: 1);

    private static TimeSpan Min(int minutes) => TimeSpan.FromMinutes(minutes);

    private static WatchWindow WindowOf(TimeSpan remaining) =>
        WatchWindow.StartingAt(User, Now, remaining);

    private static SuggestionRequest Request(
        TimeSpan remaining,
        IReadOnlyList<Candidate> candidates,
        TimewatchSettings? settings = null,
        Guid? playingSeriesId = null) => new()
        {
            Window = WindowOf(remaining),
            Instant = Now,
            Settings = settings ?? TimewatchSettings.Default,
            Candidates = candidates,
            PlayingSeriesId = playingSeriesId
        };

    [Fact]
    public void A_window_past_its_end_time_is_spent()
    {
        var result = Engine.Suggest(new SuggestionRequest
        {
            Window = WindowOf(Min(60)),
            Instant = Now + Min(61),
            Settings = TimewatchSettings.Default,
            Candidates = [Film("Arrival", Min(20))]
        });

        Assert.IsType<SuggestionResult.Spent>(result);
    }

    [Fact]
    public void A_window_with_time_left_but_no_fitting_candidate_is_spent()
    {
        var result = Engine.Suggest(Request(Min(30), [Film("Arrival", Min(116))]));

        Assert.IsType<SuggestionResult.Spent>(result);
    }

    [Fact]
    public void A_candidate_with_no_known_runtime_is_never_offered()
    {
        var result = Engine.Suggest(Request(Min(60), [Film("Unknown", runtime: null)]));

        Assert.IsType<SuggestionResult.Spent>(result);
    }

    [Fact]
    public void A_candidate_longer_than_the_remaining_time_beyond_the_overshoot_tolerance_is_not_offered()
    {
        var result = Engine.Suggest(Request(Min(60), [Film("Dune", Min(75))]));

        Assert.IsType<SuggestionResult.Spent>(result);
    }

    [Fact]
    public void A_candidate_longer_than_the_remaining_time_but_within_the_overshoot_tolerance_is_offered()
    {
        var result = Engine.Suggest(Request(Min(60), [Film("Dune", Min(65))]));

        Assert.Equal(["Dune"], TitlesOf(result));
    }

    [Fact]
    public void A_part_watched_candidate_is_priced_at_its_remaining_runtime()
    {
        var result = Engine.Suggest(
            Request(Min(40), [Film("Arrival", Min(116), playbackPosition: Min(80))]));

        var offered = Assert.IsType<SuggestionResult.Offered>(result);
        var suggestion = Assert.Single(offered.Suggestions);
        Assert.Equal(Min(36), suggestion.EffectiveRuntime);
        Assert.Equal(Min(4), suggestion.Slack);
    }

    [Fact]
    public void A_candidate_is_not_offered_when_the_remainder_it_would_leave_cannot_be_filled()
    {
        var result = Engine.Suggest(Request(
            Min(100),
            [Film("Long", Min(85)), Film("Short", Min(40)), Film("Middle", Min(55))],
            NoTolerance));

        // Long leaves 15 minutes and nothing fits those; the other two fill each other.
        Assert.Equal(["Middle", "Short"], TitlesOf(result).Order().ToList());
    }

    [Fact]
    public void A_candidate_is_offered_when_the_remainder_it_would_leave_can_be_filled()
    {
        var result = Engine.Suggest(Request(
            Min(120),
            [Film("Hour", Min(60)), Film("Fifty", Min(50))],
            NoTolerance));

        Assert.Equal(2, TitlesOf(result).Count);
    }

    [Fact]
    public void A_candidate_leaving_only_a_remainder_within_the_overshoot_tolerance_needs_no_filler()
    {
        var result = Engine.Suggest(Request(Min(60), [Film("Close", Min(55))]));

        Assert.Equal(["Close"], TitlesOf(result));
    }

    [Fact]
    public void Fillability_looks_one_step_ahead_only()
    {
        // Watching both leaves fifty unfillable minutes, but the engine checks one step:
        // each of the two fills the other's remainder, so both are offered.
        var result = Engine.Suggest(Request(
            Min(150),
            [Film("First", Min(50)), Film("Second", Min(50))],
            NoTolerance));

        Assert.Equal(2, TitlesOf(result).Count);
    }

    [Fact]
    public void The_native_next_up_episode_of_the_playing_series_is_offered_first_when_it_fits()
    {
        var result = Engine.Suggest(Request(
            Min(120),
            [
                Film("Part-watched", Min(60), playbackPosition: Min(30)),
                Episode("Other S2E3", Watching, Min(40), isNativeNextUp: true),
                Episode("Playing S4E2", Playing, Min(45), isNativeNextUp: true)
            ],
            playingSeriesId: Playing));

        Assert.Equal(["Playing S4E2", "Part-watched", "Other S2E3"], TitlesOf(result));
    }

    [Fact]
    public void The_native_next_up_episode_of_the_playing_series_is_not_special_cased_when_it_does_not_fit()
    {
        var result = Engine.Suggest(Request(
            Min(40),
            [
                Episode("Playing S4E2", Playing, Min(90), isNativeNextUp: true),
                Film("Short", Min(35))
            ],
            playingSeriesId: Playing));

        Assert.Equal(["Short"], TitlesOf(result));
    }

    [Fact]
    public void In_progress_items_rank_above_next_up_episodes_which_rank_above_cold_starts()
    {
        var result = Engine.Suggest(Request(
            Min(120),
            [
                Film("Cold film", Min(50)),
                Episode("Next up", Watching, Min(40), isNativeNextUp: true),
                Film("In progress", Min(60), playbackPosition: Min(30))
            ]));

        Assert.Equal(["In progress", "Next up", "Cold film"], TitlesOf(result));
    }

    [Fact]
    public void Within_a_tier_the_candidate_leaving_the_least_slack_is_offered_first()
    {
        var result = Engine.Suggest(Request(
            Min(180),
            [Film("Sixty", Min(60)), Film("Hundred", Min(100)), Film("Eighty", Min(80))]));

        Assert.Equal(["Hundred", "Eighty", "Sixty"], TitlesOf(result));
    }

    [Fact]
    public void The_order_of_equally_fitting_candidates_is_stable_for_a_seed()
    {
        var candidates = EqualFilms();
        var request = Request(Min(180), candidates);

        var first = TitlesOf(new SuggestionEngine(randomSeed: 7).Suggest(request));
        var again = TitlesOf(new SuggestionEngine(randomSeed: 7).Suggest(request));

        Assert.Equal(first, again);
    }

    [Fact]
    public void The_order_of_equally_fitting_candidates_does_not_depend_on_the_order_they_arrive_in()
    {
        var candidates = EqualFilms();
        var engine = new SuggestionEngine(randomSeed: 7);

        var asGiven = TitlesOf(engine.Suggest(Request(Min(180), candidates)));
        var reversed = TitlesOf(engine.Suggest(Request(Min(180), candidates.Reverse().ToList())));

        Assert.Equal(asGiven, reversed);
    }

    [Fact]
    public void Randomisation_reorders_equally_fitting_candidates_between_seeds()
    {
        var request = Request(Min(180), EqualFilms());

        var orders = Enumerable.Range(0, 20)
            .Select(seed => string.Join(",", TitlesOf(new SuggestionEngine(seed).Suggest(request))))
            .Distinct()
            .ToList();

        Assert.True(orders.Count > 1, "equal candidates should be shuffled between seeds");
    }

    [Fact]
    public void Randomisation_never_reorders_candidates_that_are_not_equal()
    {
        var candidates = new[]
        {
            Film("Sixty", Min(60)), Film("Hundred", Min(100)), Film("Eighty", Min(80))
        };

        foreach (var seed in Enumerable.Range(0, 20))
        {
            var result = new SuggestionEngine(seed).Suggest(Request(Min(180), candidates));

            Assert.Equal(["Hundred", "Eighty", "Sixty"], TitlesOf(result));
        }
    }

    [Fact]
    public void At_most_one_suggestion_is_offered_per_series()
    {
        var result = Engine.Suggest(Request(
            Min(180),
            [
                Episode("S1E4", Watching, Min(80)),
                Episode("S1E5", Watching, Min(90)),
                Film("Seventy", Min(70))
            ]));

        // S1E5 fits best of the two, so it is the one episode of that series offered.
        Assert.Equal(["S1E5", "Seventy"], TitlesOf(result));
    }

    [Fact]
    public void The_number_of_suggestions_honours_the_configured_count()
    {
        var result = Engine.Suggest(Request(
            Min(180),
            Ladder,
            TimewatchSettings.Default with { SuggestionCount = 2 }));

        Assert.Equal(["Hundred", "Ninety"], TitlesOf(result));
    }

    [Fact]
    public void Fewer_suggestions_are_returned_when_fewer_candidates_are_fillable()
    {
        var result = Engine.Suggest(Request(
            Min(100),
            [Film("Long", Min(85)), Film("Short", Min(40)), Film("Middle", Min(55))],
            NoTolerance with { SuggestionCount = 4 }));

        Assert.Equal(2, TitlesOf(result).Count);
    }

    [Fact]
    public void Dropping_a_dismissed_candidate_frees_its_slot_for_the_next_best_suggestion()
    {
        var settings = TimewatchSettings.Default with { SuggestionCount = 2 };
        var offered = TitlesOf(Engine.Suggest(Request(Min(180), Ladder, settings)));
        Assert.Equal(["Hundred", "Ninety"], offered);

        var remaining = Ladder.Where(candidate => candidate.Title != "Hundred").ToList();
        var afterDismiss = TitlesOf(Engine.Suggest(Request(Min(180), remaining, settings)));

        Assert.Equal(["Ninety", "Eighty"], afterDismiss);
    }

    [Fact]
    public void An_exact_fit_beats_an_item_that_overruns_by_less_than_the_gap_it_saves()
    {
        // Slack is minimised by magnitude, not signed: Overshoot Tolerance lets a small
        // overrun beat a large gap, but it must never make overrunning preferable to
        // fitting. The 60 minute film fits exactly; the 65 minute one runs 5 minutes past
        // the end of the window, which is inside the default 10 minute tolerance.
        var result = Engine.Suggest(Request(Min(60), [Film("Over", Min(65)), Film("Exact", Min(60))]));

        var offered = Assert.IsType<SuggestionResult.Offered>(result);
        Assert.Equal("Exact", offered.Suggestions[0].Candidate.Title);
        Assert.Equal(TimeSpan.Zero, offered.Suggestions[0].Slack);
    }

    /// <summary>Five films of descending length, so the slack ordering is unambiguous.</summary>
    private static readonly IReadOnlyList<Candidate> Ladder =
    [
        Film("Hundred", Min(100)),
        Film("Ninety", Min(90)),
        Film("Eighty", Min(80)),
        Film("Seventy", Min(70)),
        Film("Sixty", Min(60))
    ];

    private static IReadOnlyList<Candidate> EqualFilms() =>
    [
        Film("A", Min(90)), Film("B", Min(90)), Film("C", Min(90)), Film("D", Min(90))
    ];

    private static readonly Guid Playing = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Watching = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static Candidate Episode(
        string title,
        Guid seriesId,
        TimeSpan? runtime,
        bool isNativeNextUp = false,
        TimeSpan playbackPosition = default) => new()
        {
            ItemId = IdOf(title),
            Title = title,
            SeriesId = seriesId,
            SeriesName = seriesId.ToString(),
            Runtime = runtime,
            PlaybackPosition = playbackPosition,
            IsNativeNextUp = isNativeNextUp
        };

    private static readonly TimewatchSettings NoTolerance =
        TimewatchSettings.Default with { OvershootTolerance = TimeSpan.Zero };

    private static IReadOnlyList<string> TitlesOf(SuggestionResult result) =>
        Assert.IsType<SuggestionResult.Offered>(result)
            .Suggestions.Select(suggestion => suggestion.Candidate.Title).ToList();

    private static Candidate Film(string title, TimeSpan? runtime, TimeSpan playbackPosition = default) => new()
    {
        ItemId = IdOf(title),
        Title = title,
        Runtime = runtime,
        PlaybackPosition = playbackPosition
    };

    /// <summary>
    /// A stable item id per title, so that the engine's seeded tie-breaking — which is
    /// derived from the item id — is reproducible from one test run to the next.
    /// </summary>
    private static Guid IdOf(string title) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(title)));
}
