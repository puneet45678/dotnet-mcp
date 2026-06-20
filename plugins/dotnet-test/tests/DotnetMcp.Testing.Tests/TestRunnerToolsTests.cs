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
    public async Task RunFailedTests_AfterCleanRun_ReportsNoPreviousFailures()
    {
        // Establish a clean baseline by running all tests first
        await TestRunnerTools.RunTests(SamplePath("SampleTestProject"));

        var result = await TestRunnerTools.RunFailedTests(SamplePath("SampleTestProject"));

        // With a clean previous run, either 0 failed tests or the tool reports NoPreviousFailures
        Assert.True(
            result.NoPreviousFailures || result.Failed == 0,
            $"Expected no failures to re-run; got Failed={result.Failed}");
    }

    [Fact]
    public async Task RunFailedTests_AfterFailingRun_ReRunsOnlyFailedTests()
    {
        // Establish a baseline with known failures
        await TestRunnerTools.RunTests(SamplePath("SampleFailingProject"));

        var result = await TestRunnerTools.RunFailedTests(SamplePath("SampleFailingProject"));

        // Should have re-run the 2 failed tests and still report them as failed
        Assert.True(result.Total > 0, "Expected failed tests to be re-run");
        Assert.True(result.Failed >= 1, "Re-run of failing tests should still fail");
        // Should NOT have re-run the 4 passing tests (total should be < 6)
        Assert.True(result.Total < 6, $"Expected only failing tests to be re-run, got Total={result.Total}");
    }

    [Fact]
    public async Task RunFailedTests_NoHistory_ReportsNoHistory()
    {
        // Copy SampleTestProject to a clean temp dir — deliberately skip TestResults/
        // so there's no inherited TRX from earlier tests in this run.
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotnet-mcp-nohistory-{Guid.NewGuid():N}");
        CopyDirectory(SamplePath("SampleTestProject"), tempDir, exclude: "TestResults");
        try
        {
            var result = await TestRunnerTools.RunFailedTests(tempDir);
            Assert.True(result.NoHistory, "Fresh project with no run history should set NoHistory=true");
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    // Copies a directory tree. Pass exclude to skip a named subdirectory (e.g. "TestResults").
    private static void CopyDirectory(string src, string dest, string? exclude = null)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(src))
        {
            if (Path.GetFileName(dir) == exclude) continue;
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), exclude);
        }
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

    // ==========================================================================
    // Scenario: CI shows intermittent failures — developer uses detect_flaky_tests
    // ==========================================================================

    [Fact]
    public async Task DetectFlakyTests_WithAlternatingTest_IdentifiesItAsFlaky()
    {
        // SampleFlakyProject has one test that alternates pass/fail each dotnet test invocation.
        // Running it 4 times guarantees 2 passes and 2 failures → detected as flaky.
        var result = await TestRunnerTools.DetectFlakyTests(
            SamplePath("SampleFlakyProject"),
            runs: 4);

        Assert.Equal(4, result.TotalRuns);
        Assert.Single(result.FlakyTests);
        Assert.Contains(result.FlakyTests, f => f.TestName.Contains("Flaky_AlternatesEachRun"));
    }

    [Fact]
    public async Task DetectFlakyTests_FlakyTest_ReportsCorrectPassFailCounts()
    {
        var result = await TestRunnerTools.DetectFlakyTests(
            SamplePath("SampleFlakyProject"),
            runs: 4);

        var flaky = result.FlakyTests.Single(f => f.TestName.Contains("Flaky_AlternatesEachRun"));

        // 4 runs alternating → 2 passes, 2 failures
        Assert.Equal(2, flaky.Passes);
        Assert.Equal(2, flaky.Failures);
    }

    [Fact]
    public async Task DetectFlakyTests_StableTests_DoNotAppearInFlakyList()
    {
        var result = await TestRunnerTools.DetectFlakyTests(
            SamplePath("SampleFlakyProject"),
            runs: 4);

        // StablePass always passes → consistent → not flaky
        Assert.DoesNotContain(result.FlakyTests, f => f.TestName.Contains("StablePass"));
        // StableFail always fails → consistently broken, not flaky
        Assert.DoesNotContain(result.FlakyTests, f => f.TestName.Contains("StableFail"));
    }

    [Fact]
    public async Task DetectFlakyTests_FlakyTest_IncludesFailureMessage()
    {
        var result = await TestRunnerTools.DetectFlakyTests(
            SamplePath("SampleFlakyProject"),
            runs: 4);

        var flaky = result.FlakyTests.Single(f => f.TestName.Contains("Flaky_AlternatesEachRun"));

        Assert.NotNull(flaky.SampleFailureMessage);
        Assert.Contains("Odd-numbered invocation", flaky.SampleFailureMessage);
    }

    [Fact]
    public async Task DetectFlakyTests_CleanProject_ReportsNoFlakyTests()
    {
        var result = await TestRunnerTools.DetectFlakyTests(
            SamplePath("SampleTestProject"),
            runs: 3);

        Assert.Empty(result.FlakyTests);
        Assert.True(result.TotalTests > 0, "Should have found tests in SampleTestProject");
    }
}
