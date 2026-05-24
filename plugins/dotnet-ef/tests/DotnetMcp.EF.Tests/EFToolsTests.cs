namespace DotnetMcp.EF.Tests;

public class EFToolsTests : PluginTestBase
{
    // All tests that invoke dotnet ef skip gracefully when the global tool is not installed.
    // Install with: dotnet tool install --global dotnet-ef

    // ==========================================================================
    // Scenario: Developer checks migration state before pushing to CI
    // ==========================================================================

    [Fact]
    public async Task ListMigrations_FreshProject_ReportsNoMigrations()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            var result = await EFTools.ListMigrations(temp);
            Assert.Contains("No migrations", result);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public async Task ListMigrations_AfterAddMigration_ShowsAsPending()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            await EFTools.AddMigration("InitialCreate", temp);
            var result = await EFTools.ListMigrations(temp);

            Assert.Contains("InitialCreate", result);
            Assert.Contains("Pending", result);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public async Task ListMigrations_AfterUpdateDatabase_ShowsAsApplied()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            await EFTools.AddMigration("InitialCreate", temp);
            await EFTools.UpdateDatabase(temp);

            var result = await EFTools.ListMigrations(temp);

            Assert.Contains("InitialCreate", result);
            Assert.Contains("Applied", result);
            Assert.DoesNotContain("Pending", result);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    // ==========================================================================
    // Scenario: Developer scaffolds a migration after changing the model
    // ==========================================================================

    [Fact]
    public async Task AddMigration_NewProject_CreatesFiles()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            var result = await EFTools.AddMigration("InitialCreate", temp);

            Assert.Contains("InitialCreate", result);
            // EF creates a Migrations/ folder with at least the migration class
            Assert.True(Directory.Exists(Path.Combine(temp, "Migrations")),
                "Migrations folder should exist after add");
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public async Task AddMigration_ReportsSuccessMessage()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            var result = await EFTools.AddMigration("CreateBlogsTable", temp);
            Assert.Contains("CreateBlogsTable", result);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    // ==========================================================================
    // Scenario: Developer removes a migration they scaffolded by mistake
    // ==========================================================================

    [Fact]
    public async Task RemoveMigration_AfterAdd_RemovesFiles()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            await EFTools.AddMigration("InitialCreate", temp);
            Assert.True(Directory.Exists(Path.Combine(temp, "Migrations")));

            var result = await EFTools.RemoveMigration(temp);

            Assert.Contains("removed", result.ToLower());
            // Migrations folder should now be gone or empty
            var migrationFiles = Directory.Exists(Path.Combine(temp, "Migrations"))
                ? Directory.GetFiles(Path.Combine(temp, "Migrations"), "*.cs")
                : [];
            Assert.Empty(migrationFiles);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    // ==========================================================================
    // Scenario: Developer applies migrations to a local dev database
    // ==========================================================================

    [Fact]
    public async Task UpdateDatabase_AppliesPendingMigration_ReportsSuccess()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            await EFTools.AddMigration("InitialCreate", temp);
            var result = await EFTools.UpdateDatabase(temp);

            Assert.Contains("updated", result.ToLower());
            // The SQLite file should now exist
            Assert.True(File.Exists(Path.Combine(temp, "sample.db")),
                "SQLite database file should be created after update");
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public async Task UpdateDatabase_ToTarget_StopsAtSpecifiedMigration()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            await EFTools.AddMigration("InitialCreate", temp);

            // Apply only up to InitialCreate (which is all we have, but tests the flag works)
            var result = await EFTools.UpdateDatabase(temp, targetMigration: "InitialCreate");

            Assert.Contains("updated", result.ToLower());
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    // ==========================================================================
    // Scenario: Tech lead reviews migration SQL before handing off to DBA
    // ==========================================================================

    [Fact]
    public async Task ScriptMigration_GeneratesSqlWithCreateTable()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            await EFTools.AddMigration("InitialCreate", temp);
            var result = await EFTools.ScriptMigration(temp);

            Assert.Contains("CREATE TABLE", result.ToUpper());
            Assert.Contains("Blogs", result);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public async Task ScriptMigration_AlwaysIncludesMigrationsHistoryTable()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            await EFTools.AddMigration("InitialCreate", temp);
            var result = await EFTools.ScriptMigration(temp);

            // EF always inserts a row into __EFMigrationsHistory after applying
            Assert.Contains("__EFMigrationsHistory", result);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    // ==========================================================================
    // Scenario: Developer inspects project's DbContext before adding a new entity
    // ==========================================================================

    [Fact]
    public async Task GetDbContextInfo_ShowsAppDbContext()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            var result = await EFTools.GetDbContextInfo(temp);

            Assert.Contains("AppDbContext", result);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    [Fact]
    public async Task GetDbContextInfo_ShowsSqliteProvider()
    {
        if (!await EFAvailableAsync()) return;

        var temp = await CreateTempProjectAsync("SampleEFProject");
        try
        {
            var result = await EFTools.GetDbContextInfo(temp);

            Assert.Contains("Sqlite", result, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(temp, recursive: true); }
    }

    // ==========================================================================
    // Edge cases
    // ==========================================================================

    [Fact]
    public async Task ListMigrations_InvalidPath_ReturnsErrorMessage()
    {
        var result = await EFTools.ListMigrations("/nonexistent/path/Project.csproj");

        // Should return an error, not throw
        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(
            result.Contains("failed") || result.Contains("error") || result.Contains("not installed"),
            $"Expected error description, got: {result}");
    }

    [Fact]
    public async Task AddMigration_InvalidPath_ReturnsErrorMessage()
    {
        var result = await EFTools.AddMigration("SomeMigration", "/nonexistent/path");

        Assert.False(string.IsNullOrWhiteSpace(result));
        Assert.True(
            result.Contains("failed") || result.Contains("error") || result.Contains("not installed"),
            $"Expected error description, got: {result}");
    }
}
