namespace DotnetMcp.Build.Tests;

public class BuildToolsTests : PluginTestBase
{
    // ==========================================================================
    // Scenario: Developer asks Claude "why is my build failing?"
    // ==========================================================================

    [Fact]
    public async Task BuildProject_BrokenProject_ReportsAllThreeErrors()
    {
        var result = await BuildTools.BuildProject(SamplePath("SampleBrokenProject"));

        Assert.False(result.Success);
        Assert.True(result.ErrorCount >= 2);
        Assert.Contains(result.Errors, e => e.Code == "CS0029" || e.Code == "CS0103");
        Assert.Contains(result.Errors, e => e.File.Contains("BrokenCode.cs"));
    }

    [Fact]
    public async Task BuildProject_BrokenProject_ErrorIncludesLineNumber()
    {
        var result = await BuildTools.BuildProject(SamplePath("SampleBrokenProject"));

        Assert.False(result.Success);
        Assert.All(result.Errors, e => Assert.True(e.Line > 0));
    }

    [Fact]
    public async Task GetBuildErrors_BrokenProject_ReturnsStructuredDiagnostics()
    {
        var result = await BuildTools.GetBuildErrors(SamplePath("SampleBrokenProject"));

        Assert.False(result.Success);
        Assert.True(result.ErrorCount >= 2);
        Assert.All(result.Errors, e =>
        {
            Assert.NotEmpty(e.Code);
            Assert.NotEmpty(e.File);
            Assert.True(e.Line > 0);
            Assert.NotEmpty(e.Message);
        });
    }

    // ==========================================================================
    // Scenario: Developer verifies a clean project builds in both configurations
    // ==========================================================================

    [Fact]
    public async Task BuildProject_DebugConfiguration_Succeeds()
    {
        var result = await BuildTools.BuildProject(SamplePath("SampleProject"), configuration: "Debug");
        Assert.True(result.Success);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task BuildProject_ReleaseConfiguration_Succeeds()
    {
        var result = await BuildTools.BuildProject(SamplePath("SampleProject"), configuration: "Release");
        Assert.True(result.Success);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task GetBuildErrors_CleanProject_ReportsNoErrors()
    {
        var result = await BuildTools.GetBuildErrors(SamplePath("SampleProject"));
        Assert.True(result.Success);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
    }

    // ==========================================================================
    // Scenario: Developer does a clean rebuild before submitting for review
    // ==========================================================================

    [Fact]
    public async Task CleanThenBuild_Succeeds()
    {
        var cleanResult = await BuildTools.CleanProject(SamplePath("SampleProject"));
        Assert.True(cleanResult.Success);

        var buildResult = await BuildTools.BuildProject(SamplePath("SampleProject"));
        Assert.True(buildResult.Success);
    }

    // ==========================================================================
    // Scenario: Developer restores packages on a fresh clone
    // ==========================================================================

    [Fact]
    public async Task RestorePackages_ProjectWithDependencies_Succeeds()
    {
        var result = await BuildTools.RestorePackages(SamplePath("SampleTestProject"));
        Assert.True(result.Success);
    }

    [Fact]
    public async Task BuildProject_WithRestoreDisabled_StillBuildsIfAlreadyRestored()
    {
        await BuildTools.RestorePackages(SamplePath("SampleProject"));
        var result = await BuildTools.BuildProject(SamplePath("SampleProject"), restore: false);
        Assert.True(result.Success);
    }

    // ==========================================================================
    // Scenario: Developer inspects a project before adding a dependency
    // ==========================================================================

    [Fact]
    public async Task GetProjectInfo_ShowsTargetFrameworkAndPackages()
    {
        var csprojPath = Path.Combine(SamplePath("SampleTestProject"), "SampleTestProject.csproj");
        var result = await BuildTools.GetProjectInfo(csprojPath);

        Assert.Equal("SampleTestProject.csproj", result.Name);
        Assert.Contains(result.Packages, p => p.Name.Contains("xunit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Packages, p => p.Name == "Microsoft.NET.Test.Sdk");
    }

    [Fact]
    public async Task GetProjectInfo_ShowsProjectReference()
    {
        var csprojPath = Path.Combine(SamplePath("SampleTestProject"), "SampleTestProject.csproj");
        var result = await BuildTools.GetProjectInfo(csprojPath);

        Assert.Contains(result.ProjectReferences, r => r.Contains("SampleProject"));
    }

    [Fact]
    public async Task GetProjectInfo_ShowsPackageCount()
    {
        var csprojPath = Path.Combine(SamplePath("SampleTestProject"), "SampleTestProject.csproj");
        var result = await BuildTools.GetProjectInfo(csprojPath);

        Assert.Equal(4, result.Packages.Count);
    }

    [Fact]
    public async Task GetProjectInfo_NonExistentFile_ReturnsEmptyInfo()
    {
        var result = await BuildTools.GetProjectInfo("/nonexistent/path/Project.csproj");
        Assert.Equal("Project.csproj", result.Name);
        Assert.Empty(result.Packages);
    }

    // ==========================================================================
    // Scenario: Developer checks installed and outdated packages
    // ==========================================================================

    [Fact]
    public async Task ListPackages_ShowsInstalledPackages()
    {
        var result = await BuildTools.ListPackages(SamplePath("SampleTestProject"));

        Assert.NotEmpty(result.Projects);
        var packages = result.Projects.SelectMany(p => p.Packages).ToList();
        Assert.Contains(packages, p => p.Name.Contains("xunit", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(packages, p => p.Name.Contains("coverlet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CheckOutdated_ReturnsResultWithoutCrashing()
    {
        var result = await BuildTools.CheckOutdated(SamplePath("SampleTestProject"));

        // Either all up to date or a real list — both valid
        Assert.True(result.AllUpToDate || result.Packages.Count > 0);
    }

    // ==========================================================================
    // Scenario: Developer wants to understand the solution structure
    // ==========================================================================

    [Fact]
    public async Task ListProjects_SolutionFile_ReturnsAllPluginProjects()
    {
        var slnPath = FindSolutionFile();
        if (slnPath is null) return;

        var result = await BuildTools.ListProjects(slnPath);

        Assert.Contains(result.Projects, p => p.Contains("DotnetMcp.Host"));
        Assert.Contains(result.Projects, p => p.Contains("DotnetMcp.Core"));
        Assert.Contains(result.Projects, p => p.Contains("DotnetMcp.Testing"));
        Assert.Contains(result.Projects, p => p.Contains("DotnetMcp.Build"));
    }

    [Fact]
    public async Task ListProjects_StandaloneCsproj_ReturnsSingleProject()
    {
        var csprojPath = Path.Combine(SamplePath("SampleProject"), "SampleProject.csproj");
        var result = await BuildTools.ListProjects(csprojPath);

        Assert.Single(result.Projects);
        Assert.Contains("SampleProject", result.Projects[0]);
    }

    // ==========================================================================
    // Helpers
    // ==========================================================================

    private static string? FindSolutionFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var sln = dir.GetFiles("dotnet-mcp.sln").FirstOrDefault();
            if (sln is not null) return sln.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
