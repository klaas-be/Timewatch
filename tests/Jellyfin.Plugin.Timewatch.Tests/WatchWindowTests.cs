using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

public class WatchWindowTests
{
    private static readonly Guid User = Guid.Parse("1b9cd0a2-7cf5-4a65-9c4e-9b2d1e9f0a11");
    private static readonly DateTimeOffset NineOClock = new(2026, 11, 24, 21, 0, 0, TimeSpan.Zero);

    private static WatchWindow EndingAtEleven() => new() { UserId = User, EndsAt = NineOClock.AddHours(2) };

    [Fact]
    public void Remaining_time_before_the_end_time_is_the_time_still_to_go()
    {
        var window = EndingAtEleven();

        Assert.Equal(TimeSpan.FromMinutes(90), window.RemainingTimeAt(NineOClock.AddMinutes(30)));
    }

    [Fact]
    public void Remaining_time_at_the_end_time_is_zero()
    {
        var window = EndingAtEleven();

        Assert.Equal(TimeSpan.Zero, window.RemainingTimeAt(NineOClock.AddHours(2)));
    }

    [Fact]
    public void Remaining_time_after_the_end_time_is_negative()
    {
        var window = EndingAtEleven();

        Assert.Equal(TimeSpan.FromMinutes(-15), window.RemainingTimeAt(NineOClock.AddHours(2).AddMinutes(15)));
    }

    [Fact]
    public void A_window_with_time_left_has_not_expired()
    {
        var window = EndingAtEleven();

        Assert.False(window.HasExpiredAt(NineOClock.AddMinutes(30)));
    }

    [Fact]
    public void A_window_has_expired_once_its_end_time_is_reached()
    {
        var window = EndingAtEleven();

        Assert.True(window.HasExpiredAt(NineOClock.AddHours(2)));
        Assert.True(window.HasExpiredAt(NineOClock.AddHours(3)));
    }

    [Fact]
    public void Starting_a_window_for_a_duration_anchors_it_to_a_wall_clock_end_time()
    {
        var window = WatchWindow.StartingAt(User, NineOClock, TimeSpan.FromHours(2));

        Assert.Equal(User, window.UserId);
        Assert.Equal(NineOClock.AddHours(2), window.EndsAt);
    }

    [Fact]
    public void An_end_time_is_held_in_utc_whatever_offset_it_was_given_in()
    {
        var berlinNine = new DateTimeOffset(2026, 11, 24, 22, 0, 0, TimeSpan.FromHours(1));

        var window = new WatchWindow { UserId = User, EndsAt = berlinNine };

        Assert.Equal(TimeSpan.Zero, window.EndsAt.Offset);
        Assert.Equal(berlinNine.UtcDateTime, window.EndsAt.UtcDateTime);
    }

    [Fact]
    public void Remaining_time_shrinks_with_wall_clock_time_rather_than_runtime_watched()
    {
        var window = WatchWindow.StartingAt(User, NineOClock, TimeSpan.FromHours(2));

        // A phone call costs the user an hour of Window without a frame being played.
        Assert.Equal(TimeSpan.FromHours(1), window.RemainingTimeAt(NineOClock.AddHours(1)));
    }
}
