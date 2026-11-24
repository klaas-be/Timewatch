using Jellyfin.Plugin.Timewatch.Api.Models;
using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The shapes the start page and the End Card code against. The controllers themselves are
/// not testable here — Jellyfin's assemblies are absent from the test output by design — so
/// the translation from domain answer to wire answer lives in these models, on this side of
/// the boundary, and is tested here.
/// </summary>
public class ApiContractTests
{
    private static readonly Guid User = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Series = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void No_window_reads_as_inactive_with_no_remaining_time()
    {
        var status = WatchWindowStatus.For(null, Now, TimewatchSettings.Default);

        Assert.False(status.Active);
        Assert.Null(status.EndsAt);
        Assert.Equal(0, status.RemainingSeconds);
    }

    [Fact]
    public void An_active_window_reads_as_its_end_time_and_its_remaining_seconds()
    {
        var window = WatchWindow.StartingAt(User, Now, TimeSpan.FromMinutes(90));

        var status = WatchWindowStatus.For(window, Now.AddMinutes(10), TimewatchSettings.Default);

        Assert.True(status.Active);
        Assert.Equal(Now.AddMinutes(90), status.EndsAt);
        Assert.Equal(80 * 60, status.RemainingSeconds);
    }

    /// <summary>
    /// The start page prefills the duration from the admin's default, and it must do so
    /// without being able to read the plugin configuration — that needs an admin.
    /// </summary>
    [Fact]
    public void The_status_carries_the_configured_default_duration_so_a_non_admin_page_can_prefill_it()
    {
        var settings = TimewatchSettings.Default with { DefaultWindowDuration = TimeSpan.FromMinutes(45) };

        Assert.Equal(45, WatchWindowStatus.For(null, Now, settings).DefaultWindowMinutes);
    }

    [Fact]
    public void Remaining_time_is_truncated_rather_than_rounded_up_so_it_is_never_overstated()
    {
        var window = new WatchWindow { UserId = User, EndsAt = Now.AddSeconds(90.9) };

        Assert.Equal(90, WatchWindowStatus.For(window, Now, TimewatchSettings.Default).RemainingSeconds);
    }

    [Fact]
    public void A_spent_result_offers_nothing_and_says_so()
    {
        var response = SuggestionsResponse.For(SuggestionResult.Spent.Instance, TimeSpan.FromMinutes(5));

        Assert.True(response.Spent);
        Assert.Empty(response.Suggestions);
        Assert.Equal(5 * 60, response.RemainingSeconds);
    }

    [Fact]
    public void An_offered_result_keeps_the_engines_order()
    {
        var remaining = TimeSpan.FromMinutes(120);
        var result = SuggestionResult.Offer(
        [
            Suggestion.For(Film("Arrival", TimeSpan.FromMinutes(116)), remaining),
            Suggestion.For(Film("Primer", TimeSpan.FromMinutes(77)), remaining)
        ]);

        var response = SuggestionsResponse.For(result, remaining);

        Assert.False(response.Spent);
        Assert.Equal(["Arrival", "Primer"], response.Suggestions.Select(s => s.Title));
    }

    [Fact]
    public void A_suggestion_carries_its_effective_runtime_and_the_slack_it_would_leave()
    {
        var remaining = TimeSpan.FromMinutes(120);
        var film = Film("Arrival", TimeSpan.FromMinutes(116));

        var view = Single(SuggestionsResponse.For(
            SuggestionResult.Offer([Suggestion.For(film, remaining)]),
            remaining));

        Assert.Equal(film.ItemId, view.ItemId);
        Assert.Equal(116 * 60, view.EffectiveRuntimeSeconds);
        Assert.Equal(4 * 60, view.SlackSeconds);
    }

    [Fact]
    public void A_part_watched_suggestion_is_priced_at_what_is_left_of_it()
    {
        var remaining = TimeSpan.FromMinutes(60);
        var film = Film("Arrival", TimeSpan.FromMinutes(116)) with
        {
            PlaybackPosition = TimeSpan.FromMinutes(80)
        };

        var view = Single(SuggestionsResponse.For(
            SuggestionResult.Offer([Suggestion.For(film, remaining)]),
            remaining));

        Assert.Equal(36 * 60, view.EffectiveRuntimeSeconds);
        Assert.Equal(24 * 60, view.SlackSeconds);
    }

    /// <summary>
    /// An overshoot within the Overshoot Tolerance is offered, and the End Card has to be
    /// able to say so — which it cannot if the sign is lost on the way out.
    /// </summary>
    [Fact]
    public void An_overshooting_suggestion_reports_negative_slack()
    {
        var remaining = TimeSpan.FromMinutes(60);

        var view = Single(SuggestionsResponse.For(
            SuggestionResult.Offer([Suggestion.For(Film("Dune", TimeSpan.FromMinutes(65)), remaining)]),
            remaining));

        Assert.Equal(-5 * 60, view.SlackSeconds);
    }

    [Fact]
    public void An_episode_carries_its_series_and_whether_jellyfin_would_have_played_it_next()
    {
        var remaining = TimeSpan.FromMinutes(60);
        var episode = new Candidate
        {
            ItemId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Title = "S1E4",
            SeriesId = Series,
            SeriesName = "Severance",
            Runtime = TimeSpan.FromMinutes(45),
            IsNativeNextUp = true
        };

        var view = Single(SuggestionsResponse.For(
            SuggestionResult.Offer([Suggestion.For(episode, remaining)]),
            remaining));

        Assert.Equal(Series, view.SeriesId);
        Assert.Equal("Severance", view.SeriesName);
        Assert.True(view.IsNativeNextUp);
    }

    [Fact]
    public void A_film_carries_no_series()
    {
        var remaining = TimeSpan.FromMinutes(60);

        var view = Single(SuggestionsResponse.For(
            SuggestionResult.Offer([Suggestion.For(Film("Arrival", TimeSpan.FromMinutes(45)), remaining)]),
            remaining));

        Assert.Null(view.SeriesId);
        Assert.Null(view.SeriesName);
        Assert.False(view.IsNativeNextUp);
    }

    private static SuggestionView Single(SuggestionsResponse response) =>
        Assert.Single(response.Suggestions);

    private static Candidate Film(string title, TimeSpan runtime) => new()
    {
        ItemId = new Guid(System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(title))),
        Title = title,
        Runtime = runtime
    };
}
