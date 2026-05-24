namespace DotnetMcp.Testing.Tests;

public abstract class PluginTestBase
{
    // Walk up from the test binary until we find the repo's samples/ folder.
    // Works regardless of configuration (Debug/Release) or TFM subfolder depth.
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
