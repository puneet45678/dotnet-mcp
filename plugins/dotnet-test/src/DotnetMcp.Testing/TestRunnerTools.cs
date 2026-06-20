using System.ComponentModel;
using System.Xml.Linq;
using DotnetMcp.Core.Models;
using DotnetMcp.Core.Utilities;
using ModelContextProtocol.Server;



namespace DotnetMcp.Testing;

[McpServerToolType]
public static class TestRunnerTools
{
    [McpServerTool(Name = "run_tests", Idempotent = true, Destructive = false)]
    [Description("Run dotnet tests for a project or solution. Returns pass/fail/skip counts and failure details with stack traces.")]
    public static async Task<TestRunResult> RunTests(
        [Description("Path to the .csproj, .sln, or directory to test")] string projectPath,
        [Description("Optional test filter expression (e.g. 'FullyQualifiedName~MyTest')")] string? filter = null,
        [Description("Build configuration: Debug or Release (default: Debug)")] string configuration = "Debug",
        CancellationToken cancellationToken = default)
    {
        return await RunWithTrx(projectPath, filter, configuration, cancellationToken);
    }

    [McpServerTool(Name = "run_failed_tests", Idempotent = true, Destructive = false)]
    [Description("Re-run only the tests that failed in the last run_tests call for this project. Returns 'No previously failed tests' if the project has a clean run history.")]
    public static async Task<TestRunResult> RunFailedTests(
        [Description("Path to the .csproj, .sln, or directory to test")] string projectPath,
        [Description("Build configuration: Debug or Release (default: Debug)")] string configuration = "Debug",
        CancellationToken cancellationToken = default)
    {
        // Read the TRX that run_tests persisted for this project
        var persistentTrx = PersistentTrxPath(projectPath);
        if (!File.Exists(persistentTrx))
            return new TestRunResult { Success = true, NoHistory = true };

        var previous = ParseTrx(persistentTrx, filter: null);
        if (previous.Failures.Count == 0)
            return new TestRunResult { Success = true, Total = 0, NoPreviousFailures = true };

        // Build a FullyQualifiedName filter from the failed names so it works with all frameworks
        var filterParts = previous.Failures
            .Select(f => $"FullyQualifiedName~{f.TestName.Replace(" ", "_")}");
        var filter = string.Join("|", filterParts);

        return await RunWithTrx(projectPath, filter, configuration, cancellationToken);
    }

    [McpServerTool(Name = "list_tests", ReadOnly = true, Destructive = false)]
    [Description("List all test names in a project or solution without running them.")]
    public static async Task<TestListResult> ListTests(
        [Description("Path to the .csproj, .sln, or directory")] string projectPath,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet", $"test \"{projectPath}\" --list-tests", cancellationToken: cancellationToken);

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var startIdx = Array.FindIndex(lines, l => l.Contains("The following Tests are available"));
        var testLines = startIdx >= 0 ? lines[(startIdx + 1)..] : lines;
        var tests = testLines.Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        return new TestListResult { Tests = tests };
    }

    [McpServerTool(Name = "get_coverage_summary", Idempotent = true, Destructive = false)]
    [Description("Run tests with Coverlet and return line and branch coverage percentages per assembly. Requires coverlet.collector in the test project.")]
    public static async Task<CoverageResult> GetCoverageSummary(
        [Description("Path to the .csproj, .sln, or directory to test")] string projectPath,
        CancellationToken cancellationToken = default)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), $"dotnet-mcp-coverage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var result = await ProcessRunner.RunAsync(
                "dotnet",
                $"test \"{projectPath}\" --collect:\"XPlat Code Coverage\" --results-directory \"{outputDir}\"",
                cancellationToken: cancellationToken);

            var coverageFile = Directory.GetFiles(outputDir, "coverage.cobertura.xml", SearchOption.AllDirectories)
                .FirstOrDefault();

            if (coverageFile is null)
                return new CoverageResult();

            return ParseCobertura(coverageFile);
        }
        finally
        {
            try { Directory.Delete(outputDir, recursive: true); } catch { }
        }
    }

    [McpServerTool(Name = "detect_flaky_tests", Idempotent = true, Destructive = false)]
    [Description("Run the test suite multiple times and identify tests with inconsistent results. A test is considered flaky if it passes in some runs and fails in others.")]
    public static async Task<FlakeResult> DetectFlakyTests(
        [Description("Path to the .csproj, .sln, or directory to test")] string projectPath,
        [Description("Number of times to run the suite (default: 5, min: 2, max: 20)")] int runs = 5,
        [Description("Optional test filter to narrow which tests are exercised")] string? filter = null,
        [Description("Build configuration: Debug or Release (default: Debug)")] string configuration = "Debug",
        CancellationToken cancellationToken = default)
    {
        runs = Math.Clamp(runs, 2, 20);

        // Build once up-front so the repeated runs skip compilation
        await ProcessRunner.RunAsync(
            "dotnet",
            $"build \"{projectPath}\" --configuration {configuration} -q",
            cancellationToken: cancellationToken);

        // Collect per-test outcomes for each run: testName → [(outcome, message)]
        var history = new Dictionary<string, List<(string Outcome, string? Message)>>();

        for (int i = 0; i < runs; i++)
        {
            var results = await RunAndGetOutcomes(projectPath, filter, configuration, cancellationToken);
            foreach (var (testName, outcome, message) in results)
            {
                if (!history.TryGetValue(testName, out var list))
                    history[testName] = list = [];
                list.Add((outcome, message));
            }
        }

        var flaky = history
            .Where(kv => kv.Value.Any(r => r.Outcome == "Passed") &&
                         kv.Value.Any(r => r.Outcome == "Failed"))
            .Select(kv => new FlakyTest
            {
                TestName             = kv.Key,
                Passes               = kv.Value.Count(r => r.Outcome == "Passed"),
                Failures             = kv.Value.Count(r => r.Outcome == "Failed"),
                SampleFailureMessage = kv.Value.FirstOrDefault(r => r.Outcome == "Failed").Message,
            })
            .OrderByDescending(t => t.Failures)
            .ToList();

        return new FlakeResult
        {
            TotalRuns  = runs,
            TotalTests = history.Count,
            FlakyTests = flaky,
        };
    }

    [McpServerTool(Name = "get_test_summary", ReadOnly = true, Destructive = false)]
    [Description("Parse an existing TRX test results file and return a structured pass/fail/skip summary with failure details.")]
    public static Task<TestRunResult> GetTestSummary(
        [Description("Path to the .trx results file")] string trxPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(trxPath))
            return Task.FromResult(new TestRunResult());

        return Task.FromResult(ParseTrx(trxPath, filter: null));
    }

    // --- private helpers ---

    private static async Task<TestRunResult> RunWithTrx(
        string projectPath, string? filter, string configuration, CancellationToken ct)
    {
        var trxPath = Path.Combine(Path.GetTempPath(), $"dotnet-mcp-{Guid.NewGuid():N}.trx");

        try
        {
            var args = $"test \"{projectPath}\" --configuration {configuration} --logger \"trx;LogFileName={trxPath}\"";
            if (!string.IsNullOrWhiteSpace(filter))
                args += $" --filter \"{filter}\"";

            await ProcessRunner.RunAsync("dotnet", args, cancellationToken: ct);

            if (!File.Exists(trxPath))
                return new TestRunResult { Success = false };

            var result = ParseTrx(trxPath, filter);

            // Persist a stable TRX for run_failed_tests — only when running without a filter
            // so we don't overwrite the baseline with a partial re-run.
            if (string.IsNullOrWhiteSpace(filter))
            {
                try
                {
                    var persistentTrx = PersistentTrxPath(projectPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(persistentTrx)!);
                    File.Copy(trxPath, persistentTrx, overwrite: true);
                }
                catch { /* best-effort */ }
            }

            return result;
        }
        finally
        {
            try { File.Delete(trxPath); } catch { }
        }
    }

    // Runs the tests once and returns a list of (testName, outcome, failureMessage) tuples.
    // Uses --no-build because DetectFlakyTests builds up-front and re-uses the output.
    private static async Task<List<(string TestName, string Outcome, string? Message)>>
        RunAndGetOutcomes(string projectPath, string? filter, string configuration, CancellationToken ct)
    {
        var trxPath = Path.Combine(Path.GetTempPath(), $"dotnet-mcp-flake-{Guid.NewGuid():N}.trx");
        try
        {
            var args = $"test \"{projectPath}\" --configuration {configuration} --no-build" +
                       $" --logger \"trx;LogFileName={trxPath}\"";
            if (!string.IsNullOrWhiteSpace(filter))
                args += $" --filter \"{filter}\"";

            await ProcessRunner.RunAsync("dotnet", args, cancellationToken: ct);

            if (!File.Exists(trxPath)) return [];

            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var doc = XDocument.Load(trxPath);
            return doc.Descendants(ns + "UnitTestResult")
                .Select(r => (
                    TestName: r.Attribute("testName")?.Value ?? "",
                    Outcome:  r.Attribute("outcome")?.Value  ?? "Unknown",
                    Message:  r.Descendants(ns + "Message").FirstOrDefault()?.Value?.Trim()
                ))
                .Where(r => r.TestName.Length > 0)
                .ToList();
        }
        finally
        {
            try { File.Delete(trxPath); } catch { /* best-effort */ }
        }
    }

    // Returns a stable TRX path scoped to this project for run_failed_tests to consume.
    private static string PersistentTrxPath(string projectPath)
    {
        // Resolve to a directory: .csproj → its directory, directory → itself
        var dir = File.Exists(projectPath) ? Path.GetDirectoryName(projectPath)! : projectPath;
        return Path.Combine(dir, "TestResults", "dotnet-mcp-last-run.trx");
    }

    private static TestRunResult ParseTrx(string trxPath, string? filter)
    {
        XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
        var doc = XDocument.Load(trxPath);

        var counters = doc.Descendants(ns + "Counters").FirstOrDefault();
        var total   = int.TryParse(counters?.Attribute("total")?.Value,       out var t) ? t : 0;
        var passed  = int.TryParse(counters?.Attribute("passed")?.Value,      out var p) ? p : 0;
        var failed  = int.TryParse(counters?.Attribute("failed")?.Value,      out var f) ? f : 0;
        var skipped = int.TryParse(counters?.Attribute("notExecuted")?.Value, out var s) ? s : 0;

        var failures = doc.Descendants(ns + "UnitTestResult")
            .Where(r => r.Attribute("outcome")?.Value == "Failed")
            .Select(r => new TestFailure
            {
                TestName   = r.Attribute("testName")?.Value ?? "",
                Message    = r.Descendants(ns + "Message").FirstOrDefault()?.Value?.Trim(),
                StackTrace = r.Descendants(ns + "StackTrace").FirstOrDefault()?.Value?.Trim(),
            })
            .ToList();

        return new TestRunResult
        {
            Success  = failed == 0,
            Total    = total,
            Passed   = passed,
            Failed   = failed,
            Skipped  = skipped,
            Filter   = filter,
            Failures = failures,
        };
    }

    private static CoverageResult ParseCobertura(string xmlPath)
    {
        var doc = XDocument.Load(xmlPath);
        var root = doc.Root;
        if (root is null) return new CoverageResult();

        double Rate(XElement? el, string attr) =>
            double.TryParse(el?.Attribute(attr)?.Value, out var v) ? Math.Round(v * 100, 1) : 0;

        var assemblies = doc.Descendants("package").Select(pkg => new AssemblyCoverage
        {
            Name           = pkg.Attribute("name")?.Value ?? "",
            LineCoverage   = Rate(pkg, "line-rate"),
            BranchCoverage = Rate(pkg, "branch-rate"),
            LowCoverageClasses = pkg.Descendants("class")
                .Select(c => new ClassCoverage
                {
                    Name         = c.Attribute("name")?.Value ?? "",
                    LineCoverage = Rate(c, "line-rate"),
                })
                .Where(c => c.LineCoverage < 80)
                .ToList(),
        }).ToList();

        return new CoverageResult
        {
            LineCoverage   = Rate(root, "line-rate"),
            BranchCoverage = Rate(root, "branch-rate"),
            Assemblies     = assemblies,
        };
    }
}
