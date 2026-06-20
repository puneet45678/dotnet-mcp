namespace DotnetMcp.Core.Models;

public sealed record FlakeResult
{
    /// <summary>Number of times the suite was executed.</summary>
    public int TotalRuns { get; init; }

    /// <summary>Distinct tests observed across all runs.</summary>
    public int TotalTests { get; init; }

    /// <summary>Tests that passed in some runs and failed in others.</summary>
    public List<FlakyTest> FlakyTests { get; init; } = [];
}

public sealed record FlakyTest
{
    public string TestName { get; init; } = "";

    /// <summary>How many runs this test passed.</summary>
    public int Passes { get; init; }

    /// <summary>How many runs this test failed.</summary>
    public int Failures { get; init; }

    /// <summary>Failure message from the first observed failure, to help diagnose root cause.</summary>
    public string? SampleFailureMessage { get; init; }
}
