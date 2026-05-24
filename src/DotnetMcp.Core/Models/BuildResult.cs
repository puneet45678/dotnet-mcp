namespace DotnetMcp.Core.Models;

public sealed record BuildResult
{
    public bool Success { get; init; }
    public int ErrorCount => Errors.Count;
    public int WarningCount => Warnings.Count;
    public List<BuildDiagnostic> Errors { get; init; } = [];
    public List<BuildDiagnostic> Warnings { get; init; } = [];
}

public sealed record BuildDiagnostic
{
    public string Code { get; init; } = "";
    public string File { get; init; } = "";
    public int Line { get; init; }
    public int Column { get; init; }
    public string Message { get; init; } = "";
}
