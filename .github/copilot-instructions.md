# Mud MCP - AI Coding Agent Instructions

MCP (Model Context Protocol) server and token-efficient CLI that give AI assistants **version-accurate** MudBlazor component documentation. It clones the MudBlazor repo at a specific tag, parses source with Roslyn, builds an in-memory index of ~85 components, and exposes it via 12 MCP tools or compact terminal queries.

**Tech Stack:** .NET 10, ASP.NET Core, Roslyn (`Microsoft.CodeAnalysis.CSharp`), LibGit2Sharp, Aspire 13.5.3, xunit.v3 + Moq on Microsoft Testing Platform.

## Documentation Map (read these before duplicating knowledge)

Deep docs live in [docs/](../docs/) — link to them, don't re-explain them here:

| Topic | Doc |
|-------|-----|
| Architecture, Roslyn pipeline, caching | [docs/03-architecture.md](../docs/03-architecture.md) |
| All 12 MCP tools (params & output) | [docs/05-tools-reference.md](../docs/05-tools-reference.md) |
| CLI mode (commands, formats, store, skill) | [docs/11-cli.md](../docs/11-cli.md) |
| Configuration schema & options | [docs/06-configuration.md](../docs/06-configuration.md) |
| Testing (framework, patterns, commands) | [docs/07-testing.md](../docs/07-testing.md) |
| Getting started / IDE integration / troubleshooting | [docs/02](../docs/02-getting-started.md), [09](../docs/09-ide-integration.md), [10](../docs/10-troubleshooting.md) |

## Architecture

```
Program.cs -> CliApp.IsCliInvocation(args)
  +-- CLI -> CliApp -> CliCommands -> IndexStore -> compact / markdown / json
  |          index -> McpServerHost.RegisterCoreServices -> ComponentIndexer
  +-- MCP -> McpServerHost (--stdio / default HTTP / --http)
              -> MCP Tools (12) -> ComponentIndexer <- Parsing (Roslyn)
                                      ^
              VersionContext -> GitRepositoryService (tag v9.0.0)
              VersionCacheManager (LRU, MaxCachedVersions=3)
                -> per-version index.json + store/
```

Every request is scoped to one MudBlazor version. Indexes persist to disk so subsequent runs load instantly.

**Key services** ([src/MudBlazor.Mcp/Services/](../src/MudBlazor.Mcp/Services/)):
- `ComponentIndexer` — builds/queries the in-memory component index
- `VersionCacheManager` — LRU cache of up to 3 version indexes; evicts least-recently-used
- `VersionContext` ([Configuration/VersionContext.cs](../src/MudBlazor.Mcp/Configuration/VersionContext.cs)) — resolves per-version paths (`data/v{version}/...`)
- `GitRepositoryService` — clones the MudBlazor repo and checks out the `v{version}` tag
- `Parsing/` — `SemanticComponentParser` (primary: one Roslyn `CSharpCompilation`, inheritance merge + `<inheritdoc/>` + enums), `XmlDocParser` (degraded fallback), `RazorDocParser`, `ExampleExtractor`, `MenuCategoryParser` (menu-derived categories) (see [docs/03](../docs/03-architecture.md))

## Build & Test Commands

```bash
# Build from repo root — TreatWarningsAsErrors=true, so ANY warning fails the build
dotnet build

# Tests run on Microsoft Testing Platform (MTP), enabled via global.json — NOT VSTest.
# Use the MTP CLI (--filter-class / --filter-method), not `dotnet test --filter`.
dotnet test tests/MudBlazor.Mcp.Tests/MudBlazor.Mcp.Tests.csproj -c Release --no-build

# Run a single test class or method (MTP uses hyphenated flags with wildcard patterns)
dotnet test --filter-class "*ComponentListToolsTests"
dotnet test --filter-method "*ListComponentsAsync_ReturnsAllComponents*"

# Run the server — `--version` is REQUIRED and must match the consumer's MudBlazor version.
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- --version 9.0.0            # HTTP on http://localhost:8000
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- --stdio --version 9.0.0    # stdio (CLI clients)

# Aspire dashboard (OpenTelemetry, health checks, service discovery)
dotnet run --project src/MudBlazor.Mcp.AppHost

# CLI (use the consumer project's exact MudBlazor version)
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- help
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- index --version 9.0.0
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- props MudButton --version 9.0.0
pwsh -NoProfile -File eng/scripts/Measure-CliLatency.ps1 -Version 9.0.0
```

## Critical Conventions (agents can't infer these)

- **`--version X.Y.Z` is mandatory** in every transport (or the `MUDBLAZOR_VERSION` env var used by IIS hosting). Missing/invalid version → the server prints guidance and exits with code 1. Format: `X.Y.Z` or `X.Y.Z-prerelease`. See argument parsing in [Hosting/McpServerHost.cs](../src/MudBlazor.Mcp/Hosting/McpServerHost.cs).
- **Routing only**: [Program.cs](../src/MudBlazor.Mcp/Program.cs) holds only the routing expression; MCP bootstrap lives in [Hosting/McpServerHost.cs](../src/MudBlazor.Mcp/Hosting/McpServerHost.cs).
- **CLI fast path**: queries build no host, DI container, configuration or logger and use no reflection. Register every new JSON type in [CliJsonContext](../src/MudBlazor.Mcp/Cli/CliJsonContext.cs) or [IndexStoreJsonContext](../src/MudBlazor.Mcp/Services/IndexStore.cs). Only `index` composes services.
- **CLI output** follows the format rules in [docs/11-cli.md](../docs/11-cli.md); stdout contains only the requested format and errors go to stderr.
- **Cache defaults differ**: CLI uses `~/.mudmcp`; MCP server stays at `./data`. Explicit data path or `MudBlazor__Repository__DataPath` shares caches.
- **Central Package Management**: add/bump NuGet versions in [Directory.Packages.props](../Directory.Packages.props) (`<PackageVersion>`), never inline `Version=` in a `.csproj`. `ManagePackageVersionsCentrally=true`.
- **`TreatWarningsAsErrors=true` repo-wide** ([Directory.Build.props](../Directory.Build.props)) with `EnforceCodeStyleInBuild` + `AnalysisLevel=latest`. In tests, **xUnit1051** fires whenever you await a method taking a `CancellationToken` without passing one — including setup I/O (`File.WriteAllTextAsync`) and `indexer.BuildIndexAsync()`. Fix: pass `TestContext.Current.CancellationToken`; use the named form `cancellationToken:` when optional params precede the token. Details in [docs/07-testing.md](../docs/07-testing.md).
- **`IsPackable=false` by default**; only the `MudBlazor.Mcp` project opts back in and packs as a **dnx .NET tool** (`PackageId=MudMCP`, `ToolCommandName=mudmcp`). Solution-wide `dotnet pack` emits only that one package.
- **`data/` is runtime-generated** (per-version repo clones + `index.json`) and excluded from compilation via `<DefaultItemExcludes>`. Never commit it.
- **All logs go to stderr** — stdout is reserved for MCP protocol frames in stdio mode.
- **slnx gotcha**: running a throwaway `dotnet run --project <outside-repo>` while this workspace is open makes C# Dev Kit rewrite [MudBlazor.Mcp.slnx](../MudBlazor.Mcp.slnx). Revert with `git checkout -- MudBlazor.Mcp.slnx`; keep throwaway projects outside the repo tree.
- **Copyright header** on every source file:
  ```csharp
  // Copyright (c) 2025 Mud MCP Contributors
  // Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.
  ```

## Code Patterns

**MCP tools** are `static` methods on `[McpServerToolType]` classes, marked `[McpServerTool(Name = "…")]` + `[Description]`. Services are injected as parameters; tool inputs are `[Description]`-annotated params. Tools are **auto-discovered** via `WithToolsFromAssembly()` — no registration needed. Return **Markdown** (LLM-friendly).

```csharp
[McpServerTool(Name = "get_component_detail")]
[Description("Gets comprehensive details about a MudBlazor component.")]
public static async Task<string> GetComponentDetailAsync(
    IComponentIndexer indexer,                                  // DI injected
    ILogger<ComponentDetailTools> logger,
    [Description("Component name")] string componentName,       // tool parameter
    CancellationToken cancellationToken = default)
{
    ToolValidation.RequireNonEmpty(componentName, nameof(componentName));
    // ...
}
```

- **Validation**: use [ToolValidation](../src/MudBlazor.Mcp/Tools/ToolValidation.cs) (`RequireNonEmpty`, `RequireInRange`, `ThrowComponentNotFound`) for MCP-friendly `McpException`s that LLMs can self-correct.
- **Domain models** in [Models/ComponentInfo.cs](../src/MudBlazor.Mcp/Models/ComponentInfo.cs) are immutable `record`s.
- **Flexible lookup**: `"Button"` resolves to `"MudButton"`.
- Full walkthrough for adding a tool: [docs/05-tools-reference.md](../docs/05-tools-reference.md).

## Testing Conventions

- Tests in [tests/MudBlazor.Mcp.Tests/](../tests/MudBlazor.Mcp.Tests/) mirror `src/` structure; Moq for interfaces, xunit.v3 for assertions.
- Use `NullLoggerFactory.Instance.CreateLogger<T>()` for test loggers.
- Cover both success and `McpException` error cases for every tool.

## Key Files

| Purpose | Location |
|---------|----------|
| Startup, arg parsing, DI, transports | [Hosting/McpServerHost.cs](../src/MudBlazor.Mcp/Hosting/McpServerHost.cs) |
| CLI router | [Cli/CliApp.cs](../src/MudBlazor.Mcp/Cli/CliApp.cs) |
| CLI index store | [Services/IndexStore.cs](../src/MudBlazor.Mcp/Services/IndexStore.cs) |
| Shared queries | [Services/ComponentQueries.cs](../src/MudBlazor.Mcp/Services/ComponentQueries.cs) |
| Agent Skill | [skills/mudblazor-cli/SKILL.md](../skills/mudblazor-cli/SKILL.md) |
| Version scoping / path resolution | [Configuration/VersionContext.cs](../src/MudBlazor.Mcp/Configuration/VersionContext.cs) |
| Options binding | [Configuration/MudBlazorOptions.cs](../src/MudBlazor.Mcp/Configuration/MudBlazorOptions.cs) |
| Index build/query | [Services/ComponentIndexer.cs](../src/MudBlazor.Mcp/Services/ComponentIndexer.cs) |
| Multi-version LRU cache | [Services/VersionCacheManager.cs](../src/MudBlazor.Mcp/Services/VersionCacheManager.cs) |
| Roslyn parsing | [Services/Parsing/XmlDocParser.cs](../src/MudBlazor.Mcp/Services/Parsing/XmlDocParser.cs) |
| Tool validation helpers | [Tools/ToolValidation.cs](../src/MudBlazor.Mcp/Tools/ToolValidation.cs) |
| Aspire host / ServiceDefaults | [AppHost/Program.cs](../src/MudBlazor.Mcp.AppHost/Program.cs), [ServiceDefaults/Extensions.cs](../src/MudBlazor.Mcp.ServiceDefaults/Extensions.cs) |

## Related Customizations

- Local domain agent in [.github/agents/](agents/): `csharp-expert`. To consume this server, and for MudBlazor or MCP-server engineering, use the [Clowder](https://github.com/mcbodge/clowder) agents — **CAT MudBlazor Engineer**, **CAT C# MCP Engineer**, and the `(MudBlazor)` planner and executor variants — which query the `mudblazor/*` MCP tools.
- Health checks: `/health`, `/health/ready`, `/health/live`. Aspire SDK pinned in [Directory.Packages.props](../Directory.Packages.props) (`Aspire.AppHost.Sdk` 13.5.3).
