using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.Timewatch.Domain;

namespace Jellyfin.Plugin.Timewatch.Storage;

/// <summary>
/// The Watch Window store the server runs: one JSON file, <c>watch-windows.json</c>, in the
/// plugin's data folder, holding one entry per user with the end time in UTC.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not the plugin configuration XML. That file is the admin's dashboard
/// settings; rewriting it every time somebody starts a window would make an evening's
/// state look like a configuration change.
/// </para>
/// <para>
/// It takes a plain directory path rather than the plugin, so the folder Jellyfin hands us
/// through <c>BasePlugin.DataFolderPath</c> is the only thing that couples it to the host
/// and the whole store is testable against a temporary directory.
/// </para>
/// <para>
/// Nothing sweeps expired entries: a window that is over reads as absent, and starting a
/// new one replaces the old entry, so the file stays bounded by the number of users who
/// have ever started a window.
/// </para>
/// </remarks>
public sealed class FileWatchWindowStore : IWatchWindowStore
{
    /// <summary>The file name inside the plugin's data folder.</summary>
    public const string FileName = "watch-windows.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// One gate per file, not per instance: several <see cref="FileWatchWindowStore"/>
    /// objects over the same folder — a scoped controller dependency, a test — must still
    /// take turns, because every write is a read-modify-write of the whole file.
    /// </summary>
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    private readonly string _dataFolderPath;
    private readonly string _filePath;

    /// <param name="dataFolderPath">
    /// The directory to keep the file in; <c>BasePlugin.DataFolderPath</c> in the server.
    /// It is created on first write if the host has not created it.
    /// </param>
    public FileWatchWindowStore(string dataFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolderPath);

        _dataFolderPath = Path.GetFullPath(dataFolderPath);
        _filePath = Path.Combine(_dataFolderPath, FileName);
    }

    /// <inheritdoc />
    public async Task<WatchWindow?> GetActiveWindowAsync(
        Guid userId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        var record = await GetLatestWindowAsync(userId, cancellationToken).ConfigureAwait(false);

        return record is not null && record.IsActiveAt(asOf) ? record.Window : null;
    }

    /// <inheritdoc />
    public async Task<WatchWindowRecord?> GetLatestWindowAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entries = await WithFileAsync(ReadAsync, cancellationToken).ConfigureAwait(false);

        return entries.FirstOrDefault(entry => entry.UserId == userId)?.ToRecord();
    }

    /// <inheritdoc />
    public Task StartWindowAsync(WatchWindow window, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(window);

        return WithFileAsync(
            async token =>
            {
                var entries = await ReadAsync(token).ConfigureAwait(false);
                entries.RemoveAll(entry => entry.UserId == window.UserId);
                entries.Add(StoredWindow.From(window));
                await WriteAsync(entries, token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task CancelWindowAsync(Guid userId, CancellationToken cancellationToken = default)
        => WithFileAsync(
            async token =>
            {
                var entries = await ReadAsync(token).ConfigureAwait(false);
                var cancelled = entries.Where(entry => entry.UserId == userId && !entry.Cancelled).ToList();
                if (cancelled.Count > 0)
                {
                    // Kept rather than removed, with the cancellation recorded: a client
                    // that was running this window has to be able to find out that it went
                    // away because the user said so, not because the time ran out.
                    cancelled.ForEach(entry => entry.Cancelled = true);
                    await WriteAsync(entries, token).ConfigureAwait(false);
                }
            },
            cancellationToken);

    /// <summary>
    /// Reads the file. A missing, empty, truncated or otherwise unreadable file means "no
    /// windows": a damaged file costs the user their evening's window, which is recoverable
    /// by starting another, whereas throwing would break every request that asks for status.
    /// </summary>
    private async Task<List<StoredWindow>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var document = await JsonSerializer
                .DeserializeAsync<StoredWindows>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return document?.Windows?.Where(entry => entry is not null).ToList() ?? [];
        }
        catch (Exception ex) when (ex is FileNotFoundException
                                      or DirectoryNotFoundException
                                      or JsonException
                                      or FormatException)
        {
            return [];
        }
    }

    /// <summary>
    /// Writes the whole set to a temporary file in the same directory and renames it over
    /// the real one. The rename is atomic, so a crash or a concurrent reader sees either the
    /// previous file or the new one in full — never a truncated prefix of either.
    /// </summary>
    private async Task WriteAsync(List<StoredWindow> entries, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_dataFolderPath);

        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await JsonSerializer
                    .SerializeAsync(stream, new StoredWindows { Windows = entries }, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        catch
        {
            File.Delete(temporaryPath);
            throw;
        }
    }

    private async Task<T> WithFileAsync<T>(
        Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken)
    {
        var gate = Gates.GetOrAdd(_filePath, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await work(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task WithFileAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
        => WithFileAsync<bool>(
            async token =>
            {
                await work(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);

    /// <summary>
    /// The file's shape, kept separate from the domain record so the on-disk format can
    /// gain fields without the domain gaining them.
    /// </summary>
    private sealed class StoredWindows
    {
        [JsonPropertyName("windows")]
        public List<StoredWindow>? Windows { get; set; }
    }

    private sealed class StoredWindow
    {
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("endsAt")]
        public DateTimeOffset EndsAt { get; set; }

        /// <summary>
        /// Whether the user cancelled this window. Absent in a file written by an earlier
        /// version, which reads as false: those windows all ran out.
        /// </summary>
        [JsonPropertyName("cancelled")]
        public bool Cancelled { get; set; }

        public static StoredWindow From(WatchWindow window) =>
            new() { UserId = window.UserId, EndsAt = window.EndsAt };

        /// <summary>
        /// <see cref="WatchWindow.EndsAt"/> normalises to UTC itself, so a stored offset
        /// cannot change which instant the window ends at.
        /// </summary>
        public WatchWindow ToWindow() => new() { UserId = UserId, EndsAt = EndsAt };

        public WatchWindowRecord ToRecord() => new() { Window = ToWindow(), Cancelled = Cancelled };
    }
}
