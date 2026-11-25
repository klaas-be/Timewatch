namespace Jellyfin.Plugin.Timewatch.Injection;

/// <summary>
/// What File Transformation hands a callback: a single object that
/// <c>{ "contents": "&lt;file text&gt;" }</c> deserialises into. Each plugin declares its
/// own; there is no shared type, because the two assemblies live in different
/// AssemblyLoadContexts.
/// </summary>
public sealed class FileTransformationPayload
{
    /// <summary>The whole current contents of the file being served.</summary>
    public string? Contents { get; set; }
}
