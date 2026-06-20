using DotnetMcp.Core.Utilities;

namespace DotnetMcp.EF.Tests;

/// <summary>
/// Class fixture that pre-warms the NuGet package cache once per test run.
/// Without this, each EF test that copies SampleEFProject to a temp dir would
/// trigger a full network restore for the EF Core + SQLite packages on first run.
/// After this fixture restores against the original sample, all subsequent restores
/// in individual tests hit the local cache and complete in &lt;500ms.
/// </summary>
public sealed class EFProjectFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var samplePath = FindSamplePath("SampleEFProject");
        if (samplePath is not null)
            await ProcessRunner.RunAsync("dotnet", $"restore \"{samplePath}\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string? FindSamplePath(string projectName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", projectName);
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
