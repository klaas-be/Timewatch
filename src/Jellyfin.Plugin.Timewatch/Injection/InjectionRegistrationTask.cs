using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.Timewatch.Injection;

/// <summary>
/// Registers our two web-client transformations with jellyfin-plugin-file-transformation at
/// server start. Reflection only: a direct assembly reference does not work, because plugins
/// load into separate AssemblyLoadContexts. Deliberately thin and never throwing — a throw
/// out of a scheduled task marks the plugin Malfunctioned.
/// </summary>
public class InjectionRegistrationTask : IScheduledTask
{
    private const string FileTransformationAssemblyMarker = ".FileTransformation";
    private const string PluginInterfaceTypeName = "Jellyfin.Plugin.FileTransformation.PluginInterface";
    private const string RegisterTransformationMethodName = "RegisterTransformation";

    // Fixed ids, so that re-running the task replaces our registrations instead of stacking them.
    private static readonly Guid IndexHtmlTransformationId = new("1a35b0b4-6a9f-4f3e-bd9a-8f6d0d4a0f21");
    private static readonly Guid ConfigJsonTransformationId = new("2b46c1c5-7ba0-4a4f-be0b-9a7e1e5b1a32");

    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<InjectionRegistrationTask> _logger;

    public InjectionRegistrationTask(
        IConfigurationManager configurationManager,
        ILogger<InjectionRegistrationTask> logger)
    {
        _configurationManager = configurationManager;
        _logger = logger;
    }

    /// <summary>
    /// Whether the last run found File Transformation and registered. The dashboard page
    /// reports this, so that a missing dependency is visible and not just logged.
    /// </summary>
    public static bool IsRegistered { get; private set; }

    public string Name => "Register Timewatch web client injection";

    public string Key => "TimewatchInjectionRegistration";

    public string Description =>
        "Registers Timewatch's web client transformations with the File Transformation plugin.";

    public string Category => "Timewatch";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
        [new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger }];

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            Register();
        }
        catch (Exception ex)
        {
            // Never propagate: a throw here marks the whole plugin Malfunctioned.
            IsRegistered = false;
            _logger.LogError(
                ex,
                "Timewatch could not register its web client injection. The End Card will not "
                + "appear, but the rest of the plugin is unaffected");
        }

        progress.Report(100);
        return Task.CompletedTask;
    }

    private void Register()
    {
        var developerMode = TimewatchPlugin.Instance?.Configuration.DeveloperMode ?? false;
        var version = developerMode
            ? DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture)
            : GetType().Assembly.GetName().Version?.ToString() ?? "0";
        var baseUrl = _configurationManager.GetNetworkConfiguration().BaseUrl;

        WebClientPatches.Configure(WebClientPatches.BuildScriptUrl(baseUrl, version));

        var registerTransformation = FindRegisterTransformation();
        if (registerTransformation is null)
        {
            IsRegistered = false;
            _logger.LogError(
                "Timewatch needs the File Transformation plugin to reach the web client, and it "
                + "is not installed. Install \"File Transformation\" (>= 3.0.3.0) from "
                + "https://repo.jellyfin.org/files/plugin/manifest.json and restart the server. "
                + "Until then Timewatch is inert: no End Card, no teaser, no suppression");
            return;
        }

        Invoke(registerTransformation, IndexHtmlTransformationId, @"index\.html", nameof(WebClientPatches.TransformIndexHtml));
        Invoke(registerTransformation, ConfigJsonTransformationId, @"config\.json", nameof(WebClientPatches.TransformConfigJson));

        IsRegistered = true;
        _logger.LogInformation(
            "Timewatch registered its web client injection, serving the script from {ScriptPath}",
            WebClientPatches.ScriptPath);
    }

    private static MethodInfo? FindRegisterTransformation() =>
        AssemblyLoadContext.All
            .SelectMany(context => context.Assemblies)
            .FirstOrDefault(assembly =>
                assembly.FullName?.Contains(FileTransformationAssemblyMarker, StringComparison.Ordinal) ?? false)
            ?.GetType(PluginInterfaceTypeName)
            ?.GetMethod(RegisterTransformationMethodName);

    private void Invoke(MethodInfo registerTransformation, Guid id, string fileNamePattern, string callbackMethod)
    {
        // The payload shape is File Transformation's contract; JObject is Newtonsoft's.
        var payload = new JObject
        {
            { "id", id.ToString() },
            { "fileNamePattern", fileNamePattern },
            { "callbackAssembly", typeof(WebClientPatches).Assembly.FullName },
            { "callbackClass", typeof(WebClientPatches).FullName },
            { "callbackMethod", callbackMethod }
        };

        registerTransformation.Invoke(null, [payload]);
        _logger.LogDebug("Timewatch registered a transformation for {Pattern}", fileNamePattern);
    }
}
