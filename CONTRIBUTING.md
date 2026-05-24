# Contributing to dotnet-mcp

Thank you for your interest in contributing! This guide covers everything you need to add a new plugin or improve an existing one.

---

## Before You Start

- **Open an issue first** before building a new plugin. This avoids duplicate work and lets maintainers give early feedback on scope.
- Search existing issues and plugins to make sure yours doesn't already exist.
- Keep plugins **narrow in scope** — one domain, one concern.

---

## Plugin Structure

Every plugin lives under `plugins/<plugin-name>/` and follows this layout:

```
plugins/dotnet-example/
├── plugin.json                        # Required: plugin manifest
├── src/
│   └── DotnetMcp.Example/
│       ├── ExampleTools.cs            # Tool implementation
│       └── DotnetMcp.Example.csproj  # References DotnetMcp.Core
└── tests/
    └── eval.yaml                      # Required: scenario-based tests
```

---

## Step-by-Step: Creating a New Plugin

### 1. Scaffold the project

```bash
dotnet new classlib -n DotnetMcp.Example -o plugins/dotnet-example/src/DotnetMcp.Example
dotnet sln add plugins/dotnet-example/src/DotnetMcp.Example/DotnetMcp.Example.csproj
```

### 2. Add the Core reference to your `.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>DotnetMcp.Example</PackageId>
    <Description>dotnet-mcp plugin: short description here</Description>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../../../src/DotnetMcp.Core/DotnetMcp.Core.csproj" />
  </ItemGroup>
</Project>
```

> `TargetFrameworks` is inherited from `Directory.Build.props` — do not set it in your `.csproj`.

### 3. Implement your tools

```csharp
using System.ComponentModel;
using DotnetMcp.Core.Utilities;
using ModelContextProtocol.Server;

namespace DotnetMcp.Example;

[McpServerToolType]
public static class ExampleTools
{
    [McpServerTool(Name = "my_tool", ReadOnly = true, Destructive = false)]
    [Description("One clear sentence describing what this tool does and when to use it.")]
    public static async Task<string> MyTool(
        [Description("Description of this parameter")] string input,
        CancellationToken cancellationToken = default)
    {
        // Use ProcessRunner for CLI calls
        var result = await ProcessRunner.RunAsync("dotnet", $"...", cancellationToken: cancellationToken);
        return result.Success ? result.Output : $"Failed: {result.Error}";
    }
}
```

**Tool attribute guide:**

| Scenario | Attributes |
|----------|-----------|
| Read-only inspection | `ReadOnly = true, Destructive = false` |
| Build / test run (idempotent) | `Idempotent = true, Destructive = false` |
| Mutating operation (add package, etc.) | `Destructive = true` |

### 4. Add `plugin.json`

```json
{
  "name": "dotnet-example",
  "version": "0.1.0",
  "description": "What this plugin does in one sentence.",
  "assembly": "./bin/DotnetMcp.Example.dll",
  "tools": [
    { "name": "my_tool", "description": "Same description as the [Description] attribute." }
  ]
}
```

### 5. Add `tests/eval.yaml`

```yaml
scenarios:
  - name: basic_usage
    tool: my_tool
    input:
      input: "some value"
    expected:
      not_empty: true
```

### 6. Add a CODEOWNERS entry

Add a line to [CODEOWNERS](CODEOWNERS):

```
/plugins/dotnet-example/   @your-github-username
```

### 7. Open a PR

Make sure:
- [ ] `dotnet build` passes
- [ ] `plugin.json` has all required fields (`name`, `version`, `description`, `assembly`, `tools`)
- [ ] At least one scenario in `eval.yaml`
- [ ] CODEOWNERS entry added
- [ ] `[Description]` attributes on every tool and parameter

---

## Code Style

- Nullable reference types enabled — no `!` suppressions without justification
- No comments unless the WHY is non-obvious
- `TreatWarningsAsErrors` is on — fix all warnings

---

## Questions?

Open a [Discussion](https://github.com/puneet45678/dotnet-mcp/discussions) or comment on your issue.
