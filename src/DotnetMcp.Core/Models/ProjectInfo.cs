namespace DotnetMcp.Core.Models;

public sealed record ProjectInfo
{
    public string Name { get; init; } = "";
    public string? Sdk { get; init; }
    public string? TargetFramework { get; init; }
    public string? TargetFrameworks { get; init; }
    public string? OutputType { get; init; }
    public string? AssemblyName { get; init; }
    public string? Nullable { get; init; }
    public string? LangVersion { get; init; }
    public List<PackageRef> Packages { get; init; } = [];
    public List<string> ProjectReferences { get; init; } = [];
}

public sealed record PackageRef(string Name, string Version);

public sealed record ProjectListResult
{
    public string Solution { get; init; } = "";
    public List<string> Projects { get; init; } = [];
}
