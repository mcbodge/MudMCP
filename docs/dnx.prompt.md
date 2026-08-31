# Plan: Add `dnx` support + docs

## Goal
Enable running the MudBlazor MCP server via `dnx` (.NET 10 one-off tool runner) and update docs.
Reference: NuGet.Mcp.Server config `{ "command": "dnx", "args": ["NuGet.Mcp.Server@1.4.3", "--yes"] }`.

## Research findings (confirmed)
- Main project `src/MudBlazor.Mcp/MudBlazor.Mcp.csproj` uses `Microsoft.NET.Sdk.Web`. NO PackAsTool/ToolCommandName/PackageId/Version/authors/license.
- Requires `--version X.Y.Z` arg OR `MUDBLAZOR_VERSION` env var (errors out if missing). Program.cs parses `--stdio`, `--version`.
- global.json pins SDK 10.0.100, allowPrerelease true. nuget.config = nuget.org only.
- Data cache default `./data` RELATIVE to cwd (VersionContext). dnx from arbitrary cwd => clone lands in that folder.
- Azure pipeline = IIS deploy only, no pack/push. No GitHub Actions. Directory.Build.props has no package metadata.
- Config files today: mcp.local.json (dotnet run), mcp.executable.json (published exe).
- Docs to touch: README.md, docs/02-getting-started.md, docs/09-ide-integration.md (+ maybe 08-mcp-inspector, 10-troubleshooting).

## Key issues / decisions to resolve
1. DISTRIBUTION FEED: nuget.org (public) vs Azure Artifacts (private) vs packable+local-feed-only. (BLOCKING)
   - Concern: PackageId "MudBlazor.Mcp" prefix may be reserved/trademark on public nuget.org.
2. `--version` COLLISION: dnx/`dotnet tool exec` has its own `--version` (package version). Passing MudBlazor version as tool arg needs `--` separator OR use MUDBLAZOR_VERSION env var (recommended).
3. PackageId + ToolCommandName naming.
4. CI automation: add pack+push step or manual publish?
5. Data cache location when run via dnx (pollutes cwd with ~100MB clone). Keep ./data / user-profile / doc env override.
6. Web SDK packed as tool => carries ASP.NET runtime dependency; keep single project (stdio+HTTP) vs split.

## DECISIONS (confirmed via interview)
1. Distribution = **C**: make packable + docs now, validate via LOCAL folder feed. NO public/private publish, NO CI push (out of scope).
2. Naming = PackageId **MudBlazor.Mcp**, ToolCommandName **mudblazor-mcp**, Version **1.0.0**.
3. Version passing = **env var MUDBLAZOR_VERSION** primary (avoids dnx `--version` collision); document `--` passthrough as alt.
4. Cache = **A**: no code change, keep `./data`; document `MudBlazor__Repository__DataPath` override for dnx.
5. Keep single Web SDK project, pack it as the tool (carries ASP.NET dep — fine on .NET 10 SDK).
6. IsPackable=false in Directory.Build.props, true only in main csproj (no stray ServiceDefaults/AppHost/test pkgs).
7. Docs: README + 02 + 09 (core) AND 08 + 10 (secondary).
8. RepositoryUrl: docs only have placeholders (yourusername/YourOrg) -> use placeholder, flag to replace with real remote.

## IMPLEMENTATION PLAN

### A. Packaging metadata
- Directory.Build.props: add `<IsPackable>false</IsPackable>`.
- src/MudBlazor.Mcp/MudBlazor.Mcp.csproj PropertyGroup add: PackAsTool=true, ToolCommandName=mudblazor-mcp,
  PackageId=MudBlazor.Mcp, Version=1.0.0, IsPackable=true, Authors="Mud MCP Contributors",
  Description (mention dnx + MUDBLAZOR_VERSION), PackageTags (mcp;modelcontextprotocol;mudblazor;blazor;ai;dotnet-tool;dnx),
  PackageLicenseFile=LICENSE, PackageReadmeFile=README.md, RepositoryUrl(placeholder)+RepositoryType=git, PackageProjectUrl.
- csproj ItemGroup: <None Include="..\..\LICENSE" Pack="true" PackagePath="\"/> and same for ..\..\README.md.
- Watch TreatWarningsAsErrors: supply all metadata to avoid NU5xxx-as-error; add NoWarn fallback if needed.

### B. New config file (repo root) mcp.dnx.json
- mcpServers.mudblazor: command "dnx", args ["MudBlazor.Mcp@1.0.0","--yes","--","--stdio"], env {MUDBLAZOR_VERSION:"9.0.0"}.

### C. Docs
- README.md: add "Option D: dnx (no install)" in Local MCP(stdio) + transport-comparison row; note feed prereq + local-feed `--source ./nupkg`; add `dotnet pack` step.
- docs/02-getting-started.md: "Running via dnx" subsection (pack -> dnx run, env var).
- docs/09-ide-integration.md: dnx config blocks (VS Code `servers`, Claude `mcpServers`, Continue.dev), MUDBLAZOR_VERSION + DataPath override notes, feed prereq.
- docs/08-mcp-inspector.md: dnx invocation for Inspector.
- docs/10-troubleshooting.md: dnx pitfalls (dnx needs .NET10 SDK; `--version` collision; pkg-not-found needs feed/--source; cache lands in cwd ./data + DataPath override).

### D. Verification
1. `dotnet pack src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -c Release -o ./nupkg` -> MudBlazor.Mcp.1.0.0.nupkg with tools/net10.0/any/ + DotnetToolSettings.xml (command mudblazor-mcp).
2. Solution-wide `dotnet pack -c Release` -> ONLY MudBlazor.Mcp.nupkg (no ServiceDefaults/AppHost/test).
3. `dnx MudBlazor.Mcp --source ./nupkg --yes -- --stdio` with MUDBLAZOR_VERSION=9.0.0 -> starts, responds to MCP initialize/tools/list. Optionally via `npx @modelcontextprotocol/inspector`.
4. `dotnet build` clean (warnings-as-errors), `dotnet test --no-build` green.
5. Validate mcp.dnx.json parses.
6. Optional: `dotnet tool install -g --add-source ./nupkg MudBlazor.Mcp` -> `mudblazor-mcp --stdio` -> uninstall.