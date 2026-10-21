# CLI Mode

The same MudMCP package provides the MCP server and a compact terminal CLI.
Queries are version-scoped, local and read-only; only `index` may clone sources.

## Install and run

Install the .NET 10 SDK, then prefer the global tool's direct apphost for repeated queries:

```text
dotnet tool install -g MudMCP
dotnet tool update -g MudMCP
mudmcp help
```

Zero-install fallback: `dnx MudMCP --yes -- help`. Always put tool arguments
after `--`. On .NET 10, dnx starts the SDK CLI and checks NuGet feeds on every
invocation, even for a pinned version
([dotnet/sdk#50579](https://github.com/dotnet/sdk/issues/50579)).
The global tool avoids that overhead.

From the repository root:

```text
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- help
```

Read the consumer project's MudBlazor version from its
`PackageReference Include="MudBlazor"` or central
`PackageVersion Include="MudBlazor"`. Examples here use 9.0.0; substitute
the project's exact version, including a prerelease suffix when applicable.
The CLI does not auto-detect it.

## Indexing

```text
mudmcp index --version 9.0.0
mudmcp index-status --version 9.0.0
mudmcp index-status
```

The first index may take minutes to clone the tagged repository and parse it.
Query commands never build or download an index automatically.
`index` recreates the split store; a valid existing `index.json` is reused
without cloning when its schema and parsing options match.

Data path precedence: `--data-path`, `MudBlazor__Repository__DataPath`,
then `.mudmcp` under the user home (`USERPROFILE` on Windows, `HOME` elsewhere,
then the runtime user-profile folder). Paths resolve to absolute paths.
The MCP server default remains `./data`; set its
`MudBlazor__Repository__DataPath` to the same absolute CLI data path to share caches.

```text
~/.mudmcp/v9.0.0/
  index.json
  mudblazor-repo/
  store/
    manifest.json
    catalog.json
    enums.json
    components/
      mudbutton.json
```

The schema-1 manifest records version, UTC build time, counts and the three
parsing options. The name-sorted catalog omits example code; component files
include full examples. Enums are name-sorted. No API-reference copies are stored.
Every full MCP index build writes the store. A cached MCP load derives it when
the manifest is missing, stale or for another version, so existing caches migrate.
Writes use a temporary sibling directory and rename swap; readers see a complete
old/new store or briefly no store (exit 3), never partially written files.

## Commands

All usages below accept global options except `help`. Names resolve
case-insensitively, with an optional `Mud` prefix for components.
Quoted generic names such as `"MudSelect<T>"` resolve to `MudSelect`.
After trimming and stripping the generic suffix, names must be 1-128 ASCII
letters, digits or underscores.

| Command | Options | MCP tool equivalent |
| --- | --- | --- |
| `mudmcp search <query...>` | `--in name\|description\|parameters\|examples\|all` (all), `--limit 1..50` (10) | `search_components` |
| `mudmcp list` | `--category <name>`, `--desc` | `list_components`, `get_components_by_category` |
| `mudmcp categories` | none | `list_categories` |
| `mudmcp component <name>` | `--no-inherited`, `--no-examples` (markdown only) | `get_component_detail` |
| `mudmcp props <name>` | `--filter <globs>`, `--category <param-category>`, `--required-only`, `--no-inherited`, `--limit 1..1000`, `--desc` | `get_component_parameters` |
| `mudmcp events <name>` | `--filter <globs>`, `--no-inherited`, `--limit 1..1000`, `--desc` | events in `get_component_detail`, `get_api_reference` |
| `mudmcp methods <name>` | `--filter <globs>`, `--no-inherited`, `--limit 1..1000`, `--desc` | methods in `get_component_detail`, `get_api_reference` |
| `mudmcp examples <name>` | `--names-only`, `--filter <text>`, `--limit 1..20` (3) | `get_component_examples`, `list_component_examples` |
| `mudmcp example <name> <example-name...>` | none | `get_example_by_name` |
| `mudmcp related <name>` | `--type all\|parent\|child\|sibling\|commonly_used_with` (all) | `get_related_components` |
| `mudmcp api <type>` | `--members all\|properties\|methods\|events` (all), `--desc` | `get_api_reference` (including enum fallback) |
| `mudmcp enum <name>` | `--desc` | `get_enum_values` |
| `mudmcp index` | version required | none |
| `mudmcp index-status` | version optional | none |
| `mudmcp help [command]` | none | none |

Member filters are comma-separated case-insensitive globs, ORed together:
`--filter Color,Variant,Size` matches those exact names; `--filter "*Color*"`
also matches IconColor. Quote patterns containing `*` or `?`.
Props apply category, required-only, inherited and glob filters, then member
ordering, then limit. Example filters are case-insensitive substrings of name,
description or features. `--names-only` lists all filtered names, without code.

## Global options

| Option | Behavior |
| --- | --- |
| `--version X.Y.Z` or `--version=X.Y.Z` | Trimmed; fallback `MUDBLAZOR_VERSION`; also accepts `X.Y.Z-prerelease`. Required except for help and index-status. |
| `--data-path <path>` | Overrides the environment and home default described above. |
| `--format compact\|markdown\|json` | Compact by default. |
| `--help` or `-h` after a command | Print that command's help and exit 0. |

MCP launches are unchanged: no arguments or host-style arguments go to the
server; `--stdio` selects stdio, default HTTP or `--http` selects HTTP.
Combining `--stdio` and `--http` exits 1 before version resolution.

## Output formats

Compact output is UTF-8 without BOM, ends in one newline and adds no Markdown
tables, headings, fences, hint footers or blank lines outside example bodies.
Example source itself is preserved as code and may contain such characters.

- Header: subject, `v<version>`, command and counts. Member counts are shown/total
  before filtering; examples use shown/total, or a single count for names-only.
- Properties: `Name: Type = Default`, then `[required]` and `[cascading]` when set.
  Events: `Name: EventCallback<Args>` (no generic suffix without args).
  Methods: `ReturnType Name(Type argument, ...)`.
- Own members come first in name order, then groups `-- inherited from Type`,
  sorted by declaring type (`base` if missing), with members in name order.
- `--desc` appends two spaces and `// ` plus at most 80 characters of description,
  with newlines collapsed; empty descriptions add nothing.
- `component` shows the qualified type, base, category, summary (160 characters),
  own parameter names and total counts, events, methods, example names,
  related names and docs URL; empty sections are omitted.
- Search prints name, `[Category]` (or `[-]`) and an 80-character summary.
  List groups names by category alphabetically, including Uncategorized;
  `list --desc` uses search-style lines instead. Categories prints names/counts.
  Related uses non-empty `parent:`, `children:`, `siblings:` and `other:` lines.
- Examples use `=== Name`, Razor, then `--- code` and C# when present.
  Code is dedented, trailing whitespace trimmed and repeated blank lines collapsed.
  Names-only adds at most three features in brackets.
- Enums normally occupy one line, with `Name=Value` for explicitly numbered values;
  `--desc` uses a header and one described value per line. API output uses
  `props N`, `events N`, `methods N` sections selected by `--members`.

```text
mudmcp props MudButton --filter Color,Variant,Size --version 9.0.0
```

```text
MudButton v9.0.0 props 3/23
Color: Color = Color.Default
Size: Size = Size.Medium
Variant: Variant = Variant.Text
```

`mudmcp component MudButton --version 9.0.0`:

```text
MudButton v9.0.0 component
MudBlazor.MudButton : MudBaseButton [Buttons]
Represents a button for actions, links, and commands.
props 23 (own 10): ChildContent, Color, EndIcon, FullWidth, IconClass, IconColor, IconSize, Size, StartIcon, Variant
events 1: OnClick
methods 1: FocusAsync
examples 11: Customized, Drop Shadow, Filled, Full Width, Icon Label, Link, Loading, Nofollow, Outlined, Size, Text
docs: https://mudblazor.com/components/button
```

`--format markdown` reuses MCP renderers for equivalent inputs; the CLI examples
default is still 3 (MCP defaults to 5). For comparison, Markdown parameters start:

```markdown
# MudButton Parameters (v9.0.0)

## Appearance

### `Color`

- **Type:** `Color`
- **Default:** `Color.Default`
- **Description:** The color of the button.
```

JSON is minified camelCase with default values omitted, using source-generated
serialization. Component overviews and names-only examples omit example code.
Index/status treat markdown as compact. Zero matches exit 0: compact header only,
JSON `[]`, or the corresponding Markdown renderer's empty-result message.

## Exit codes

| Code | Meaning / action |
| --- | --- |
| 0 | Success, including zero matches. |
| 1 | Runtime error: I/O, corrupt store or index build failure; inspect stderr and rebuild the index. |
| 2 | Usage error: fix arguments, invalid names or version. |
| 3 | Version not indexed: run the hinted index command. |
| 4 | Component, type, enum, example or category not found: use suggestions or search. |
| 130 | Cancelled with Ctrl+C. |

Errors go only to stderr as one `error: ` line and optionally one `hint: ` line.
For a missing version:

```text
error: MudBlazor version required: pass --version X.Y.Z or set MUDBLAZOR_VERSION
hint: indexed versions: 9.0.0
```

With no ready versions, the hint is
`hint: read Version from <PackageReference Include="MudBlazor" ...> in your .csproj or Directory.Packages.props`.
For a missing/stale store:

```text
error: MudBlazor v9.0.0 is not indexed in <full-data-path>
hint: mudmcp index --version 9.0.0
```

Missing component/type hints suggest up to five names, ignoring the Mud prefix,
ranked by length difference then name. An enum name hints
`mudmcp enum Color --version 9.0.0`; without candidates the hint is
`mudmcp search <input> --version 9.0.0`.

## Performance

Queries route before host construction: no Host, WebApplication, DI container,
configuration or logger factory is built (only `index` composes services).
JSON reads are synchronous byte reads with source-generated type metadata, not
reflection; version validation uses a generated regex. Single-component queries
read the manifest and one component file (v9.0.0 median 4,525 bytes, p95 34,826,
max 111,853); enums read 42,130 bytes; catalog queries read 695,438 bytes rather
than the 2,360,116-byte minified index.

`MUDMCP_TIMING=1` adds `elapsed-ms=` (one decimal) and `process-ms=` (integer)
to stderr after stdout flush. Elapsed measures CLI entry through that flush;
process includes runtime startup and is reported, not gated.

The **50 ms p95 is advisory**, measured across 20 fresh processes with warm OS
file cache, not a warmed process. The benchmark warns and exits 0 for successful
measurements; `-StrictBudget` opts into exit 1 for overruns. Real command/build
or timing failures still fail. Run:

```powershell
pwsh -NoProfile -File eng\scripts\Measure-CliLatency.ps1 -Version 9.0.0
```

Recorded Windows run, 2026-10-21 (milliseconds; all 13 commands exceeded target):

| Command | p50 elapsed | p95 elapsed | p50 process | p95 process |
| --- | ---: | ---: | ---: | ---: |
| component MudButton | 116.9 | 155.6 | 253 | 327 |
| props MudButton --filter Color,Variant,Size | 109.3 | 144.7 | 242 | 289 |
| props MudDataGrid | 108.2 | 148.1 | 248 | 347 |
| events MudButton | 100.9 | 122.8 | 231 | 255 |
| methods MudButton | 95.1 | 131.1 | 221 | 260 |
| api MudButton | 115.3 | 158.0 | 251 | 299 |
| enum Color | 96.4 | 119.8 | 219 | 282 |
| example MudButton Filled | 101.7 | 123.6 | 226 | 274 |
| examples MudButton --names-only | 99.6 | 121.5 | 221 | 262 |
| search button | 126.1 | 208.0 | 255 | 343 |
| list | 125.2 | 165.2 | 251 | 292 |
| categories | 125.3 | 157.5 | 251 | 304 |
| related MudButton | 133.1 | 181.8 | 262 | 352 |

Cold JSON metadata initialization and JIT remain significant even with warm
file cache. A diagnostic props probe took 192.80 ms initially and 3.79/2.12 ms
within the same process; those warm results are not substituted for this table.
The timing-boundary correction and removal of an unused compact API model
preserve output; paired runs showed identical stdout but inconsistent tail
improvements. Overruns are accepted with this technical explanation, not hidden.
No runtime, GC, globalization, schema or distribution changes were introduced.
See [profiling methodology and rejected experiments](../eng/README.md#cli-startup-benchmark).
Use a globally installed tool rather than dnx to avoid SDK/feed-check overhead.

Token estimates below use characters / 4 on real v9.0.0 output (2026-10-20),
not a tokenizer or guaranteed current byte count. Compact output reduces context,
not necessarily startup latency.

| CLI compact command | MCP comparison | MCP tokens | CLI tokens | Saving |
| --- | --- | ---: | ---: | --- |
| Skill metadata only | 12 tool schemas per turn | ~2,100 | ~100 once, body when triggered | ~2,000 per turn |
| props MudButton | get_component_parameters | 820 | 157 | 81% |
| props MudButton --filter Color,Variant,Size | get_component_parameters | 820 | 28 | 97% |
| props MudDataGrid | get_component_parameters | 3,292 | 774 | 76% |
| component MudButton | get_component_detail | 938 | 111 | 88% |
| component MudDataGrid | get_component_detail | 5,961 | 663 | 89% |
| search button | search_components | 556 | 221 | 60% |
| list | list_components (details) | 4,600 | 566 | 88% |
| enum Color | get_enum_values | 312 | 32 | 90% |
| examples MudButton | get_component_examples (5) | 577 | 166 | 71% |
| examples MudButton --limit 5 | get_component_examples (5) | 577 | 386 | 33% |
| examples MudButton --names-only | list_component_examples | 185 | 103 | 44% |

## Agent Skill

Copy the [mudblazor-cli folder](../skills/mudblazor-cli/SKILL.md) into your
repository's `.github/skills/`, `.claude/skills/` or `.agents/skills/`, or your
user-level `~/.copilot/skills/`, `~/.claude/skills/` or `~/.agents/skills/`.
The skill reads the consumer's package version, verifies exact members before
editing, and fetches example code only after narrow member queries.

## Copilot instructions snippet

Replace 9.0.0 with your project's version and paste into your consumer
repository's `.github/copilot-instructions.md`:

```markdown
## MudBlazor component APIs

- This repository uses MudBlazor 9.0.0 (use the Version of the MudBlazor PackageReference). Never guess Mud* parameter, event, or enum names: verify them with the mudmcp CLI first.
- Always pass --version 9.0.0. Start narrow: mudmcp props MudButton --filter Color,Variant --version 9.0.0; then component, events, methods, or enum; fetch code only with examples --names-only followed by example.
- Exit code 3: run mudmcp index --version 9.0.0 once. Exit code 4: use the suggested names or mudmcp search.
```
