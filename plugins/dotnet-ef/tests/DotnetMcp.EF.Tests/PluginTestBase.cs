using DotnetMcp.Core.Utilities;

namespace DotnetMcp.EF.Tests;

public abstract class PluginTestBase
{
    private static bool? _efAvailable;

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

    protected static async Task<bool> EFAvailableAsync()
    {
        if (_efAvailable.HasValue) return _efAvailable.Value;
        var result = await ProcessRunner.RunAsync("dotnet", "ef --version");
        _efAvailable = result.Success;
        return _efAvailable.Value;
    }

    // Copies a sample project to a temp directory and restores it.
    // The caller is responsible for deleting the directory.
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
