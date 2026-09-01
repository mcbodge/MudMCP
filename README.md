# Mud MCP

An enterprise-grade Model Context Protocol (MCP) server that provides AI assistants with comprehensive access to MudBlazor component documentation, code examples, and API reference.

[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
[![MCP Protocol](https://img.shields.io/badge/MCP-Protocol-blue)](https://modelcontextprotocol.io/)
[![License: GPL-2.0](https://img.shields.io/badge/License-GPL%202.0-green.svg)](LICENSE)

> **Disclaimer:** This project is not affiliated with, endorsed by, or officially supported by the MudBlazor team. It is an independent implementation that extracts and serves documentation from the official MudBlazor repository.

---

## 📖 Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Quick Start](#quick-start)
- [Documentation](#documentation)
- [Available MCP Tools](#available-mcp-tools)
- [Project Structure](#project-structure)
- [Contributing](#contributing)
- [License](#license)

---

## Overview

Mud MCP bridges the gap between AI assistants and MudBlazor component documentation. It clones the official MudBlazor repository, parses source files using Roslyn, and exposes an indexed API via the Model Context Protocol—enabling AI agents like GitHub Copilot, Claude, and other MCP-compatible clients to provide accurate, context-aware assistance for Blazor development.

### Key Value Propositions

- **Version-Aware**: Serves documentation for the exact MudBlazor version your project uses
- **AI-Optimized Output**: Formats responses in Markdown for optimal LLM consumption
- **Production-Ready**: Built with Aspire 13.1, health checks, and observability
- **Flexible Deployment**: Supports both HTTP and stdio transports
- **Multi-Version Cache**: Caches up to 3 versions simultaneously with LRU eviction — instant startup after first run

---

## Features

| Feature | Description |
|---------|-------------|
| **Component Discovery** | List all ~85 MudBlazor components with category filtering |
| **Detailed Documentation** | Access parameters, events, methods, and inheritance info |
| **Code Examples** | Extract real examples from the MudBlazor documentation |
| **Semantic Search** | Search components by name, description, or parameters |
| **API Reference** | Full API reference for components and enum types |
| **Related Components** | Discover related components through inheritance and categories |
| **Health Monitoring** | Built-in health checks with detailed status reporting |
| **Expert Agent** | Pre-built agent file for optimal MCP tool usage with GitHub Copilot |

---

## MudBlazor Expert Agent

To maximize the value of the MCP server, this project includes a specialized GitHub Copilot agent file:

**Location:** `.github/agents/mudblazor-expert.agent.md`

The agent file teaches GitHub Copilot how to effectively use the MudBlazor MCP tools by providing:

- **Decision Logic**: Automatically selects the right MCP tool for each query
- **Best Practices**: Enforces "query before answering" to prevent hallucination
- **Blazor Guidelines**: Includes component architecture and rendering optimization patterns
- **Tool Chaining**: Combines multiple tools for comprehensive answers

**Example workflow:**
```
User: "How do I create a form with validation?"

Agent:
1. search_components("form input validation") → Find relevant components
2. get_component_detail("MudForm") → Get parameters and events
3. get_component_examples("MudTextField", filter="validation") → Get code examples
4. Provide complete, accurate answer with working code
```

> **Credits:** This agent file is derived from work in the [github/awesome-copilot](https://github.com/github/awesome-copilot) repository.

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Git](https://git-scm.com/)

### 1. Clone and Build

```bash
dotnet build
```

### 2. Find Your MudBlazor Version

Check your project's `.csproj` file for the MudBlazor version:

```xml
<PackageReference Include="MudBlazor" Version="9.0.0" />
```

### 3. Run the Server

The `--version` argument is **required** and must match your project's MudBlazor version:

```bash
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- --version 9.0.0
```

The server will:
1. Clone the MudBlazor repository and checkout the matching tag (`v9.0.0`)
2. Parse all components using Roslyn and build an index
3. Cache the index to disk — subsequent runs load instantly
4. Start the MCP server on `http://localhost:8000`

### 4. Verify

```bash
curl http://localhost:8000/health
```

### 5. Connect Your AI Assistant

**VS Code / Cursor (HTTP mode, mcp.json):**
```json
{
  "servers": {
    "mudblazor": {
      "type": "http",
      "url": "http://localhost:8000/mcp"
    }
  }
}
```

---

## Local MCP (stdio — no frontend required)

Run the MCP server locally without starting a web server or the Aspire dashboard. The server communicates directly through stdin/stdout, which is the native mode for Cursor, Claude Code, Claude Desktop, and most MCP clients.

> **Important:** The `--version` argument is required. It must match the MudBlazor version in your project's `.csproj` file (e.g., `<PackageReference Include="MudBlazor" Version="9.0.0" />`).

### Option A — dotnet run (development)

Add this to your project's `.mcp.json` (or `.cursor/mcp.json`, `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "<path-to-MudMCP>/src/MudBlazor.Mcp/MudBlazor.Mcp.csproj",
        "--",
        "--stdio",
        "--version",
        "9.0.0"
      ]
    }
  }
}
```

Replace `<path-to-MudMCP>` with the absolute path to where you cloned this repository, and `9.0.0` with your project's MudBlazor version.

> The first run per version takes longer because it clones the MudBlazor repository and builds the index. Subsequent runs load from a cached `index.json` and start instantly.

### Option B — Self-contained executable (recommended for daily use)

Publish a single-file executable that starts instantly without the .NET SDK:

```powershell
dotnet publish src/MudBlazor.Mcp/MudBlazor.Mcp.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish/win-x64
```

Then use this as your MCP configuration:

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "<path-to-MudMCP>/publish/win-x64/MudBlazor.Mcp.exe",
      "args": ["--stdio", "--version", "9.0.0"]
    }
  }
}
```

Replace `<path-to-MudMCP>` with the absolute path to where you cloned this repository, and `9.0.0` with your project's MudBlazor version.

### Option C — Docker (HTTP mode, persistent cache)

Run the server in a container with built-in health checks and a named volume that persists the cloned MudBlazor repository across restarts.

**Prerequisites:** [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or Docker Engine + Compose plugin)

```bash
# Build the image and start the container
docker compose up --build -d

# Follow startup logs (first run clones ~500 MB — takes a few minutes)
docker compose logs -f

# Check health
curl http://localhost:8000/health
```

The MCP endpoint is available at `http://localhost:8000/mcp`. If an existing MCP client config points to the previous `:5180` default, update it to `:8000`.

**Volume:** All cached data is stored under a named Docker volume (`mudblazor-data`) mounted at `/app/data`. Each MudBlazor version gets its own subdirectory (`/app/data/v{Version}/`) containing the git clone and serialized index (`index.json`). The version manifest (`versions.json`) lives at `/app/data/versions.json`. Because tagged commits are immutable, the server does not run `git fetch` on subsequent starts — it simply reuses the existing clone and loads the pre-built `index.json`.

```bash
# Stop without removing the volume (cache is preserved)
docker compose down

# Stop AND delete all cached data (forces a full re-clone and re-index next start)
docker compose down -v
```

**Connect your AI assistant** — same config as HTTP mode:
```json
{
  "servers": {
    "mudblazor": {
      "type": "http",
      "url": "http://localhost:8000/mcp"
    }
  }
}
```

### Option D — dnx (one-off, no install)

[`dnx`](https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-exec) ships with the .NET 10 SDK and downloads + runs a tool package on demand — no global install and no manually cloned repo. This project is published to nuget.org as the .NET tool **[`MudMCP`](https://www.nuget.org/packages/MudMCP)** (command `mudmcp`).

```bash
# MUDBLAZOR_VERSION selects the docs version to serve
#    PowerShell:
$env:MUDBLAZOR_VERSION = "9.0.0"; dnx MudMCP@1.0.1 --yes -- --stdio
#    bash:
MUDBLAZOR_VERSION=9.0.0 dnx MudMCP@1.0.1 --yes -- --stdio
```

MCP client configuration (also provided as `mcp.dnx.json` in the repo root):

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "dnx",
      "args": ["MudMCP@1.0.1", "--yes", "--", "--stdio"],
      "env": { "MUDBLAZOR_VERSION": "9.0.0" }
    }
  }
}
```

> **Building from source instead?** Pack locally and point `dnx` at it: `dotnet pack src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -c Release -o ./nupkg`, then `dnx MudMCP --source ./nupkg --yes -- --stdio` (use an absolute `--source` path in MCP client configs).
>
> **Why the `--` and the env var?** `dnx` has its own `--version` flag (the *package* version), so the MudBlazor docs version is supplied via the `MUDBLAZOR_VERSION` environment variable to avoid a collision. Everything after `--` (here `--stdio`) is forwarded to the server — you can append `--version 9.0.0` there instead of the env var if you prefer.
>
> **Cache location:** `dnx` runs from the current working directory, so the ~500 MB clone lands in `./data` relative to it. Set `MudBlazor__Repository__DataPath` to a fixed absolute path to share one cache across projects.

### Version Caching

The server caches up to **3 MudBlazor versions** simultaneously. Each version gets its own git clone and serialized index:

```
data/
  versions.json          # tracks cached versions + last-used timestamps
  v8.15.0/
    mudblazor-repo/      # git clone at tag v8.15.0
    index.json           # serialized component index
  v9.0.0/
    mudblazor-repo/
    index.json
```

When a 4th version is requested, the least recently used version is evicted automatically. This means you can work on multiple projects with different MudBlazor versions — each project gets its own `.mcp.json` with the right `--version`, and they share the cached clones.

---

### Transport comparison

| Mode | Command | Kestrel | Use case |
|------|---------|---------|----------|
| `--stdio` | `dotnet run -- --stdio --version X.Y.Z` or `.exe --stdio --version X.Y.Z` | No | Cursor, Claude Code, Claude Desktop, local clients |
| HTTP (default) | `dotnet run -- --version X.Y.Z` | Yes (`:8000`) | VS Code HTTP, MCP Inspector, remote |
| Docker | `docker compose up` | Yes (`:8000→8080`) | Containerised / persistent cache |
| `dnx` | `dnx MudMCP --yes -- --stdio` (env `MUDBLAZOR_VERSION`) | No | One-off, no install (needs .NET 10 SDK) |

---

## Documentation

For comprehensive documentation, see the [docs](./docs/) folder:

| Document | Description |
|----------|-------------|
| [Overview](./docs/01-overview.md) | Architecture, design principles, and system overview |
| [Getting Started](./docs/02-getting-started.md) | Installation, prerequisites, and first run |
| [Architecture](./docs/03-architecture.md) | Technical architecture and component design |
| [Best Practices](./docs/04-best-practices.md) | Implemented patterns and practices |
| [Tools Reference](./docs/05-tools-reference.md) | Complete reference for all 12 MCP tools |
| [Configuration](./docs/06-configuration.md) | Configuration options and environment setup |
| [Testing](./docs/07-testing.md) | Unit testing strategy and examples |
| [MCP Inspector](./docs/08-mcp-inspector.md) | Testing with MCP Inspector tool |
| [IDE Integration](./docs/09-ide-integration.md) | VS Code, Visual Studio, and Claude Desktop setup |
| [Troubleshooting](./docs/10-troubleshooting.md) | Common issues and solutions |
| [Changelog](./docs/CHANGELOG.md) | Version history and release notes |

---

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `list_components` | Lists all MudBlazor components with optional category filter |
| `list_categories` | Lists all component categories with descriptions |
| `get_component_detail` | Gets comprehensive details about a specific component |
| `get_component_parameters` | Gets all parameters for a component |
| `get_component_examples` | Gets code examples for a component |
| `get_example_by_name` | Gets a specific example by name |
| `list_component_examples` | Lists all example names for a component |
| `search_components` | Searches components by query |
| `get_components_by_category` | Gets all components in a specific category |
| `get_related_components` | Gets components related to a specific component |
| `get_api_reference` | Gets full API reference for a type |
| `get_enum_values` | Gets all values for a MudBlazor enum |

**Example Interaction:**

Ask your AI assistant:
- *"List all MudBlazor button components"*
- *"Show me how to use MudTextField with validation"*
- *"What parameters does MudDataGrid support?"*
- *"What are the available Color enum values?"*

---

## Project Structure

```
MudBlazor.Mcp/
├── .github/
│   └── agents/
│       └── mudblazor-expert.agent.md  # GitHub Copilot agent file
├── src/
│   ├── MudBlazor.Mcp/              # Main MCP server
│   │   ├── Configuration/          # Strongly-typed options
│   │   ├── Models/                 # Domain models (immutable records)
│   │   ├── Services/               # Core services
│   │   │   └── Parsing/            # Roslyn-based parsers
│   │   └── Tools/                  # MCP tool implementations
│   ├── MudBlazor.Mcp.AppHost/      # Aspire orchestration
│   └── MudBlazor.Mcp.ServiceDefaults/  # Shared service configuration
├── tests/
│   └── MudBlazor.Mcp.Tests/        # Unit tests
├── docs/                           # Documentation
└── README.md
```

---

## Contributing

Contributions are welcome! Please see the [Contributing Guide](./docs/01-overview.md#contributing) for details.

### Quick Contribution Steps

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## License

This project is licensed under the **GNU General Public License v2.0 (GPL-2.0)** in compliance with MudBlazor's licensing.

- Source code is provided under GPL-2.0
- Original copyright notices are retained
- Modifications are documented

See the [LICENSE](LICENSE) file for full details.

---

## Acknowledgments

- [MudBlazor](https://mudblazor.com/) — The excellent Blazor component library
- [Model Context Protocol](https://modelcontextprotocol.io/) — The protocol specification
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) — Cloud-native orchestration
- [Roslyn](https://github.com/dotnet/roslyn) — The .NET Compiler Platform
- [github/awesome-copilot](https://github.com/github/awesome-copilot) — Inspiration for the expert agent file

---

<p align="center">
  Built with ❤️ for the Blazor community
</p>
