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

    // Set by run_failed_tests when there is no previous run TRX for this project
    public bool NoHistory { get; init; }

    // Set by run_failed_tests when the previous run had 0 failures
    public bool NoPreviousFailures { get; init; }
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
