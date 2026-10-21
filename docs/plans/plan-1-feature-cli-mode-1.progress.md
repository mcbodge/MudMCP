# Plan Execution — working notes

plans:
- docs/plans/plan-1-feature-cli-mode-1.prompt.md (implementation) — phases GOAL-001..GOAL-007, TASK-001..030
  - Delivery: local (branch develop, never push)
  - Plan N = 1 → commit trailer `plan-1 GOAL-###`

current: plan-1 - BLOCKED at Phase 6 TASK-024 (2026-10-21). Phases 1-5 DONE and committed.
  Phase 5 commit 0f7a632; review fix commit 9666431. Full suite 499/499, solution build exits 0.
  Plan status stays In progress; TASK-001..023 checked. TASK-024 remains blank because its latency gate fails.
  RESUME AT: ask user to authorize focused latency investigation/optimization or revise the performance scope.
    eng/scripts/Measure-CliLatency.ps1 and Test-MeasureCliLatency.ps1 are implemented but UNCOMMITTED.
    Self-checks pass, including percentiles, pipe draining, argument boundaries, exit codes, environment restoration.
    Initial real 20-run benchmark: every command fails 50 ms p95, range 75.4..131.6 ms.
    Independent exact 20-run verification: every command fails, range 107.0..302.3 ms.
    Logs: logs/task-024-benchmark.txt, logs/verify-task-024-benchmark.txt, logs/verify-task-024-selfchecks.txt.
    Diagnostics: help p95 28.5 ms, index-status p95 106 ms; startup/store overhead not yet isolated.
    Budget and runtime configuration were NOT changed. Never claim TASK-024 complete from self-checks alone.
  TASK-025 and Phase 7 remain untouched: required Phase 6 DONE-WHEN does not hold.
  Independent CAT Code Reviewer ran ONCE over all five code-phase commits, verdict changes requested:
    major: required catalog/enums missing or JSON null silently returned empty success.
    Fixed test-first in 9666431; missing files now raise I/O errors and null payloads raise JsonException.
    Existing component files containing null now raise JsonException; absent component lookup still returns null.
    Red confirmed 12 failures; focused 235/235, full 499/499; own full build/test also passed.
  Phase 5 real v9.0.0 props/component outputs passed exact FMT-009/FMT-010 assertions.
  Delivery local: develop, never push. Review has already run; do not repeat the once-per-plan review.
  Commit-scope deviation: 0f7a632 unexpectedly included this pre-existing handoff progress file.
    Main plan was kept out of code commits. No history rewrite; use explicit commit pathspecs going forward.
  NOTE: 13 lingering dotnet.exe are the user's installed MudMCP dnx tool — NOT mine; leave alone. Built DLL: src/MudBlazor.Mcp/bin/Release/net10.0/MudBlazor.Mcp.dll

app: n/a (CLI/library; no web UI, no Playwright)

BASELINE (2026-10-20): build -c Release EXIT 0, 0 Error(s), 1 Warning(s) = pre-existing ASPIRE010
  (AppHost AspireUseCliBundle=false; NOT in MudBlazor.Mcp; environmental). Tests: 130 passed, 0 failed.
  => interpret phase DONE-WHEN "0 Warning(s)" as 0 warnings from MudBlazor.Mcp + tests (ASPIRE010 ignored).

## Commands
- build all: dotnet build -c Release > logs/x.txt 2>&1   (TreatWarningsAsErrors on compiler warnings)
- build main only (0-warning check): dotnet build src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -c Release
- test all (MTP): dotnet test tests/MudBlazor.Mcp.Tests/MudBlazor.Mcp.Tests.csproj -c Release --no-build
- test class (MTP, builds): dotnet test tests/MudBlazor.Mcp.Tests/MudBlazor.Mcp.Tests.csproj -c Release --filter-class "*ClassName"
  (confirmed working; no `--` separator; add --no-build only after a build)
- csproj ProjectReference group = the ServiceDefaults ref; add InternalsVisibleTo item group after it.
- Copyright header for NEW files uses year 2026 (CON-002):
    // Copyright (c) 2026 Mud MCP Contributors
    // Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

## Manifest (per phase/task: owning files, tests, validation cmd, latest failure)
### Phase 1 (GOAL-001) DEPENDS none
- TASK-001 VersionArgument.cs + csproj InternalsVisibleTo → VersionArgumentTests → --filter-class "*VersionArgumentTests"
- TASK-002 McpServerHost.cs + IndexerHealthCheck.cs + Program.cs → McpServerHostTests → --filter-class "*McpServerHostTests"  (Depends TASK-001)
- TASK-003 --http alias in McpServerHost → McpServerHostTests  (Depends TASK-002)
### Phase 2 (GOAL-002) DEPENDS GOAL-001
- TASK-004 ComponentQueries.cs + ComponentIndexer delegate → ComponentQueriesTests
- TASK-005 ComponentListTools renderers → ComponentListToolsTests
- TASK-006 ComponentDetailTools renderers → ComponentDetailToolsTests
- TASK-007 ComponentSearchTools renderers + resolved-name fix + GroupRelated → ComponentSearchToolsTests
- TASK-008 ComponentExampleTools helpers/renderers → ComponentExampleToolsTests
- TASK-009 ApiReferenceTools renderers → ApiReferenceToolsTests
### Phase 4 (GOAL-004) DEPENDS GOAL-001, GOAL-003
- TASK-013 CliException.cs + CliArgs.cs → CliArgsTests
- TASK-014 CliApp/CliCommands/CompactFormatter/CliJsonContext + help + index-status + CliTestStore → CliAppTests  (Depends TASK-013)
- TASK-015 Program.cs routing + CliApp.RunAsync → manual TEST-014  (Depends TASK-014)
- TASK-016 index command → CliIndexCommandTests  (Depends TASK-014)
### Phase 5 (GOAL-005) DEPENDS GOAL-002, GOAL-004
- TASK-017 component → CliComponentCommandsTests
- TASK-018 props/events/methods → CliComponentCommandsTests  (Depends TASK-017)
- TASK-019 api/enum → CliComponentCommandsTests  (Depends TASK-018)
- TASK-020 examples/example → CliExampleCommandsTests  (Depends TASK-017)
- TASK-021 search → CliCatalogCommandsTests
- TASK-022 list/categories → CliCatalogCommandsTests  (Depends TASK-021)
- TASK-023 related → CliCatalogCommandsTests  (Depends TASK-017, TASK-021)
### Phase 6 (GOAL-006) DEPENDS GOAL-005
- TASK-024 eng/scripts/Measure-CliLatency.ps1 → TEST-019 (manual)
- TASK-025 csproj Description/PackageTags → TEST-020 (manual)
### Phase 7 (GOAL-007) DEPENDS GOAL-005, GOAL-006
- TASK-026 skills/mudblazor-cli/SKILL.md → TEST-021 (manual)
- TASK-027 docs/11-cli.md → TEST-021
- TASK-028 README.md → TEST-021  (Depends TASK-027)
- TASK-029 docs/03-architecture.md + docs/05-tools-reference.md → TEST-021
- TASK-030 .github/copilot-instructions.md → TEST-021

## Pre-flight
- (implementation plan — no work items)

## Evidence
### Resumed execution 2026-10-21
- TASK-017: component command; 37 cases, Red unknown command, Green37/37; own verification passed.
- TASK-018: member commands; 42 new cases, Red expected, Green79/79; own verification passed.
- TASK-019: api/enum; 36 new cases, Red36 failures, Green115/115; own verification passed.
- TASK-020: examples/example; 52 cases, Red expected, Green52/52; own verification passed.
- TASK-021: search; 28 cases, Red28 failures, Green28/28. Own first verification had a transient temp-folder rename access denial; unchanged retry passed.
- TASK-022: list/categories; 33 added cases, Green61/61 catalog; own verification passed.
- TASK-023: related; 32 cases, Red32 failures, Green93/93 catalog and297/297 CLI.
- GOAL-005: full build/tests pass (logs/phase5-build.txt and logs/phase5-fullsuite.txt); exact real-cache FMT009/010 outputs pass; code commit0f7a632.
- TASK-024: controlled-input self-checks pass, real latency gate fails; scripts left uncommitted, not marked complete.
- Review: one major required-store corruption finding, fixed in9666431; own build/fullsuite pass (logs/review-fix-build.txt and logs/review-fix-fullsuite.txt), 499 tests.
- Playwright: not applicable; no web UI flow in this implementation plan.
### Phase 1
- TASK-001: VersionArgument.cs (new), csproj InternalsVisibleTo, VersionArgumentTests.cs (new). Red ok; 11/11 pass (verify-task-001.txt). Refactor n/a. VERIFIED by me.
- TASK-003: McpServerHost --http alias + guard + StripCustomArgs skip; McpServerHostTests 5/5; suite 146/146.
- PHASE 1 DONE: commit 51ff7b5 (plan-1 GOAL-001). TEST-003 smoke: (1)(2) /health/live Healthy w/ & w/o --http on :8000; (3) --stdio --http exit 1 + msg; (4) stdio served initialize+tools/list (proxy; Inspector GUI not run). 1 pre-existing ASPIRE010 warning only.
### Phase 2 (DONE, committed a1e1b2b plan-1 GOAL-002)
- TASK-004: ComponentQueries.cs (new: BuildCategories/Search/FindRelated/CalculateSearchScore), ComponentIndexer.cs delegates. REQ-028 (ThenBy, ordered siblings). ✅
- TASK-005: ComponentListTools RenderComponentList/RenderCategories. TEST-005 7/7. ✅
- TASK-006: ComponentDetailTools RenderDetail/RenderParameters. TEST-006 16/16. ✅
- TASK-007: ComponentSearchTools 4 members internal + RenderSearchResults/RenderRelated + resolved-name fix; ComponentQueries.GroupRelated. TEST-007 18/18, TEST-004 6/6. ✅
- TASK-008: ComponentExampleTools FilterExamples/FindExample/RenderExamples/RenderExample/RenderExampleList. TEST-008 18/18. ✅
- TASK-009: ApiReferenceTools ValidMemberTypes internal + RenderApiReference + RenderEnum(string version). TEST-009 29/29. ✅
- PHASE 2 GATE: build -c Release 0 err / 1 ASPIRE010; full suite 172/172. Commit a1e1b2b.
### Phase 3 (DONE, committed 0ad5af1 plan-1 GOAL-003)
- TASK-010+011: IndexStore.cs (new: StoreManifest, IndexStoreJsonContext camelCase/WhenWritingDefault, WriteAsync atomic temp→store→old, TryReadManifest/IsCurrent, NormalizeName/TryReadComponent/ReadCatalog/ReadEnums/TryReadEnum) + VersionContext.StorePath. TEST-010 14/14. ✅
- TASK-012: ComponentIndexer CacheSchemaVersion internal + EnsureStoreAsync(force) at cached-load (force:false) and post-SaveCachedIndex (force:true). TEST-011 ComponentIndexerTests 17/17. ✅
- PHASE 3 GATE: build 0 err / 1 ASPIRE010; full suite 189/189. Commit 0ad5af1.
### Phase 4 (DONE, committed 5e6d03e plan-1 GOAL-004)
- TASK-013: CliException.cs (CliExitCodes, CliException) + CliArgs.cs (Parse). TEST-012 CliArgsTests 8/8. ✅
- TASK-014: CliApp/CliCommands/CompactFormatter/CliJsonContext + help + index-status + CliTestStore fixture. TEST-013 CliAppTests 27/27. ✅ (json index-status omits data-path text header → valid JSON)
- TASK-015: Program.cs routing + CliApp.RunAsync(string[]) overload (UTF8 no-BOM stdout, Ctrl+C cancel). TEST-014 manual: help exit0+commands:, bare --version exit1 server guidance, serach exit2 unknown command+hint. ✅ (inline)
- TASK-016: index command + REQ-024 missing-version hint (ReadyVersions). TEST-015 CliIndexCommandTests 2/2, CliAppTests still 27/27. ✅
- PHASE 4 GATE: build 0 err / 1 ASPIRE010; full suite 226/226; index exit0 "9.0.0 ready components=146 enums=85", index-status exit0. Commit 5e6d03e.
(per task: TDD result, commit hash, issues)
