using DotnetMcp.Host;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var pluginsRoot = ResolvePluginsRoot();

if (pluginsRoot is null)
{
    Console.Error.WriteLine("[dotnet-mcp] Could not locate a 'plugins' directory. " +
        "Set DOTNET_MCP_PLUGINS_DIR or run from the repo root.");
    return;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();

var mcpBuilder = builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

foreach (var assembly in PluginLoader.LoadFromDirectory(pluginsRoot))
    mcpBuilder.WithToolsFromAssembly(assembly);

await builder.Build().RunAsync();

static string? ResolvePluginsRoot()
{
    // 1. Explicit override via environment variable
    var env = Environment.GetEnvironmentVariable("DOTNET_MCP_PLUGINS_DIR");
    if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
        return Path.GetFullPath(env);

    // 2. plugins/ relative to current working directory (works for `dotnet run` from repo root)
    var cwd = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
    if (Directory.Exists(cwd))
        return cwd;

    // 3. Walk up from the binary location (works for published / installed scenarios)
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "plugins");
        if (Directory.Exists(candidate))
            return candidate;
        dir = dir.Parent;
    }

    return null;
}
