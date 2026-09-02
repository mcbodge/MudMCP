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
- [Quick Start](#quick-start) — for consumers (dnx, no install)
- [Running from Source](#running-from-source-developers) — for contributors
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

The fastest way to use Mud MCP — **no clone, no build, no global install**. [`dnx`](https://learn.microsoft.com/dotnet/core/tools/dotnet-tool-exec) (bundled with the **.NET 10 SDK**) downloads and runs the published **[`MudMCP`](https://www.nuget.org/packages/MudMCP)** tool on demand, so your AI assistant launches it directly.

> Want to run from a local clone to contribute or customize the server? See [Running from Source](#running-from-source-developers).

### Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** — provides the `dnx` command. Nothing else to install; `dnx` restores `MudMCP` from nuget.org on first run.

### 1. Find your MudBlazor version

Check your project's `.csproj` for the MudBlazor version — the server serves documentation for this exact version:

```xml
<PackageReference Include="MudBlazor" Version="9.0.0" />
```

### 2. Add Mud MCP to your AI assistant

Add the snippet below to your MCP client configuration, replacing `9.0.0` with your version from step 1. A pinned example is provided as [`mcp.dnx.json`](./mcp.dnx.json) in the repo root.

**VS Code — `.vscode/mcp.json`:**
```json
{
  "servers": {
    "mudblazor": {
      "command": "dnx",
      "args": ["MudMCP", "--yes", "--", "--stdio"],
      "env": {
        "MUDBLAZOR_VERSION": "9.0.0",
        "MudBlazor__Repository__DataPath": "${userHome}/.mudmcp"
      }
    }
  }
}
```

**Claude Desktop / Cursor — `claude_desktop_config.json` / `.cursor/mcp.json`:**
```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "dnx",
      "args": ["MudMCP", "--yes", "--", "--stdio"],
      "env": {
        "MUDBLAZOR_VERSION": "9.0.0",
        "MudBlazor__Repository__DataPath": "C:/Users/<you>/.mudmcp"
      }
    }
  }
}
```

Restart your assistant and ask it something like *"List all MudBlazor button components"*.

> The first run per version clones the MudBlazor repository (~500 MB) and builds the index, so it takes a little longer. Subsequent runs load from a cached `index.json` and start instantly.

### Notes

- **Latest vs pinned version:** `MudMCP` (no suffix) always fetches the **latest** published tool. For a reproducible setup, pin a release by appending `@<version>` — e.g. `"args": ["MudMCP@1.0.2", "--yes", "--", "--stdio"]`. Browse released versions on [nuget.org](https://www.nuget.org/packages/MudMCP).
- **`MUDBLAZOR_VERSION` vs `--version`:** `dnx` reserves its own `--version` flag for the *package* version, so the MudBlazor docs version is supplied through the `MUDBLAZOR_VERSION` environment variable. Everything after `--` is forwarded to the server, so you may append `"--version", "9.0.0"` there instead of the env var.
- **`MudBlazor__Repository__DataPath` (shared cache):** where cloned repos and indexes are stored. Point every project at one fixed folder so they share a single cache instead of each cloning ~500 MB into its own working-directory `./data`. Omit it to use `./data` relative to the client's working directory.
- **What to use for `${userHome}`:** VS Code expands `${userHome}` to your home directory automatically — `C:\Users\<you>` (Windows), `/home/<you>` (Linux), `/Users/<you>` (macOS). Clients that don't expand variables (Claude Desktop, Cursor) need a **literal absolute path** instead, e.g. `C:/Users/<you>/.mudmcp` or `/home/<you>/.mudmcp`.

---

## Running from Source (Developers)

Prefer to run the server from a local clone — to contribute, debug, or customize it? Build it yourself and point your MCP client at the local build. The server communicates over stdin/stdout (the native mode for Cursor, Claude Code, Claude Desktop, and most MCP clients) or over HTTP.

### Prerequisites

- **[.NET 10 SDK](https://dotnet.microsoft.com/download)**
- **[Git](https://git-scm.com/)** — to clone this repository.

### Clone and build

```bash
git clone https://github.com/mcbodge/MudMCP.git
cd MudMCP
dotnet build
```

> **Important:** The `--version` argument (or the `MUDBLAZOR_VERSION` env var) is required in every transport below. It must match the MudBlazor version in your project's `.csproj` file (e.g., `<PackageReference Include="MudBlazor" Version="9.0.0" />`).

### Run the HTTP server

```bash
dotnet run --project src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -- --version 9.0.0
```

The server clones the MudBlazor repository at tag `v9.0.0`, parses it with Roslyn, builds the index (cached to disk for instant subsequent starts), and listens on `http://localhost:8000`. Verify with `curl http://localhost:8000/health`, then connect an HTTP client:

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
| `dnx` | `dnx MudMCP --yes -- --stdio` (env `MUDBLAZOR_VERSION`) | No | **Recommended** — one-off, no install (needs .NET 10 SDK) |
| `--stdio` | `dotnet run -- --stdio --version X.Y.Z` or `.exe --stdio --version X.Y.Z` | No | Running from source: Cursor, Claude Code, Claude Desktop |
| HTTP (default) | `dotnet run -- --version X.Y.Z` | Yes (`:8000`) | VS Code HTTP, MCP Inspector, remote |
| Docker | `docker compose up` | Yes (`:8000→8080`) | Containerised / persistent cache |

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
