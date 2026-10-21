---
name: mudblazor-cli
description: 'Look up version-accurate MudBlazor component APIs with the mudmcp CLI before writing or changing Blazor markup that uses Mud* components: parameter names, types and defaults, events, methods, enum values, and code examples for the exact MudBlazor version of the project. Use it when editing .razor or .razor.cs files with Mud* tags, when a Mud* parameter, event, or enum value is uncertain, or when a build error names a MudBlazor member. Output is compact and token-frugal.'
license: GPL-2.0-only
compatibility: Requires the .NET 10 SDK and the MudMCP .NET tool (dotnet tool install -g MudMCP) or dnx. Network access is needed only for the one-time mudmcp index per MudBlazor version.
---

# MudBlazor CLI (mudmcp)

## When to use

Use before writing or changing .razor or .razor.cs files with Mud* components,
when a parameter, event, method or enum is uncertain, when a build error names
a MudBlazor member, and before guessing any Mud* member name.

## Setup

Read the project's MudBlazor version from PackageReference Include="MudBlazor"
in *.csproj, or PackageVersion Include="MudBlazor" in Directory.Packages.props.
All examples below use 9.0.0; replace it with the project's version.

```text
dotnet tool install -g MudMCP
dotnet tool update -g MudMCP
mudmcp index --version 9.0.0
mudmcp index-status --version 9.0.0
```

Index once per version; the first clone and parse can take minutes.
Prefer the global tool for repeated queries. Zero-install fallback:
`dnx MudMCP --yes -- props MudButton --version 9.0.0`.
It is slower because dnx checks NuGet feeds on every call; tool arguments go after --.

## Workflow

1. Find: `mudmcp search "date picker" --version 9.0.0` or
   `mudmcp list --category Buttons --version 9.0.0`.
2. Overview: `mudmcp component MudDatePicker --version 9.0.0`.
3. Verify exact members before editing:
   `mudmcp props MudButton --filter Color,Variant,Size --version 9.0.0`;
   `mudmcp events MudButton --version 9.0.0`;
   `mudmcp methods MudDataGrid --filter "*Async" --version 9.0.0`;
   `mudmcp enum Color --version 9.0.0`.
4. Fetch code only when a markup pattern is needed:
   `mudmcp examples MudButton --names-only --version 9.0.0`, then
   `mudmcp example MudButton "Icon Label" --version 9.0.0`.

## Rules

- Always pass --version or set MUDBLAZOR_VERSION.
- Prefer --filter with exact comma-separated names; quote patterns containing * or ?.
- Add --desc only when names are ambiguous.
- Never fetch examples before props.
- Use --format markdown only when the user asks for documentation prose.
- Use --format json only for scripts.
- Never invent Mud* member names.

## Reading compact output

The first line identifies subject, version and command. Member counts are
shown/total before filters. Own members precede inherited groups headed
-- inherited from Type. Properties are Name: Type = Default with optional
[required] and [cascading]; events use EventCallback<Args>; methods are
ReturnType Name(Type argument). --desc adds a short // description.

```text
MudButton v9.0.0 props 3/23
Color: Color = Color.Default
Size: Size = Size.Medium
Variant: Variant = Variant.Text
```

Example bodies start with === Name; --- code separates C# from Razor.
Enums normally fit on one line, with Name=Value for explicit numbers;
--desc prints one value per line instead.

## Exit codes

- 0: done (zero matches is success).
- 2: fix arguments using the hint.
- 3: run the hinted mudmcp index command once.
- 4: use suggested names or mudmcp search.
- 1: rerun mudmcp index; inspect stderr if it still fails.
- 130: cancelled; retry only when intended.

## Commands

Global --version, --data-path and --format apply except to help.
Use mudmcp help <command> for options.

| Command | Purpose |
| --- | --- |
| mudmcp search <query...> | Find components; --in and --limit narrow results. |
| mudmcp list | Browse components; --category and --desc are optional. |
| mudmcp categories | List categories and counts. |
| mudmcp component <name> | Overview; --no-inherited or --no-examples. |
| mudmcp props <name> | Parameters; --filter, --category, --required-only, --no-inherited, --limit, --desc. |
| mudmcp events <name> | Events; --filter, --no-inherited, --limit, --desc. |
| mudmcp methods <name> | Methods; --filter, --no-inherited, --limit, --desc. |
| mudmcp examples <name> | Examples; --names-only, --filter, --limit (default 3). |
| mudmcp example <name> <example-name...> | One named example with code. |
| mudmcp related <name> | Related components; --type selects relationship. |
| mudmcp api <type> | API sections; --members and --desc. |
| mudmcp enum <name> | Enum values; --desc for descriptions. |
| mudmcp index | Build the version's index and split store. |
| mudmcp index-status | Check cached versions; version optional. |
| mudmcp help [command] | Show usage and options. |
