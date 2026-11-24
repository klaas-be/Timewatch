using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Tests;

/// <summary>
/// The in-memory double of <see cref="IWatchWindowStore"/> that the engine's tests drive.
/// It touches no disk, and <see cref="WatchWindowStoreContract"/> holds it to the same
/// behaviour as the file-backed store.
/// </summary>
public sealed class FakeWatchWindowStore : IWatchWindowStore
{
    private readonly Dictionary<Guid, WatchWindow> _windows = [];

    public Task<WatchWindow?> GetActiveWindowAsync(
        Guid userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var window = _windows.GetValueOrDefault(userId);

        return Task.FromResult(window is null || window.HasExpiredAt(asOf) ? null : window);
    }

    public Task StartWindowAsync(WatchWindow window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);
        _windows[window.UserId] = window;

        return Task.CompletedTask;
    }

    public Task CancelWindowAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _windows.Remove(userId);

        return Task.CompletedTask;
    }
}
