using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

public class CandidateTests
{
    private static Candidate Film(TimeSpan? runtime, TimeSpan playbackPosition = default) => new()
    {
        ItemId = Guid.NewGuid(),
        Title = "Arrival",
        Runtime = runtime,
        PlaybackPosition = playbackPosition
    };

    [Fact]
    public void Effective_runtime_of_an_unwatched_candidate_is_its_full_runtime()
    {
        var candidate = Film(TimeSpan.FromMinutes(116));

        Assert.Equal(TimeSpan.FromMinutes(116), candidate.EffectiveRuntime);
    }

    [Fact]
    public void Effective_runtime_of_a_part_watched_candidate_is_runtime_minus_playback_position()
    {
        var candidate = Film(TimeSpan.FromMinutes(116), TimeSpan.FromMinutes(80));

        Assert.Equal(TimeSpan.FromMinutes(36), candidate.EffectiveRuntime);
    }

    [Fact]
    public void Effective_runtime_of_a_candidate_with_no_known_runtime_is_zero()
    {
        var candidate = Film(runtime: null);

        Assert.Equal(TimeSpan.Zero, candidate.EffectiveRuntime);
    }

    [Fact]
    public void Effective_runtime_of_a_candidate_with_a_zero_runtime_is_zero()
    {
        var candidate = Film(TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, candidate.EffectiveRuntime);
    }

    [Fact]
    public void Effective_runtime_never_goes_negative_when_the_playback_position_is_past_the_runtime()
    {
        var candidate = Film(TimeSpan.FromMinutes(116), TimeSpan.FromMinutes(120));

        Assert.Equal(TimeSpan.Zero, candidate.EffectiveRuntime);
    }

    [Fact]
    public void A_part_watched_candidate_is_in_the_in_progress_tier()
    {
        var candidate = Film(TimeSpan.FromMinutes(116), TimeSpan.FromMinutes(80));

        Assert.True(candidate.IsInProgress);
        Assert.Equal(CandidateTier.InProgress, candidate.Tier);
    }

    [Fact]
    public void An_unstarted_native_next_up_episode_is_in_the_next_up_tier()
    {
        var candidate = Film(TimeSpan.FromMinutes(43)) with { SeriesId = Guid.NewGuid(), SeriesName = "Severance", IsNativeNextUp = true };

        Assert.False(candidate.IsInProgress);
        Assert.Equal(CandidateTier.NextUp, candidate.Tier);
    }

    [Fact]
    public void An_unstarted_candidate_that_is_not_next_up_is_in_the_cold_tier()
    {
        var candidate = Film(TimeSpan.FromMinutes(116));

        Assert.Equal(CandidateTier.Cold, candidate.Tier);
    }

    [Fact]
    public void In_progress_outranks_next_up_which_outranks_cold()
    {
        Assert.True(CandidateTier.InProgress < CandidateTier.NextUp);
        Assert.True(CandidateTier.NextUp < CandidateTier.Cold);
    }

    [Fact]
    public void A_started_next_up_episode_is_in_progress_rather_than_next_up()
    {
        var candidate = Film(TimeSpan.FromMinutes(43), TimeSpan.FromMinutes(10)) with { IsNativeNextUp = true };

        Assert.Equal(CandidateTier.InProgress, candidate.Tier);
    }
}
