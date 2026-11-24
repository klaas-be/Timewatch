using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

public class SuggestionTests
{
    private static Candidate Film(TimeSpan runtime, TimeSpan playbackPosition = default) => new()
    {
        ItemId = Guid.NewGuid(),
        Title = "Arrival",
        Runtime = runtime,
        PlaybackPosition = playbackPosition
    };

    [Fact]
    public void Slack_is_positive_dead_air_when_the_candidate_is_shorter_than_the_remaining_time()
    {
        var suggestion = Suggestion.For(Film(TimeSpan.FromMinutes(43)), TimeSpan.FromMinutes(60));

        Assert.Equal(TimeSpan.FromMinutes(17), suggestion.Slack);
    }

    [Fact]
    public void Slack_is_zero_when_the_candidate_fills_the_remaining_time_exactly()
    {
        var suggestion = Suggestion.For(Film(TimeSpan.FromMinutes(60)), TimeSpan.FromMinutes(60));

        Assert.Equal(TimeSpan.Zero, suggestion.Slack);
    }

    [Fact]
    public void Slack_is_negative_overshoot_when_the_candidate_runs_past_the_end_of_the_window()
    {
        var suggestion = Suggestion.For(Film(TimeSpan.FromMinutes(116)), TimeSpan.FromMinutes(90));

        Assert.Equal(TimeSpan.FromMinutes(-26), suggestion.Slack);
    }

    [Fact]
    public void Slack_is_priced_against_the_effective_runtime_of_a_part_watched_candidate()
    {
        var film = Film(TimeSpan.FromMinutes(116), TimeSpan.FromMinutes(80));

        var suggestion = Suggestion.For(film, TimeSpan.FromMinutes(60));

        Assert.Equal(TimeSpan.FromMinutes(36), suggestion.EffectiveRuntime);
        Assert.Equal(TimeSpan.FromMinutes(24), suggestion.Slack);
    }

    [Fact]
    public void A_suggestion_carries_the_candidate_it_was_made_from()
    {
        var film = Film(TimeSpan.FromMinutes(43));

        var suggestion = Suggestion.For(film, TimeSpan.FromMinutes(60));

        Assert.Equal(film, suggestion.Candidate);
    }
}
