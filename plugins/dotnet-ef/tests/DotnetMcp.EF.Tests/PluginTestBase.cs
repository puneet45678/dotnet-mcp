using DotnetMcp.Core.Utilities;

namespace DotnetMcp.EF.Tests;

public abstract class PluginTestBase
{
    // Thread-safe: Lazy<Task<T>> runs the factory exactly once even under concurrent access.
    private static readonly Lazy<Task<bool>> _efAvailable = new(
        () => ProcessRunner.RunAsync("dotnet", "ef --version")
                           .ContinueWith(t => t.Result.Success));

    protected static string SamplePath(string projectName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "samples", projectName);
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            $"Could not locate samples/{projectName}. Run tests from the repo root.");
    }

    // Returns true if the dotnet-ef global tool is installed.
    // Safe to call concurrently — the version check runs exactly once.
    protected static Task<bool> EFAvailableAsync() => _efAvailable.Value;

    // Copies a sample project to a temp directory.
    // NuGet packages are already warm after the class fixture pre-restores them,
    // so the restore here completes from the local cache in <1 s.
    // The caller MUST delete the returned directory in a finally block.
    protected static async Task<string> CreateTempProjectAsync(string sampleName)
    {
        var src = SamplePath(sampleName);
        var dest = Path.Combine(Path.GetTempPath(), $"dotnet-mcp-ef-{Guid.NewGuid():N}");
        CopyDirectory(src, dest);
        await ProcessRunner.RunAsync("dotnet", $"restore \"{dest}\"");
        return dest;
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
