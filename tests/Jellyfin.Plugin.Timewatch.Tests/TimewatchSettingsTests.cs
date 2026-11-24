using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

public class TimewatchSettingsTests
{
    [Fact]
    public void Defaults_match_the_spec()
    {
        var settings = TimewatchSettings.Default;

        Assert.Equal(TimeSpan.FromHours(2), settings.DefaultWindowDuration);
        Assert.Equal(TimeSpan.FromMinutes(10), settings.OvershootTolerance);
        Assert.Equal(4, settings.SuggestionCount);
        Assert.Empty(settings.LibraryIds);
        Assert.False(settings.DeveloperMode);
    }
}
