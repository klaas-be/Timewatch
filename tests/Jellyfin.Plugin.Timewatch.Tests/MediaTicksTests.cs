using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The one piece of the CandidateSource adapter worth testing: turning the tick counts a
/// media server reports into the <see cref="Candidate"/> contract, which requires an
/// unknown runtime to be null and a playback position to be non-negative. Everything else
/// in the adapter is untestable translation, so this lives on the Domain side of the port.
/// </summary>
public class MediaTicksTests
{
    [Fact]
    public void A_runtime_of_ninety_minutes_round_trips()
    {
        Assert.Equal(TimeSpan.FromMinutes(90), MediaTicks.ToRuntime(54_000_000_000));
    }

    [Fact]
    public void An_unreported_runtime_is_unknown_rather_than_zero()
    {
        Assert.Null(MediaTicks.ToRuntime(null));
    }

    [Fact]
    public void A_zero_runtime_is_unknown_too()
    {
        // A library item mid-scan has no runtime yet and reports 0, which is not the same
        // claim as "this item is zero seconds long".
        Assert.Null(MediaTicks.ToRuntime(0));
    }

    [Fact]
    public void A_negative_runtime_is_unknown_too()
    {
        Assert.Null(MediaTicks.ToRuntime(-1));
    }

    [Fact]
    public void A_resume_position_of_twenty_minutes_round_trips()
    {
        Assert.Equal(TimeSpan.FromMinutes(20), MediaTicks.ToPlaybackPosition(12_000_000_000));
    }

    [Fact]
    public void An_item_with_no_resume_data_has_not_been_started()
    {
        Assert.Equal(TimeSpan.Zero, MediaTicks.ToPlaybackPosition(null));
    }

    [Fact]
    public void A_negative_resume_position_has_not_been_started()
    {
        // Candidate.EffectiveRuntime subtracts the position from the runtime, so a negative
        // position would price the item above its own runtime.
        Assert.Equal(TimeSpan.Zero, MediaTicks.ToPlaybackPosition(-1));
    }

    [Fact]
    public void A_part_watched_candidate_built_from_ticks_is_priced_at_its_remainder()
    {
        var candidate = new Candidate
        {
            ItemId = Guid.NewGuid(),
            Title = "Heat",
            Runtime = MediaTicks.ToRuntime(102_000_000_000),
            PlaybackPosition = MediaTicks.ToPlaybackPosition(48_000_000_000)
        };

        Assert.Equal(TimeSpan.FromMinutes(90), candidate.EffectiveRuntime);
        Assert.True(candidate.IsInProgress);
    }
}
