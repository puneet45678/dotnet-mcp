namespace DotnetMcp.Core.Models;

public sealed class PluginManifest
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "0.1.0";
    public string Description { get; init; } = "";
    public string Assembly { get; init; } = "";
    public List<PluginToolInfo> Tools { get; init; } = [];
}

public sealed class PluginToolInfo
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
}
