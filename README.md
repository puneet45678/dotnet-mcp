# dotnet-mcp

[![CI](https://github.com/puneet45678/dotnet-mcp/actions/workflows/ci.yml/badge.svg)](https://github.com/puneet45678/dotnet-mcp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A community-extensible **MCP (Model Context Protocol) server** for .NET developers, built on the official [ModelContextProtocol C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

Give Claude (or any MCP-compatible AI) the ability to build your .NET projects, run your tests, inspect solutions, and more — all from within the AI session. No copy-pasting terminal output. The AI closes the feedback loop itself.

---

## What It Does

Instead of this:
```
You:    dotnet build
Output: error CS0103: 'foo' does not exist (Calculator.cs, line 42)
You:    (copy-paste into Claude)
Claude: "Try adding a using directive"
You:    (make change, rebuild, copy-paste again...)
```

You get this:
```
You:    "fix the build errors in my project"
Claude: calls build_project → gets structured errors → reads the file → fixes it → rebuilds to verify
```

---

## Available Plugins

### `dotnet-build`

| Tool | Description |
|------|-------------|
| `build_project` | Build a project or solution. Returns structured errors and warnings with file, line, and column. |
| `get_build_errors` | Build and return only the diagnostic list — no build log noise. |
| `explain_build_error` | Plain-English explanation of any CS or MSB error code with common causes and a suggested fix. |
| `list_projects` | List all projects in a `.sln` file. |
| `get_project_info` | Read a `.csproj` — target framework, packages, project references. |
| `restore_packages` | Run `dotnet restore`. |
| `clean_project` | Run `dotnet clean`. |
| `list_packages` | List installed NuGet packages with resolved versions. |
| `check_outdated` | List packages that have newer versions available. |

### `dotnet-test`

| Tool | Description |
|------|-------------|
| `run_tests` | Run tests and return structured pass/fail/skip counts with failure messages and stack traces. |
| `run_failed_tests` | Re-run only the tests that failed last time. |
| `list_tests` | List all test names without running them. |
| `get_coverage_summary` | Run tests with Coverlet and return line/branch coverage per assembly. |
| `get_test_summary` | Parse an existing `.trx` file into structured results. |

### `dotnet-ef` *(Entity Framework)*

| Tool | Description |
|------|-------------|
| `ef_migrations_list` | List all EF migrations and their applied status. |
| `ef_add_migration` | Scaffold a new migration. |
| `ef_update_database` | Apply pending migrations. |
| `ef_migrations_script` | Generate a SQL script for pending migrations. |
| `ef_dbcontext_info` | Show the DbContext connection string and provider. |

---

## Quick Start

### 1. Clone and build

```bash
git clone https://github.com/puneet45678/dotnet-mcp
cd dotnet-mcp
dotnet build --configuration Release
```

### 2. Register with Claude Code (global — works in every project)

```bash
claude mcp add dotnet-mcp -s user \
  -e DOTNET_MCP_PLUGINS_DIR=/path/to/dotnet-mcp/plugins \
  -- dotnet /path/to/dotnet-mcp/src/DotnetMcp.Host/bin/Release/net8.0/DotnetMcp.Host.dll
```

Replace `/path/to/dotnet-mcp` with the directory where you cloned the repo.

### 3. Or wire it up to Claude Desktop

Add to `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS):

```json
{
  "mcpServers": {
    "dotnet-mcp": {
      "command": "dotnet",
      "args": ["/path/to/dotnet-mcp/src/DotnetMcp.Host/bin/Release/net8.0/DotnetMcp.Host.dll"],
      "env": {
        "DOTNET_MCP_PLUGINS_DIR": "/path/to/dotnet-mcp/plugins"
      }
    }
  }
}
```

### 4. Or add a workspace `.mcp.json` (checked into your repo)

```json
{
  "mcpServers": {
    "dotnet-mcp": {
      "command": "dotnet",
      "args": ["/path/to/dotnet-mcp/src/DotnetMcp.Host/bin/Release/net8.0/DotnetMcp.Host.dll"],
      "env": {
        "DOTNET_MCP_PLUGINS_DIR": "/path/to/dotnet-mcp/plugins"
      }
    }
  }
}
```

> **Why the DLL directly?** `dotnet run` adds 2-3 seconds of build-check overhead on every startup. Running the DLL directly is instant and avoids MCP client timeouts.

---

## Requirements

- .NET 8.0 SDK or later
- Claude Desktop, Claude Code, VS Code with Claude extension, or any MCP-compatible host

---

## How It Works

```
Claude / VS Code / Claude Desktop
        │  stdio (JSON-RPC)
        ▼
DotnetMcp.Host          ← spawned as a child process by the MCP host
        │  Assembly.LoadFrom()
        ├── dotnet-build plugin DLL
        ├── dotnet-test plugin DLL
        └── dotnet-ef plugin DLL
```

The host scans `plugins/*/plugin.json` at startup, loads each DLL, and registers all `[McpServerTool]`-decorated methods as MCP tools. Adding a new plugin is dropping a DLL + manifest — no changes to the host.

---

## Contributing

Want to add a plugin? See [CONTRIBUTING.md](CONTRIBUTING.md).

**Ideas for new plugins:**
- `dotnet-nuget` — search packages, audit vulnerabilities
- `dotnet-aspnet` — list API routes, controller actions
- `dotnet-format` — run `dotnet format` and report style violations
- `dotnet-watch` — stream test results as files change

---

## License

MIT — see [LICENSE](LICENSE).
