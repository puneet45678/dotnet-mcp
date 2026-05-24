namespace DotnetMcp.Testing.Tests;

public class TestRunnerToolsTests : PluginTestBase
{
    // ==========================================================================
    // Scenario: Developer runs the full test suite to check health before a PR
    // ==========================================================================

    [Fact]
    public async Task RunTests_AllPassing_ReturnsSuccessWithCounts()
    {
        var result = await TestRunnerTools.RunTests(SamplePath("SampleTestProject"));

        Assert.True(result.Success);
        Assert.True(result.Total > 0);
        Assert.Equal(result.Total, result.Passed + result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public async Task RunTests_MixedResults_ReportsFailuresWithDetail()
    {
        var result = await TestRunnerTools.RunTests(SamplePath("SampleFailingProject"));

        Assert.False(result.Success);
        Assert.True(result.Failed >= 2);
        Assert.Contains(result.Failures, f => f.TestName.Contains("Add_ShouldFail"));
        Assert.Contains(result.Failures, f => f.TestName.Contains("Contains_ShouldFail"));
    }

    [Fact]
    public async Task RunTests_FailingProject_FailuresIncludeAssertionMessages()
    {
        var result = await TestRunnerTools.RunTests(SamplePath("SampleFailingProject"));

        Assert.All(result.Failures, f => Assert.NotNull(f.Message));
        Assert.All(result.Failures, f => Assert.Contains("Assert", f.Message!));
    }

    // ==========================================================================
    // Scenario: Developer isolates one test class after a refactor
    // ==========================================================================

    [Fact]
    public async Task RunTests_FilterByClassName_RunsOnlyThatClass()
    {
        var result = await TestRunnerTools.RunTests(
            SamplePath("SampleFailingProject"),
            filter: "FullyQualifiedName~MathTests");

        Assert.False(result.Success);
        Assert.Contains(result.Failures, f => f.TestName.Contains("Add_ShouldFail"));
        Assert.DoesNotContain(result.Failures, f => f.TestName.Contains("Contains_ShouldFail"));
    }

    [Fact]
    public async Task RunTests_FilterToPassingClass_ReportsSuccess()
    {
        var result = await TestRunnerTools.RunTests(
            SamplePath("SampleFailingProject"),
            filter: "FullyQualifiedName~StringTests.Concat_ShouldPass");

        Assert.True(result.Success);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task RunTests_ReleaseConfiguration_PassesWithSameResults()
    {
        var result = await TestRunnerTools.RunTests(
            SamplePath("SampleTestProject"),
            configuration: "Release");

        Assert.True(result.Success);
        Assert.Equal(0, result.Failed);
    }

    // ==========================================================================
    // Scenario: Developer lists tests before deciding which to run
    // ==========================================================================

    [Fact]
    public async Task ListTests_ReturnsAllTestNames()
    {
        var result = await TestRunnerTools.ListTests(SamplePath("SampleTestProject"));

        Assert.True(result.Count > 0);
        Assert.Contains(result.Tests, t => t.Contains("Add_ReturnsSum"));
        Assert.Contains(result.Tests, t => t.Contains("Divide_ByZero_ThrowsDivideByZero"));
        Assert.Contains(result.Tests, t => t.Contains("Process_SmallAmount_ReturnsCompleted"));
    }

    [Fact]
    public async Task ListTests_ReportsAccurateCount()
    {
        var result = await TestRunnerTools.ListTests(SamplePath("SampleTestProject"));
        Assert.Equal(13, result.Count);
    }

    [Fact]
    public async Task ListTests_FailingProjectIncludesBothClasses()
    {
        var result = await TestRunnerTools.ListTests(SamplePath("SampleFailingProject"));

        Assert.Contains(result.Tests, t => t.Contains("MathTests"));
        Assert.Contains(result.Tests, t => t.Contains("StringTests"));
        Assert.Contains(result.Tests, t => t.Contains("Add_ShouldFail"));
    }

    // ==========================================================================
    // Scenario: Developer re-runs failed tests after a fix
    // ==========================================================================

    [Fact]
    public async Task RunFailedTests_ProjectWithNoFailures_ReturnsSuccess()
    {
        await TestRunnerTools.RunTests(SamplePath("SampleTestProject"));
        var result = await TestRunnerTools.RunFailedTests(SamplePath("SampleTestProject"));

        Assert.True(result.Success || result.Total == 0);
        Assert.Equal(0, result.Failed);
    }

    // ==========================================================================
    // Scenario: Tech lead wants coverage report before merging a PR
    // ==========================================================================

    [Fact]
    public async Task GetCoverageSummary_ReturnsLineCoverage()
    {
        var result = await TestRunnerTools.GetCoverageSummary(SamplePath("SampleTestProject"));

        if (result.LineCoverage == 0 && result.Assemblies.Count == 0)
            return; // coverlet not installed — skip gracefully

        Assert.True(result.LineCoverage > 0);
        Assert.NotEmpty(result.Assemblies);
    }

    [Fact]
    public async Task GetCoverageSummary_AssembliesHaveNames()
    {
        var result = await TestRunnerTools.GetCoverageSummary(SamplePath("SampleTestProject"));

        if (result.Assemblies.Count == 0) return;

        Assert.All(result.Assemblies, a => Assert.NotEmpty(a.Name));
    }

    // ==========================================================================
    // Scenario: Developer reads a saved TRX file from CI
    // ==========================================================================

    [Fact]
    public async Task GetTestSummary_GeneratedTrxFile_ParsesPassFailCounts()
    {
        var trxFile = Path.Combine(Path.GetTempPath(), $"dotnet-mcp-{Guid.NewGuid():N}.trx");

        try
        {
            await DotnetMcp.Core.Utilities.ProcessRunner.RunAsync(
                "dotnet",
                $"test \"{SamplePath("SampleTestProject")}\" --logger \"trx;LogFileName={trxFile}\"");

            Assert.True(File.Exists(trxFile), "TRX not generated");

            var result = await TestRunnerTools.GetTestSummary(trxFile);

            Assert.True(result.Success);
            Assert.True(result.Total > 0);
            Assert.Equal(0, result.Failed);
        }
        finally
        {
            try { File.Delete(trxFile); } catch { }
        }
    }

    [Fact]
    public async Task GetTestSummary_TrxWithFailures_ShowsFailedTestDetails()
    {
        var trxFile = Path.Combine(Path.GetTempPath(), $"dotnet-mcp-{Guid.NewGuid():N}.trx");

        try
        {
            await DotnetMcp.Core.Utilities.ProcessRunner.RunAsync(
                "dotnet",
                $"test \"{SamplePath("SampleFailingProject")}\" --logger \"trx;LogFileName={trxFile}\"");

            Assert.True(File.Exists(trxFile), "TRX not generated");

            var result = await TestRunnerTools.GetTestSummary(trxFile);

            Assert.False(result.Success);
            Assert.True(result.Failed >= 2);
            Assert.All(result.Failures, f => Assert.NotEmpty(f.TestName));
        }
        finally
        {
            try { File.Delete(trxFile); } catch { }
        }
    }

    [Fact]
    public async Task GetTestSummary_MissingFile_ReturnsEmptyResult()
    {
        var result = await TestRunnerTools.GetTestSummary("/nonexistent/path/results.trx");
        Assert.Equal(0, result.Total);
    }
}
