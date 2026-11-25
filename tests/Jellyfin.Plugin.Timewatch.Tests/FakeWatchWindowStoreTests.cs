using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

public class FakeWatchWindowStoreTests : WatchWindowStoreContract
{
    protected override IWatchWindowStore NewStore() => new FakeWatchWindowStore();
}
