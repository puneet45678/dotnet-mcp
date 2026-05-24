# Creating a Plugin

A dotnet-mcp plugin is a class library that uses the `[McpServerToolType]` and `[McpServerTool]` attributes from the ModelContextProtocol SDK. The host discovers and loads plugins at startup via their `plugin.json` manifest.

See [CONTRIBUTING.md](../CONTRIBUTING.md) for the full step-by-step guide.

## How discovery works

1. At startup, `DotnetMcp.Host` walks the `plugins/` directory looking for `plugin.json` files.
2. Each manifest's `assembly` path is resolved and loaded with `Assembly.LoadFrom`.
3. The host calls `.WithToolsFromAssembly(asm)` on the MCP builder, which scans for `[McpServerToolType]` classes.
4. All `[McpServerTool]` methods on those classes become available as MCP tools.

No registration code is needed in the host. Drop a compiled plugin DLL + `plugin.json` into the right folder and it's live.

## TargetFrameworks

All projects inherit `<TargetFrameworks>` from [`Directory.Build.props`](../Directory.Build.props) at the repo root. This ensures every plugin supports the same set of .NET versions. Do not set `<TargetFramework>` or `<TargetFrameworks>` in individual `.csproj` files.

## Tool design guidelines

- One `[McpServerToolType]` class per logical grouping (not per file).
- Every `[McpServerTool]` method must have a `[Description]` attribute — the AI uses this to decide when to call the tool.
- Every parameter must have a `[Description]` attribute.
- Set `ReadOnly`, `Destructive`, and `Idempotent` accurately so the AI can reason about side effects.
- Use `ProcessRunner.RunAsync` from `DotnetMcp.Core` for CLI invocations — it handles stdout/stderr capture and exit codes cleanly.
- Return plain strings. Structure the output so it's readable as plain text (the AI will parse it).
