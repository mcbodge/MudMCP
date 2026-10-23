---
name: mudblazor-docs
description: 'Look up version-accurate MudBlazor parameters, events, methods, enums, and examples with muddocs CLI when writing .razor markup.'
---
# MudBlazor Docs Cheat Sheet
## Rules
1. Query narrow metadata (props, events, methods with --filter) before fetching full examples.
2. Rely on default compact output format unless requested otherwise.
3. Use --help only if an edge-case flag is needed.

## Setup & Global Options
- Install & index: dotnet tool install -g MudDocs && muddocs index --version 9.0.0
- Flags: --version <ver> | --data-path <dir> | --format compact|markdown|json

## Command Templates
- muddocs search <query...> [--in name|param|desc] [--limit N]
- muddocs list [--category <cat>] [--desc]
- muddocs categories
- muddocs component <name> [--no-inherited] [--no-examples]
- muddocs props <name> [--filter <names>] [--category <cat>] [--required-only] [--no-inherited] [--limit N] [--desc]
- muddocs events <name> [--filter <names>] [--no-inherited] [--limit N] [--desc]
- muddocs methods <name> [--filter <names>] [--no-inherited] [--limit N] [--desc]
- muddocs examples <name> [--names-only] [--filter <names>] [--limit N]
- muddocs example <name> <example-name...>
- muddocs related <name> [--type child|parent|see-also]
- muddocs api <type> [--members <members>] [--desc]
- muddocs enum <name> [--desc]
- muddocs index | muddocs index-status
