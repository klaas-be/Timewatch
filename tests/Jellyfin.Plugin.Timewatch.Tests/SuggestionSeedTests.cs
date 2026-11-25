using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The seed the API derives per request. Its whole job is that two End Cards opened at
/// different moments do not show the same four equal items, while one request stays
/// internally consistent.
/// </summary>
public class SuggestionSeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void One_instant_yields_one_seed()
    {
        Assert.Equal(SuggestionSeed.FromInstant(Now), SuggestionSeed.FromInstant(Now));
    }

    [Fact]
    public void The_same_instant_in_another_offset_yields_the_same_seed()
    {
        Assert.Equal(
            SuggestionSeed.FromInstant(Now),
            SuggestionSeed.FromInstant(Now.ToOffset(TimeSpan.FromHours(5))));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1_000)]
    [InlineData(60_000)]
    [InlineData(3_600_000)]
    public void A_later_instant_yields_a_different_seed(int millisecondsLater)
    {
        Assert.NotEqual(
            SuggestionSeed.FromInstant(Now),
            SuggestionSeed.FromInstant(Now.AddMilliseconds(millisecondsLater)));
    }

    [Fact]
    public void The_extremes_of_the_calendar_do_not_overflow()
    {
        SuggestionSeed.FromInstant(DateTimeOffset.MinValue);
        SuggestionSeed.FromInstant(DateTimeOffset.MaxValue);
    }

    /// <summary>
    /// The reason the seed exists (spec: "repeated End Cards don't show me the same four
    /// things all evening"): fed through the engine over an evening's worth of instants,
    /// equally fitting Candidates come back in more than one order.
    /// </summary>
    [Fact]
    public void Seeds_taken_across_an_evening_shuffle_equally_fitting_candidates()
    {
        var window = WatchWindow.StartingAt(Guid.NewGuid(), Now, TimeSpan.FromHours(3));

        var orders = Enumerable.Range(0, 30)
            .Select(minute => Now.AddMinutes(minute))
            .Select(instant => new SuggestionEngine(SuggestionSeed.FromInstant(instant))
                .Suggest(new SuggestionRequest
                {
                    Window = window,
                    Instant = Now,
                    Settings = TimewatchSettings.Default,
                    Candidates = EqualFilms
                }))
            .Select(result => string.Join(
                ",",
                Assert.IsType<SuggestionResult.Offered>(result)
                    .Suggestions.Select(suggestion => suggestion.Candidate.Title)))
            .Distinct()
            .ToList();

        Assert.True(orders.Count > 1, "equal candidates should be shuffled across requests");
    }

    private static readonly IReadOnlyList<Candidate> EqualFilms =
    [
        Film("A"), Film("B"), Film("C"), Film("D")
    ];

    private static Candidate Film(string title) => new()
    {
        ItemId = new Guid(System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(title))),
        Title = title,
        Runtime = TimeSpan.FromMinutes(90)
    };
}
