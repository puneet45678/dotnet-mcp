namespace DotnetMcp.Core.Models;

public sealed record PackageListResult
{
    public List<ProjectPackages> Projects { get; init; } = [];
}

public sealed record ProjectPackages
{
    public string Project { get; init; } = "";
    public List<InstalledPackage> Packages { get; init; } = [];
}

public sealed record InstalledPackage
{
    public string Name { get; init; } = "";
    public string Requested { get; init; } = "";
    public string Resolved { get; init; } = "";
}

public sealed record OutdatedResult
{
    public bool AllUpToDate { get; init; }
    public List<OutdatedPackage> Packages { get; init; } = [];
}

public sealed record OutdatedPackage
{
    public string Name { get; init; } = "";
    public string Current { get; init; } = "";
    public string Latest { get; init; } = "";
}
