namespace DotnetMcp.Core.Models;

public sealed record CoverageResult
{
    public double LineCoverage { get; init; }
    public double BranchCoverage { get; init; }
    public List<AssemblyCoverage> Assemblies { get; init; } = [];
}

public sealed record AssemblyCoverage
{
    public string Name { get; init; } = "";
    public double LineCoverage { get; init; }
    public double BranchCoverage { get; init; }
    public List<ClassCoverage> LowCoverageClasses { get; init; } = [];
}

public sealed record ClassCoverage
{
    public string Name { get; init; } = "";
    public double LineCoverage { get; init; }
}
