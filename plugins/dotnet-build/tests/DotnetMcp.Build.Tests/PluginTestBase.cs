namespace DotnetMcp.Build.Tests;

public abstract class PluginTestBase
{
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
}
