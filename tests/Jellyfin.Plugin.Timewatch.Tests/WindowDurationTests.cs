using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// What a request to start a Watch Window turns into: the asked-for duration, the admin's
/// default when nothing was asked for, or nothing at all when the number is not a duration
/// an evening could have.
/// </summary>
public class WindowDurationTests
{
    [Fact]
    public void A_requested_number_of_minutes_is_the_duration()
    {
        Assert.Equal(TimeSpan.FromMinutes(90), WindowDuration.Resolve(90, TimewatchSettings.Default));
    }

    [Fact]
    public void Asking_for_nothing_takes_the_configured_default()
    {
        var settings = TimewatchSettings.Default with { DefaultWindowDuration = TimeSpan.FromMinutes(45) };

        Assert.Equal(TimeSpan.FromMinutes(45), WindowDuration.Resolve(null, settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void A_duration_that_is_not_positive_is_refused(int minutes)
    {
        Assert.Null(WindowDuration.Resolve(minutes, TimewatchSettings.Default));
    }

    [Fact]
    public void A_duration_longer_than_a_day_is_refused()
    {
        Assert.Null(WindowDuration.Resolve((int)WindowDuration.Longest.TotalMinutes + 1, TimewatchSettings.Default));
    }

    [Fact]
    public void A_duration_of_exactly_the_longest_allowed_is_accepted()
    {
        Assert.Equal(
            WindowDuration.Longest,
            WindowDuration.Resolve((int)WindowDuration.Longest.TotalMinutes, TimewatchSettings.Default));
    }

    [Fact]
    public void A_nonsense_configured_default_is_refused_rather_than_silently_replaced()
    {
        var settings = TimewatchSettings.Default with { DefaultWindowDuration = TimeSpan.Zero };

        Assert.Null(WindowDuration.Resolve(null, settings));
    }
}
