using Jellyfin.Plugin.Timewatch.Adapters;
using Jellyfin.Plugin.Timewatch.Domain;
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
    }
}
