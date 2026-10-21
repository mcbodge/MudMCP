# Plan Execution — working notes

plans:
- docs/plans/plan-1-feature-cli-mode-1.prompt.md (implementation) — phases GOAL-001..GOAL-007, TASK-001..030
  - Delivery: local (branch develop, never push)
  - Plan N = 1 → commit trailer `plan-1 GOAL-###`

current: plan-1 - WAITING ONLY FOR TEST-022 manual VS Code agent interaction.
  TASK-001..025 and TASK-027..030 complete and checked. TASK-026 implementation/static checks pass;
  leave its mark blank until TEST-022 evidence proves skill-triggered pre-edit CLI usage.
  Plan remains In progress. Delivery local on develop; never push.
  Code/docs commits this run: 1e0cdca (GOAL006 metadata/package validation), 5e20303 (GOAL007 static docs/skill).
  All independent implementation/validation/delivery done. No remaining code task or automated gate.
  Reuse prior once-per-plan independent review (major store finding fixed in9666431).
  Reuse recorded TASK024 measurements and 501/501 full-suite pass (task-024-optimized-fullsuite.txt).
  TEST020 passed including exact FMT009, idle stdout and a real MCP initialize exchange; test process stopped.
  TEST021 passed: 15 commands in both docs, valid 107-line skill, 21 added relative links resolve.
  git diff --check passed. No new runtime behavior changed; no redundant suite or latency rerun.

TEST-022 prepared (2026-10-21):
  Global MudMCP 1.1.0-local.1 installed from freshly repacked artifacts/cli-check.
  No global MudMCP existed before this run; don't disturb unrelated dnx instances.
  Skill copied to C:\Users\ma.grillo\.copilot\skills\mudblazor-cli (absent before run);
  SHA256 equality asserted against source.
  Global tool props MudButton --filter Color,Variant,StartIcon --version 9.0.0 succeeds;
  index-status v9.0.0 ready. These direct checks are NOT the agent trial.
  Disposable project:
    C:\Users\ma.grillo\.copilot\session-state\ba75bc3d-e727-432a-8f30-21516c303d49\files\mudmcp-agent-trial
  Scaffold: dotnet new blazor --name MudMcpAgentTrial --framework net10.0 --no-restore;
  added exact MudBlazor 9.0.0 via dotnet add package. Baseline Release build passed.
  Requested a new VS Code Insiders window with code-insiders --new-window <trial path>.
  User action: choose standard Copilot Agent mode in a NEW chat in that project, ask exactly
    "Add a filled primary MudButton with a start icon to the Home page".
  Do not explicitly invoke the skill or mention CLI in the prompt: automatic discovery is under test.
  Request evidence (screenshot or copied tool/edit sequence) of mudmcp props MudButton or
  component MudButton with --version 9.0.0 BEFORE first edit; then inspect resulting markup
  for Variant.Filled, Color.Primary and StartIcon and build the project yourself.
  Do not count installation, this session's direct CLI probe, or a user's bare "done" as trial proof.
  On pass: mark TASK026, complete plan; on failure: characterize actual evidence before changing anything.
  Cleanup AFTER trial: dotnet tool uninstall -g MudMCP; delete only the copied user skill directory
  C:\Users\ma.grillo\.copilot\skills\mudblazor-cli and repository artifacts\cli-check.
  Disposable trial folder may be deleted after validation, only by its explicit full path.
  Leave these prepared resources in place while awaiting interaction.
  Commit any subsequent code/docs changes separately from plan/handoff progress.

Previous handoff (TASK-024):
  Validated TASK-024 code/scripts/documentation committed in4fcc24b.
  Phases 1-5 complete; Phase 6 packaging TASK-025 is next. Phase 7 remains untouched.
  Plan status stays In progress; TASK-001..024 checked. Delivery local on develop; never push.
  User approved profiling/optimizing startup with technically justified overruns of the nominal50ms target.
  Source-generated JSON metadata/JIT initialization dominates cold startup; warm OS cache is not a warm process.
  Changes: elapsed captured immediately after stdout flush, before diagnostic overhead; compact api no longer creates an unused API-reference model.
  Benchmark keeps default BudgetMs50, warns explicitly above it; StrictBudget opts into exit1 on overrun.
  Default full20-run/13-command benchmark exits0 with warnings; strict exits1 honestly (p95 130.7..257.2ms).
  Scripts self-check percentiles, pipe draining, argument preservation, strict/default policy and environment restoration.
  TDD cycle: timing-overhead regressions and advisory-default script checks failed Red, then passed.
  Focused .NET145/145 and CLI306/306; own solution build/full suite and script self-checks passed.
  Paired old/new binaries alternated order20times per command; every stdout pair byte-identical.
    props p50 121.7->121.3ms, p95 180.1->159.6ms;
    api p50 137.5->127.7ms, p95 169.5->190.1ms;
    list p50 144.4->141.4ms, p95 170.5->201.8ms.
  No general tail-latency improvement claimed; documented measured overhead is accepted, not hidden.
  Profiling evidence and rejected experiments: eng/README.md, logs/task-024-profile-*.txt, logs/task-024-paired-comparison.txt.
  No serializer/schema/runtime/GC/globalization/AOT/ReadyToRun/package changes.
  Prior independent review already ran once; finding fixed in9666431; do not repeat once-per-plan review.
  Use explicit staging AND commit pathspecs; original Phase5 commit0f7a632 unexpectedly included this handoff file.
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
### TASK-025..030 resumed 2026-10-21
- TASK-025 metadata-only change; Red skipped (non-behavioral). TEST-020 produced exactly MudMCP.1.1.0-local.1.nupkg, installed isolated tool, verified all 15 help commands and exact real-cache FMT009 output.
- Packaged stdio stayed running with no stdout for 5 seconds; initialize request returned valid serverInfo. Logs only stderr. Process stopped by its specific PID; artifacts/cli-check removed after validation.
- TASK-025 commit 1e0cdca; pack also compiled main project. Logs/task-025-{pack,install,help,props,stdio,stdio-stderr}.txt.
- TASK-026..030 documentation-only; Red skipped. Skill uses quoted YAML description (colon-safe), 107 lines, prescribed headings/frontmatter; CLI reference includes recorded full TEST019 table and honest advisory target.
- TEST-021 passed: help from source lists 15 commands, all present in skill/reference; bounded frontmatter, one skill file, four documentation backlinks, 21 newly added relative file links resolve. Logs/task-021-{help,docs}.txt.
- TEST-022 not yet run; do not mark trial or plan complete without observed pre-edit CLI query, valid resulting parameters and validation evidence.
- TASK-027..030 checked complete; TASK-026 deliberately unchecked pending TEST-022. Static skill/docs committed 5e20303.
- TEST-022 preparatory commands and baseline project build passed; logs/task-022-{pack,isolated-install,global-install,installed-props,index-status,scaffold,package,baseline-build}.txt.
- Global tool and copied skill were absent before installation. Cleanup is intentionally deferred until the user's trial, not forgotten.
- Reused 501/501 passing full suite and prior independent review/fix; only metadata and documentation changed this run.
- No web UI validation TEST is specified; Playwright is not applicable to the terminal skill-discovery trial.
### TASK-024 extension 2026-10-21
- User explicitly authorized startup profiling/optimization and technically justified >50ms; PER001, TASK024, TEST019 and GOAL006 updated to match.
- Sampled CPU trace showed JSON metadata creation/configuration in manifest/component reads; trace artifacts ignored under logs.
- Cold/warm probes: manifest80.85ms then~1ms; component38.92ms then~1ms; same in-process CLI192.80ms then3.79/2.12ms. No host/MCP/Roslyn/LibGit2Sharp assemblies observed in query probe.
- Parameterless-constructor experiment53.95->53.62ms median: rejected. Partial-manifest reader~7%median gain but altered validation/sourcegen behavior: rejected.
- Buffered stdout flush5.36..7.98ms, not dominant; async cancellation behavior preserved.
- Exact stdout-flush measurement regression (success/error) Red confirmed; Green145/145 focused tests and306/306 CLI. Own build/fullsuite passed.
- Default benchmark logs/task-024-profile-after.txt exit0; strict logs/task-024-profile-after-strict.txt exit1 with13 above-target commands. Script selfchecks pass logs/task-024-optimized-script-tests.txt.
- Alternating before/after20samples each, props/api/list outputs identical; paired measurements logs/task-024-paired-comparison.txt, modest median changes and variable tails documented without claiming a general speedup.
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
