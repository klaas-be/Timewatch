using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The in-memory double of <see cref="IWatchWindowStore"/> that the engine's tests drive.
/// It touches no disk, and <see cref="WatchWindowStoreContract"/> holds it to the same
/// behaviour as the file-backed store.
/// </summary>
public sealed class FakeWatchWindowStore : IWatchWindowStore
{
    private readonly Dictionary<Guid, WatchWindowRecord> _windows = [];

    public Task<WatchWindow?> GetActiveWindowAsync(
        Guid userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var record = _windows.GetValueOrDefault(userId);

        return Task.FromResult(record is not null && record.IsActiveAt(asOf) ? record.Window : null);
    }

    public Task<WatchWindowRecord?> GetLatestWindowAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_windows.GetValueOrDefault(userId));

    public Task StartWindowAsync(WatchWindow window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windows[window.UserId] = new WatchWindowRecord { Window = window, Cancelled = false };

        return Task.CompletedTask;
    }

    public Task CancelWindowAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_windows.GetValueOrDefault(userId) is { } record)
        {
            _windows[userId] = record with { Cancelled = true };
        }

        return Task.CompletedTask;
    }
}
