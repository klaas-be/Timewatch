using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The behaviour every <see cref="IWatchWindowStore"/> owes its callers, run against each
/// implementation. Writing it once is what keeps the fake the engine's tests use honest
/// about the file-backed store the server actually runs.
/// </summary>
public abstract class WatchWindowStoreContract
{
    private static readonly Guid Alice = Guid.Parse("1b9cd0a2-7cf5-4a65-9c4e-9b2d1e9f0a11");
    private static readonly Guid Bob = Guid.Parse("2c0de1b3-8d06-4b76-ad5f-ac3e2f0a1b22");
    private static readonly DateTimeOffset NineOClock = new(2026, 11, 24, 21, 0, 0, TimeSpan.Zero);

    protected abstract IWatchWindowStore NewStore();

    [Fact]
    public async Task No_window_is_active_for_a_user_who_has_not_started_one()
    {
        var store = NewStore();

        Assert.Null(await store.GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task A_started_window_is_the_users_active_window()
    {
        var store = NewStore();

        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        var active = await store.GetActiveWindowAsync(Alice, NineOClock.AddMinutes(30));
        Assert.NotNull(active);
        Assert.Equal(Alice, active.UserId);
        Assert.Equal(NineOClock.AddHours(2), active.EndsAt);
    }

    [Fact]
    public async Task Starting_a_window_replaces_the_one_already_active()
    {
        var store = NewStore();

        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromMinutes(45)));

        var active = await store.GetActiveWindowAsync(Alice, NineOClock);
        Assert.NotNull(active);
        Assert.Equal(NineOClock.AddMinutes(45), active.EndsAt);
    }

    [Fact]
    public async Task Cancelling_leaves_the_user_with_no_active_window()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        await store.CancelWindowAsync(Alice);

        Assert.Null(await store.GetActiveWindowAsync(Alice, NineOClock));
    }

    /// <summary>
    /// A cancelled window and one that ran out both read as absent, so the store has to
    /// remember which happened: the client shows a stopping card for one and goes quietly
    /// inert for the other.
    /// </summary>
    [Fact]
    public async Task A_cancelled_window_is_remembered_as_cancelled()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        await store.CancelWindowAsync(Alice);

        var record = await store.GetLatestWindowAsync(Alice);
        Assert.NotNull(record);
        Assert.True(record.Cancelled);
        Assert.Equal(NineOClock.AddHours(2), record.Window.EndsAt);
    }

    [Fact]
    public async Task A_window_that_was_left_to_run_out_is_not_remembered_as_cancelled()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        var record = await store.GetLatestWindowAsync(Alice);
        Assert.NotNull(record);
        Assert.False(record.Cancelled);
    }

    [Fact]
    public async Task Starting_a_window_after_a_cancel_is_a_window_that_was_not_cancelled()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));
        await store.CancelWindowAsync(Alice);

        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(1)));

        var record = await store.GetLatestWindowAsync(Alice);
        Assert.NotNull(record);
        Assert.False(record.Cancelled);
        Assert.NotNull(await store.GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task A_user_who_never_started_a_window_has_nothing_remembered()
    {
        var store = NewStore();

        Assert.Null(await store.GetLatestWindowAsync(Alice));
    }

    [Fact]
    public async Task Cancelling_when_nothing_is_active_is_not_an_error()
    {
        var store = NewStore();

        await store.CancelWindowAsync(Alice);

        Assert.Null(await store.GetActiveWindowAsync(Alice, NineOClock));
    }

    [Fact]
    public async Task A_window_whose_end_time_has_passed_reads_as_absent()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));

        Assert.NotNull(await store.GetActiveWindowAsync(Alice, NineOClock.AddMinutes(119)));
        Assert.Null(await store.GetActiveWindowAsync(Alice, NineOClock.AddHours(2)));
        Assert.Null(await store.GetActiveWindowAsync(Alice, NineOClock.AddDays(1)));
    }

    [Fact]
    public async Task One_users_window_is_not_another_users_window()
    {
        var store = NewStore();

        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));
        await store.StartWindowAsync(WatchWindow.StartingAt(Bob, NineOClock, TimeSpan.FromMinutes(30)));

        var alice = await store.GetActiveWindowAsync(Alice, NineOClock);
        var bob = await store.GetActiveWindowAsync(Bob, NineOClock);
        Assert.Equal(NineOClock.AddHours(2), alice!.EndsAt);
        Assert.Equal(NineOClock.AddMinutes(30), bob!.EndsAt);
    }

    [Fact]
    public async Task Cancelling_one_users_window_leaves_the_others_running()
    {
        var store = NewStore();
        await store.StartWindowAsync(WatchWindow.StartingAt(Alice, NineOClock, TimeSpan.FromHours(2)));
        await store.StartWindowAsync(WatchWindow.StartingAt(Bob, NineOClock, TimeSpan.FromHours(2)));

        await store.CancelWindowAsync(Alice);

        Assert.Null(await store.GetActiveWindowAsync(Alice, NineOClock));
        Assert.NotNull(await store.GetActiveWindowAsync(Bob, NineOClock));
    }

    [Fact]
    public async Task An_end_time_given_in_a_local_offset_still_means_the_same_instant()
    {
        var store = NewStore();
        var berlinTen = new DateTimeOffset(2026, 11, 24, 23, 0, 0, TimeSpan.FromHours(1));

        await store.StartWindowAsync(new WatchWindow { UserId = Alice, EndsAt = berlinTen });

        var active = await store.GetActiveWindowAsync(Alice, NineOClock);
        Assert.Equal(berlinTen.UtcDateTime, active!.EndsAt.UtcDateTime);
        Assert.Equal(TimeSpan.Zero, active.EndsAt.Offset);
    }

    [Fact]
    public async Task Concurrent_starts_for_different_users_all_survive()
    {
        var store = NewStore();
        var users = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToList();

        await Task.WhenAll(users.Select(user =>
            store.StartWindowAsync(WatchWindow.StartingAt(user, NineOClock, TimeSpan.FromHours(2)))));

        foreach (var user in users)
        {
            Assert.NotNull(await store.GetActiveWindowAsync(user, NineOClock));
        }
    }
}
