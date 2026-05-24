using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DotnetMcp.Core.Models;
using DotnetMcp.Core.Utilities;
using ModelContextProtocol.Server;

namespace DotnetMcp.EF;

[McpServerToolType]
public static class EFTools
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    [McpServerTool(Name = "ef_list_migrations", ReadOnly = true, Idempotent = true, Destructive = false)]
    [Description("List all EF Core migrations for a project with their applied/pending status.")]
    public static async Task<string> ListMigrations(
        [Description("Path to the .csproj or directory containing the DbContext")] string projectPath,
        [Description("DbContext class name when the project has multiple contexts")] string? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunEF(EFArgs("migrations list --json", projectPath, context), cancellationToken);
        if (!result.Success) return FormatError(result);

        try
        {
            var json = ExtractJson(result.Output, '[');
            if (json is null) return result.Output.Trim();

            var migrations = JsonSerializer.Deserialize<List<MigrationEntry>>(json, JsonOpts);
            if (migrations is null || migrations.Count == 0)
                return "No migrations found.";

            var sb = new StringBuilder();
            sb.AppendLine($"Migrations ({migrations.Count}):");
            foreach (var m in migrations)
                sb.AppendLine($"  [{(m.Applied ? "Applied " : "Pending ")}] {m.Name}");
            return sb.ToString().TrimEnd();
        }
        catch
        {
            return result.Output.Trim();
        }
    }

    [McpServerTool(Name = "ef_add_migration", Idempotent = false, Destructive = false)]
    [Description("Scaffold a new EF Core migration for pending model changes.")]
    public static async Task<string> AddMigration(
        [Description("Name for the new migration (e.g. AddUserTable)")] string name,
        [Description("Path to the .csproj or directory")] string projectPath,
        [Description("DbContext class name when the project has multiple contexts")] string? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunEF(EFArgs($"migrations add \"{name}\"", projectPath, context), cancellationToken);
        return result.Success
            ? $"Migration '{name}' created successfully.\n\n{result.Output}"
            : FormatError(result);
    }

    [McpServerTool(Name = "ef_remove_migration", Idempotent = false, Destructive = true)]
    [Description("Remove the last EF Core migration. Only works if that migration has not been applied to the database.")]
    public static async Task<string> RemoveMigration(
        [Description("Path to the .csproj or directory")] string projectPath,
        [Description("DbContext class name when the project has multiple contexts")] string? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = await RunEF(EFArgs("migrations remove", projectPath, context), cancellationToken);
        return result.Success
            ? $"Last migration removed.\n\n{result.Output}"
            : FormatError(result);
    }

    [McpServerTool(Name = "ef_update_database", Idempotent = false, Destructive = true)]
    [Description("Apply pending EF Core migrations to the database. Specify a target migration name to migrate up or down to a specific point.")]
    public static async Task<string> UpdateDatabase(
        [Description("Path to the .csproj or directory")] string projectPath,
        [Description("Target migration name to stop at (applies all pending if omitted, use '0' to roll back all)")] string? targetMigration = null,
        [Description("DbContext class name when the project has multiple contexts")] string? context = null,
        CancellationToken cancellationToken = default)
    {
        var target = targetMigration is not null ? $" \"{targetMigration}\"" : "";
        var result = await RunEF(EFArgs($"database update{target}", projectPath, context), cancellationToken);
        return result.Success
            ? $"Database updated successfully.\n\n{result.Output}"
            : FormatError(result);
    }

    [McpServerTool(Name = "ef_script_migration", ReadOnly = true, Idempotent = true, Destructive = false)]
    [Description("Generate a SQL script for EF Core migrations. Useful for reviewing changes or handing off to a DBA. Uses --idempotent by default.")]
    public static async Task<string> ScriptMigration(
        [Description("Path to the .csproj or directory")] string projectPath,
        [Description("Starting migration (default: script from the beginning)")] string? from = null,
        [Description("Ending migration (default: latest)")] string? to = null,
        [Description("Generate idempotent script with IF NOT EXISTS guards (not supported by SQLite)")] bool idempotent = false,
        [Description("DbContext class name when the project has multiple contexts")] string? context = null,
        CancellationToken cancellationToken = default)
    {
        var range = from is not null ? $" \"{from}\"" : "";
        if (to is not null) range += $" \"{to}\"";
        var flags = idempotent ? " --idempotent" : "";
        var result = await RunEF(EFArgs($"migrations script{range}{flags}", projectPath, context), cancellationToken);
        return result.Success ? result.Output : FormatError(result);
    }

    [McpServerTool(Name = "ef_get_dbcontext_info", ReadOnly = true, Idempotent = true, Destructive = false)]
    [Description("List all DbContext classes in a project and show the EF provider and connection info for each.")]
    public static async Task<string> GetDbContextInfo(
        [Description("Path to the .csproj or directory")] string projectPath,
        CancellationToken cancellationToken = default)
    {
        var listResult = await RunEF(EFArgs("dbcontext list --json", projectPath, null), cancellationToken);
        if (!listResult.Success) return FormatError(listResult);

        var names = ParseContextNames(listResult.Output);
        if (names.Count == 0)
            return "No DbContext classes found in project.";

        var sb = new StringBuilder();
        sb.AppendLine($"DbContexts found ({names.Count}):");

        foreach (var name in names)
        {
            sb.AppendLine($"\n  {name}");
            var infoResult = await RunEF(
                EFArgs($"dbcontext info --json --context \"{name}\"", projectPath, null),
                cancellationToken);

            if (infoResult.Success)
            {
                try
                {
                    var json = ExtractJson(infoResult.Output, '{');
                    if (json is not null)
                    {
                        var info = JsonSerializer.Deserialize<DbContextInfo>(json, JsonOpts);
                        if (info is not null)
                        {
                            sb.AppendLine($"    Provider:  {info.ProviderName ?? "unknown"}");
                            sb.AppendLine($"    Database:  {info.DatabaseName ?? "unknown"}");
                            sb.AppendLine($"    Source:    {info.DataSource ?? "unknown"}");
                        }
                    }
                }
                catch
                {
                    sb.AppendLine($"    {infoResult.Output.Trim()}");
                }
            }
            else
            {
                sb.AppendLine($"    (Could not load info: {infoResult.Error.Split('\n')[0]})");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // --- helpers ---

    private static string EFArgs(string subcommand, string projectPath, string? context)
    {
        var args = $"ef {subcommand} --project \"{projectPath}\"";
        if (context is not null) args += $" --context \"{context}\"";
        return args;
    }

    private static Task<CommandResult> RunEF(string args, CancellationToken ct)
        => ProcessRunner.RunAsync("dotnet", args, cancellationToken: ct);

    private static string FormatError(CommandResult result)
    {
        var combined = result.Error + " " + result.Output;
        if (combined.Contains("No executable found matching command") ||
            combined.Contains("dotnet-ef") && combined.Contains("not found"))
            return "dotnet-ef tool is not installed. Run: dotnet tool install --global dotnet-ef";

        return $"EF command failed (exit {result.ExitCode}).\n{result.Error}\n{result.Output}".TrimEnd();
    }

    private static string? ExtractJson(string output, char startChar)
    {
        var idx = output.IndexOf(startChar);
        return idx >= 0 ? output[idx..] : null;
    }

    private static List<string> ParseContextNames(string output)
    {
        try
        {
            var json = ExtractJson(output, '[');
            if (json is null) return [];
            var items = JsonSerializer.Deserialize<List<DbContextEntry>>(json, JsonOpts);
            return items?
                .Select(x => x.SafeName ?? x.FullName ?? "")
                .Where(n => n.Length > 0)
                .ToList() ?? [];
        }
        catch { return []; }
    }

    private sealed record MigrationEntry(string Id, string Name, bool Applied);
    private sealed record DbContextEntry(string? FullName, string? SafeName);
    private sealed record DbContextInfo(string? ProviderName, string? DatabaseName, string? DataSource);
}
