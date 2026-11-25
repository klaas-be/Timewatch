using Jellyfin.Plugin.Timewatch.Adapters;
using Jellyfin.Plugin.Timewatch.Domain;
using Jellyfin.Plugin.Timewatch.Storage;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Timewatch;

/// <summary>
/// Binds Timewatch's ports to their Jellyfin adapters in the server's container, so that
/// everything downstream — the API controllers, the SuggestionEngine's callers — depends on
/// the port and never on the adapter. Must have a parameterless constructor and must not
/// throw: Jellyfin marks the plugin Malfunctioned if it does.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Singletons: the Jellyfin services they wrap are themselves singletons and neither
        // adapter holds per-request state.
        serviceCollection.AddSingleton<ICandidateSource, JellyfinCandidateSource>();
        serviceCollection.AddSingleton<IWatchedMarker, JellyfinWatchedMarker>();

        // The data folder is the plugin's own, and only the plugin instance knows it. It is
        // resolved lazily rather than captured here because RegisterServices runs while the
        // container is being built, which is not necessarily after the plugin was
        // constructed; falling back to the host's plugin folder would put an evening's
        // windows somewhere nobody looks, so a missing instance is a hard error — and one
        // raised on first use, where it is logged as a failed request, rather than from
        // RegisterServices, where a throw marks the whole plugin Malfunctioned.
        serviceCollection.AddSingleton<IWatchWindowStore>(_ => new FileWatchWindowStore(
            TimewatchPlugin.Instance?.DataFolderPath
            ?? throw new InvalidOperationException(
                "Timewatch's plugin instance is not available, so its data folder is unknown.")));
    }
}
