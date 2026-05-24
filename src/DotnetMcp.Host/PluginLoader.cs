using System.Reflection;
using System.Text.Json;
using DotnetMcp.Core.Models;

namespace DotnetMcp.Host;

internal static class PluginLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IEnumerable<Assembly> LoadFromDirectory(string pluginsRoot)
    {
        if (!Directory.Exists(pluginsRoot))
            yield break;

        foreach (var manifestPath in Directory.GetFiles(pluginsRoot, "plugin.json", SearchOption.AllDirectories))
        {
            PluginManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), JsonOptions);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[dotnet-mcp] Failed to read {manifestPath}: {ex.Message}");
                continue;
            }

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Assembly))
                continue;

            // Resolve assembly path relative to the manifest file
            var assemblyPath = Path.GetFullPath(
                Path.Combine(Path.GetDirectoryName(manifestPath)!, manifest.Assembly));

            if (!File.Exists(assemblyPath))
            {
                Console.Error.WriteLine($"[dotnet-mcp] Assembly not found: {assemblyPath} (from {manifestPath})");
                continue;
            }

            Assembly asm;
            try
            {
                asm = Assembly.LoadFrom(assemblyPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[dotnet-mcp] Failed to load {assemblyPath}: {ex.Message}");
                continue;
            }

            Console.Error.WriteLine($"[dotnet-mcp] Loaded plugin: {manifest.Name} v{manifest.Version}");
            yield return asm;
        }
    }
}
