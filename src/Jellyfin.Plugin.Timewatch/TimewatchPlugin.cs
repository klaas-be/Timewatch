using Jellyfin.Plugin.Timewatch.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Timewatch;

/// <summary>
/// Timewatch: time-boxed watch suggestions. See GLOSSARY.md for the vocabulary used here.
/// </summary>
public class TimewatchPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public TimewatchPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static TimewatchPlugin? Instance { get; private set; }

    public override string Name => "Timewatch";

    public override Guid Id => Guid.Parse("d5e1c166-10be-467b-95a1-82db3310661a");

    public override string Description =>
        "Set how long you have; get suggestions that fit the time left.";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }
}
