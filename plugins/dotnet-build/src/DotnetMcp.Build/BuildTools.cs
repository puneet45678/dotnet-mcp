using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotnetMcp.Core.Models;
using DotnetMcp.Core.Utilities;
using ModelContextProtocol.Server;

namespace DotnetMcp.Build;

[McpServerToolType]
public static class BuildTools
{
    private static readonly Regex DiagnosticPattern = new(
        @"^(?<file>[^(\r\n]+)\((?<line>\d+),(?<col>\d+)\):\s+(?<severity>error|warning)\s+(?<code>\w+):\s+(?<message>.+)$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex PackageLinePattern = new(
        @">\s+(?<name>\S+)\s+(?<requested>\S+)\s+(?<resolved>\S+)(?:\s+(?<latest>\S+))?",
        RegexOptions.Compiled);

    [McpServerTool(Name = "build_project", Idempotent = true, Destructive = false)]
    [Description("Build a .NET project or solution. Returns structured errors and warnings.")]
    public static async Task<BuildResult> BuildProject(
        [Description("Path to the .csproj, .sln, or directory to build")] string projectPath,
        [Description("Build configuration: Debug or Release (default: Debug)")] string configuration = "Debug",
        [Description("Restore NuGet packages before building (default: true)")] bool restore = true,
        CancellationToken cancellationToken = default)
    {
        return await RunBuild(projectPath, configuration, restore, cancellationToken);
    }

    [McpServerTool(Name = "get_build_errors", Idempotent = true, Destructive = false)]
    [Description("Build a project and return only the structured list of errors and warnings.")]
    public static async Task<BuildResult> GetBuildErrors(
        [Description("Path to the .csproj, .sln, or directory to build")] string projectPath,
        [Description("Build configuration: Debug or Release (default: Debug)")] string configuration = "Debug",
        CancellationToken cancellationToken = default)
    {
        return await RunBuild(projectPath, configuration, restore: true, cancellationToken);
    }

    [McpServerTool(Name = "clean_project", Idempotent = true, Destructive = false)]
    [Description("Clean build outputs for a project or solution.")]
    public static async Task<BuildResult> CleanProject(
        [Description("Path to the .csproj, .sln, or directory to clean")] string projectPath,
        [Description("Build configuration: Debug or Release (default: Debug)")] string configuration = "Debug",
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet",
            $"clean \"{projectPath}\" --configuration {configuration}",
            cancellationToken: cancellationToken);

        return new BuildResult { Success = result.Success };
    }

    [McpServerTool(Name = "restore_packages", Idempotent = true, Destructive = false)]
    [Description("Restore NuGet packages for a project or solution.")]
    public static async Task<BuildResult> RestorePackages(
        [Description("Path to the .csproj, .sln, or directory")] string projectPath,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet",
            $"restore \"{projectPath}\"",
            cancellationToken: cancellationToken);

        return new BuildResult { Success = result.Success };
    }

    [McpServerTool(Name = "list_projects", ReadOnly = true, Destructive = false)]
    [Description("List all projects in a .sln solution file. For a standalone .csproj, returns that single project.")]
    public static async Task<ProjectListResult> ListProjects(
        [Description("Path to the .sln file, a .csproj file, or a directory")] string path,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(path))
        {
            var sln = Directory.GetFiles(path, "*.sln").FirstOrDefault();
            if (sln is not null) path = sln;
        }

        if (path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            var result = await ProcessRunner.RunAsync("dotnet", $"sln \"{path}\" list", cancellationToken: cancellationToken);
            var projects = result.Output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new ProjectListResult { Solution = path, Projects = projects };
        }

        if (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            return new ProjectListResult { Solution = "", Projects = [path] };

        return new ProjectListResult { Solution = path, Projects = [] };
    }

    [McpServerTool(Name = "get_project_info", ReadOnly = true, Destructive = false)]
    [Description("Read a .csproj file and return its target framework, output type, NuGet packages, and project references.")]
    public static Task<ProjectInfo> GetProjectInfo(
        [Description("Path to the .csproj file")] string csprojPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(csprojPath))
            return Task.FromResult(new ProjectInfo { Name = Path.GetFileName(csprojPath) });

        var doc = XDocument.Load(csprojPath);

        string? Prop(string name) => doc.Descendants(name).FirstOrDefault()?.Value;

        var packages = doc.Descendants("PackageReference")
            .Select(p => new PackageRef(
                p.Attribute("Include")?.Value ?? "",
                p.Attribute("Version")?.Value ?? p.Element("Version")?.Value ?? "*"))
            .ToList();

        var refs = doc.Descendants("ProjectReference")
            .Select(r => r.Attribute("Include")?.Value ?? "")
            .Where(r => r.Length > 0)
            .ToList();

        return Task.FromResult(new ProjectInfo
        {
            Name             = Path.GetFileName(csprojPath),
            Sdk              = doc.Root?.Attribute("Sdk")?.Value,
            TargetFramework  = Prop("TargetFramework"),
            TargetFrameworks = Prop("TargetFrameworks"),
            OutputType       = Prop("OutputType"),
            AssemblyName     = Prop("AssemblyName"),
            Nullable         = Prop("Nullable"),
            LangVersion      = Prop("LangVersion"),
            Packages         = packages,
            ProjectReferences = refs,
        });
    }

    [McpServerTool(Name = "list_packages", ReadOnly = true, Destructive = false)]
    [Description("List all NuGet packages installed in a project or solution with their resolved versions.")]
    public static async Task<PackageListResult> ListPackages(
        [Description("Path to the .csproj, .sln, or directory")] string projectPath,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet", $"list \"{projectPath}\" package", cancellationToken: cancellationToken);

        return ParsePackageList(result.Output, outdated: false);
    }

    [McpServerTool(Name = "check_outdated", ReadOnly = true, Destructive = false)]
    [Description("List NuGet packages that have newer versions available.")]
    public static async Task<OutdatedResult> CheckOutdated(
        [Description("Path to the .csproj, .sln, or directory")] string projectPath,
        CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync(
            "dotnet", $"list \"{projectPath}\" package --outdated", cancellationToken: cancellationToken);

        if (!result.Success || result.Output.Contains("No packages were found"))
            return new OutdatedResult { AllUpToDate = true };

        var packages = new List<OutdatedPackage>();
        foreach (Match m in PackageLinePattern.Matches(result.Output))
        {
            var latest = m.Groups["latest"].Value;
            if (string.IsNullOrEmpty(latest)) continue;
            packages.Add(new OutdatedPackage
            {
                Name    = m.Groups["name"].Value,
                Current = m.Groups["resolved"].Value,
                Latest  = latest,
            });
        }

        return new OutdatedResult { AllUpToDate = packages.Count == 0, Packages = packages };
    }

    // --- shared helpers ---

    private static async Task<BuildResult> RunBuild(
        string projectPath, string configuration, bool restore, CancellationToken ct)
    {
        var args = $"build \"{projectPath}\" --configuration {configuration}";
        if (!restore) args += " --no-restore";

        var result = await ProcessRunner.RunAsync("dotnet", args, cancellationToken: ct);
        var all = ParseDiagnostics(result.Output + "\n" + result.Error);

        return new BuildResult
        {
            Success  = result.Success,
            Errors   = all.Where(d => d.Severity == "error").Select(ToModel).ToList(),
            Warnings = all.Where(d => d.Severity == "warning").Select(ToModel).ToList(),
        };
    }

    private static PackageListResult ParsePackageList(string output, bool outdated)
    {
        var projects = new List<ProjectPackages>();
        ProjectPackages? current = null;

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Project '"))
            {
                var name = trimmed.Split('\'').ElementAtOrDefault(1) ?? trimmed;
                current = new ProjectPackages { Project = name };
                projects.Add(current);
                continue;
            }

            if (current is null) continue;

            var m = PackageLinePattern.Match(trimmed);
            if (!m.Success) continue;

            current.Packages.Add(new InstalledPackage
            {
                Name      = m.Groups["name"].Value,
                Requested = m.Groups["requested"].Value,
                Resolved  = m.Groups["resolved"].Value,
            });
        }

        return new PackageListResult { Projects = projects };
    }

    private static List<(string Severity, string Code, string File, int Line, int Col, string Message)>
        ParseDiagnostics(string output)
    {
        var results = new List<(string, string, string, int, int, string)>();
        foreach (Match m in DiagnosticPattern.Matches(output))
        {
            results.Add((
                m.Groups["severity"].Value,
                m.Groups["code"].Value,
                m.Groups["file"].Value.Trim(),
                int.TryParse(m.Groups["line"].Value, out var l) ? l : 0,
                int.TryParse(m.Groups["col"].Value, out var c) ? c : 0,
                m.Groups["message"].Value.Trim()));
        }
        return results;
    }

    private static BuildDiagnostic ToModel(
        (string Severity, string Code, string File, int Line, int Col, string Message) d) =>
        new() { Code = d.Code, File = d.File, Line = d.Line, Column = d.Col, Message = d.Message };
}
