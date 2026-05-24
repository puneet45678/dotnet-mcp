namespace DotnetMcp.Core.Models;

public sealed record TestRunResult
{
    public bool Success { get; init; }
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public int Skipped { get; init; }
    public string? Filter { get; init; }
    public List<TestFailure> Failures { get; init; } = [];
}

public sealed record TestFailure
{
    public string TestName { get; init; } = "";
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
}

public sealed record TestListResult
{
    public int Count => Tests.Count;
    public List<string> Tests { get; init; } = [];
}
