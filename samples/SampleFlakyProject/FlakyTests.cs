namespace SampleFlakyProject;

/// <summary>
/// Sample project used by detect_flaky_tests integration tests.
/// Contains one deterministically flaky test (alternates pass/fail on each
/// dotnet test invocation) and two stable tests (always pass / always fail).
/// </summary>
public class FlakyTests
{
    // Stable: always passes. Should NOT appear in the flaky list.
    [Fact]
    public void StablePass_AlwaysPasses() => Assert.True(true);

    // Stable: always fails. Should NOT appear in the flaky list either —
    // a test that consistently fails is a broken test, not a flaky one.
    [Fact]
    public void StableFail_AlwaysFails() => Assert.True(false, "This test is intentionally always failing.");

    // Flaky: passes on even-numbered invocations, fails on odd.
    // Uses a counter file in the build output directory so the count
    // survives across dotnet test process boundaries but is scoped to
    // this particular build (avoids conflicts when running in parallel).
    [Fact]
    public void Flaky_AlternatesEachRun()
    {
        // Counter file lives next to the test DLL so it's per-build, not global.
        var counterFile = Path.Combine(
            Path.GetDirectoryName(typeof(FlakyTests).Assembly.Location)!,
            ".flake-run-counter");

        var count = 0;
        if (File.Exists(counterFile) &&
            int.TryParse(File.ReadAllText(counterFile).Trim(), out var stored))
            count = stored;

        count++;
        File.WriteAllText(counterFile, count.ToString());

        // Fails on run 1, 3, 5 … passes on run 2, 4, 6 …
        Assert.True(count % 2 == 0,
            $"Flaky! Odd-numbered invocation ({count}) always fails.");
    }
}
