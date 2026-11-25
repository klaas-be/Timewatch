using Jellyfin.Plugin.Timewatch.Domain;
using Jellyfin.Plugin.Timewatch.Storage;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The file-backed store against a real temporary directory. It holds to the port's whole
/// contract, and adds what only a file can get wrong: surviving a restart, concurrent
/// writers, and a file that has been damaged behind its back.
/// </summary>
public sealed class FileWatchWindowStoreTests : WatchWindowStoreContract, IDisposable
{
    private static readonly Guid Alice = Guid.Parse("1b9cd0a2-7cf5-4a65-9c4e-9b2d1e9f0a11");
    private static readonly DateTimeOffset NineOClock = new(2026, 11, 24, 21, 0, 0, TimeSpan.Zero);

    private readonly string _dataFolder = Directory.CreateTempSubdirectory("timewatch-test-").FullName;

    protected override IWatchWindowStore NewStore() => new FileWatchWindowStore(_dataFolder);

    public void Dispose() => Directory.Delete(_dataFolder, recursive: true);

    [Fact]
    public async Task A_window_survives_a_restart()
    {
        await new FileWatchWindowStore(_dataFolder)
            .StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        // A second instance over the same folder stands in for the server coming back up.
        var afterRestart = await new FileWatchWindowStore(_dataFolder)
            .GetActiveWindowAsync(Alice, NineOClock.AddMinutes(30));

        Assert.NotNull(afterRestart);
        Assert.Equal(Alice, afterRestart.UserId);
        Assert.Equal(NineOClock.AddHours(2), afterRestart.EndsAt);
    }

    [Fact]
    public async Task A_cancellation_survives_a_restart()
    {
        await new FileWatchWindowStore(_dataFolder)
            .StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));
        await new FileWatchWindowStore(_dataFolder).CancelWindowAsync(Alice);

        Assert.Null(await new FileWatchWindowStore(_dataFolder).GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task The_store_creates_its_data_folder_if_the_host_has_not()
    {
        var missing = Path.Combine(_dataFolder, "not", "there", "yet");
        var store = new FileWatchWindowStore(missing);

        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        Assert.NotNull(await store.GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task A_corrupt_file_reads_as_no_active_window_rather_than_throwing()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));
        await File.WriteAllTextAsync(StoreFile(), "{\"windows\": [ truncated-half-way");

        Assert.Null(await store.GetActiveWindowAsync(Alice, NineOClock));
        Assert.Null(await new FileWatchWindowStore(_dataFolder).GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task An_empty_file_reads_as_no_active_window()
    {
        await File.WriteAllTextAsync(StoreFile(), string.Empty);

        Assert.Null(await NewStore().GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task A_window_started_over_a_corrupt_file_is_readable_again()
    {
        await File.WriteAllTextAsync(StoreFile(), "not json at all");
        var store = NewStore();

        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        Assert.NotNull(await new FileWatchWindowStore(_dataFolder).GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task Concurrent_starts_and_cancels_leave_a_file_that_still_parses()
    {
        var store = NewStore();
        var kept = Enumerable.Range(0, 30).Select(_ => Guid.NewGuid()).ToList();
        var churn = Enumerable.Range(0, 30).Select(_ => Guid.NewGuid()).ToList();

        var writes = kept
            .Select(user => Task.Run(() =>
                store.StartWindowAsync(WatchWindow.StartingAt(user, NineOClock, TimeSpan.FromHours(2)))))
            .Concat(churn.Select(user => Task.Run(async () =>
            {
                await store.StartWindowAsync(WatchWindow.StartingAt(user, NineOClock, TimeSpan.FromHours(2)));
                await store.CancelWindowAsync(user);
            })));

        await Task.WhenAll(writes);

        // Read through a fresh instance so the answer comes off disk, not out of any cache.
        var afterRestart = new FileWatchWindowStore(_dataFolder);
        foreach (var user in kept)
        {
            Assert.NotNull(await afterRestart.GetActiveWindowAsync(user, NineOClock));
        }

        foreach (var user in churn)
        {
            Assert.Null(await afterRestart.GetActiveWindowAsync(user, NineOClock));
        }
    }

    [Fact]
    public async Task Concurrent_readers_never_see_a_half_written_file()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        var writers = Enumerable.Range(0, 60).Select(_ => Task.Run(() =>
            store.StartWindowAsync(WatchWindow.StartingAt(Guid.NewGuid(), NineOClock, TimeSpan.FromHours(2)))));
        var readers = Enumerable.Range(0, 60).Select(_ => Task.Run(async () =>
            Assert.NotNull(await store.GetActiveWindowAsync(Alice, NineOClock))));

        await Task.WhenAll(writers.Concat(readers));
    }

    private string StoreFile() => Path.Combine(_dataFolder, "watch-windows.json");
}
