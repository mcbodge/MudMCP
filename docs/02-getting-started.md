# Getting Started

This guide walks you through installing, configuring, and running the Mud MCP server for the first time.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Running the Server](#running-the-server)
- [Verifying the Installation](#verifying-the-installation)
- [Quick Configuration](#quick-configuration)
- [Next Steps](#next-steps)

---

## Prerequisites

### Required Software

| Software | Version | Download |
|----------|---------|----------|
| **.NET SDK** | 10.0 (Preview) | [Download](https://dotnet.microsoft.com/download/dotnet/10.0) |
| **Git** | 2.x+ | [Download](https://git-scm.com/downloads) |

### Verify Installation

```bash
# Check .NET version
dotnet --version
# Expected: 10.0.xxx

# Check Git version
git --version
# Expected: git version 2.x.x
```

### System Requirements

| Resource | Minimum | Recommended |
|----------|---------|-------------|
| **RAM** | 2 GB | 4 GB |
| **Disk Space** | 1 GB | 2 GB (includes cloned repo) |
| **Network** | Internet access to GitHub | - |

> **Note:** The server clones the MudBlazor repository (~500MB) on first startup.

---

## Installation

### Option 1: Clone from GitHub

```bash
# Clone the repository
git clone https://github.com/yourusername/MudBlazor.Mcp.git

# Navigate to the project
cd MudBlazor.Mcp

# Restore dependencies
dotnet restore

# Build the solution
dotnet build
```

### Option 2: Download Release (if available)

```bash
# Download the latest release
# Extract to your desired location
# Run dotnet restore and dotnet build
```

### Verify Build Success

```bash
dotnet build --no-restore
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Running the Server

### HTTP Transport (Default)

The default transport mode starts an HTTP server:

```bash
cd src/MudBlazor.Mcp
dotnet run
```

**What happens on first startup:**
1. The MudBlazor repository is cloned from GitHub (~1-2 minutes)
2. Component index is built (~10-30 seconds)
3. Server starts on `http://localhost:8000`

Local `dotnet run`, Aspire, and Docker examples use HTTP by default. IIS deployment is configured separately: its default `auto` mode uses HTTPS when a certificate thumbprint or existing HTTPS IIS binding is available, and otherwise falls back to HTTP.

**Console output:**
```
info: MudBlazor.Mcp[0]
      Building MudBlazor component index...
info: MudBlazor.Mcp.Services.GitRepositoryService[0]
      Cloning MudBlazor repository from https://github.com/MudBlazor/MudBlazor.git
info: MudBlazor.Mcp.Services.ComponentIndexer[0]
      Index build completed in 15432ms. Indexed 85 components
info: Microsoft.Hosting.Lifetime[0]
      Now listening on: http://localhost:8000
```

### stdio Transport

For CLI-based MCP clients (like Claude Desktop):

```bash
cd src/MudBlazor.Mcp
dotnet run -- --stdio
```

In stdio mode:
- Server reads JSON-RPC messages from stdin
- Server writes responses to stdout
- All logging goes to stderr

### With Aspire Dashboard

For development with observability:

```bash
cd src/MudBlazor.Mcp.AppHost
dotnet run
```

This starts:
- The Mud MCP server
- Aspire dashboard for monitoring (typically `http://localhost:15000`)

---

## Verifying the Installation

### 1. Health Check

```bash
curl http://localhost:8000/health
```

**Healthy response:**
```json
{
  "status": "Healthy",
  "totalDuration": 15.2,
  "checks": [
    {
      "name": "indexer",
      "status": "Healthy",
      "description": "Index contains 85 components in 12 categories.",
      "data": {
        "status": "ready",
        "componentCount": 85,
        "categoryCount": 12,
        "isIndexed": true,
        "lastIndexed": "2025-12-19T10:30:00Z"
      }
    }
  ]
}
```

### 2. List Available Tools

```bash
curl -X POST http://localhost:8000/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc": "2.0", "method": "tools/list", "id": 1}'
```

**Expected response:**
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "tools": [
      {
        "name": "list_components",
        "description": "Lists all available MudBlazor components..."
      },
      // ... 11 more tools
    ]
  }
}
```

### 3. Test a Tool

```bash
curl -X POST http://localhost:8000/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "list_categories",
      "arguments": {}
    },
    "id": 2
  }'
```

**Expected response:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "# MudBlazor Component Categories\n\n## Buttons\n*Interactive button components*\n- **Components:** 5\n\n## Form Inputs & Controls\n..."
      }
    ]
  }
}
```

---

## Quick Configuration

### Environment Variables

Override default settings using environment variables:

```bash
# Change repository branch
export MudBlazor__Repository__Branch=master

# Change local path
export MudBlazor__Repository__LocalPath=/custom/path

# Change log level
export Logging__LogLevel__Default=Debug
```

### appsettings.json

Modify `src/MudBlazor.Mcp/appsettings.json`:

```json
{
  "MudBlazor": {
    "Repository": {
      "Url": "https://github.com/MudBlazor/MudBlazor.git",
      "Branch": "dev",
      "LocalPath": "./data/mudblazor-repo"
    },
    "Cache": {
      "RefreshIntervalMinutes": 60,
      "ComponentCacheDurationMinutes": 30,
      "ExampleCacheDurationMinutes": 120
    },
    "Parsing": {
      "IncludeInternalComponents": false,
      "IncludeDeprecatedComponents": true,
      "MaxExamplesPerComponent": 20
    }
  }
}
```

### Changing the Port

Edit `Properties/launchSettings.json` or use:

```bash
dotnet run --urls "http://localhost:8080"
```

---

## Quick Start: First AI Interaction

### With VS Code and GitHub Copilot

1. **Create configuration file**

   Create `.vscode/mcp.json` in your workspace:
   ```json
   {
     "servers": {
       "mudblazor": {
         "url": "http://localhost:8000/mcp"
       }
     }
   }
   ```

2. **Start the Mud MCP server** (if not already running)

3. **Ask Copilot about MudBlazor**

   In a Blazor file, try asking:
   - *"What parameters does MudButton support?"*
   - *"Show me an example of MudDataGrid"*
   - *"List all navigation components"*

### With Claude Desktop

Claude Desktop supports two integration modes. Choose the one that fits your setup.

#### Option A: HTTP Transport via `mcp-proxy` (recommended for local server)

Use this when the Mud MCP server is already running locally (HTTP transport). `mcp-proxy` bridges Claude Desktop to the HTTP endpoint.

1. **Install `uv`** (if not already installed)

   Open PowerShell and run:
   ```powershell
   powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
   ```

2. **Install `mcp-proxy`** via `uv` (if not already installed)

   ```powershell
   uv tool install mcp-proxy --system-certs
   ```

   The `--system-certs` flag ensures your corporate or self-signed certificates are trusted during installation.

3. **Edit Claude Desktop configuration**

   Location: `%APPDATA%\Claude\claude_desktop_config.json` (Windows)

   Add the following entry inside `"mcpServers"`:
   ```json
   {
     "mcpServers": {
       "mud-mcp": {
         "command": "mcp-proxy",
         "args": ["--transport", "streamablehttp", "--no-verify-ssl", "https://localhost:8000/mcp"]
       }
     }
   }
   ```

   > **Note:** `--no-verify-ssl` skips TLS certificate validation for `localhost`. This is safe for local development but should not be used against remote servers.

4. **Restart Claude Desktop**

5. **Start chatting** about MudBlazor components

#### Option B: stdio Transport via `dotnet run`

Use this when you want Claude Desktop to launch the server process itself using the stdio transport. No separate server process or `mcp-proxy` is required.

1. **Edit Claude Desktop configuration**

   Location: `%APPDATA%\Claude\claude_desktop_config.json` (Windows)

   ```json
   {
     "mcpServers": {
       "mud-mcp": {
         "command": "dotnet",
         "args": ["run", "--project", "C:\\path\\to\\MudBlazor.Mcp\\src\\MudBlazor.Mcp", "--", "--stdio", "--version", "9.0.0"]
       }
     }
   }
   ```

   Replace `C:\\path\\to\\MudBlazor.Mcp` with the actual path to your cloned repository, and replace `9.0.0` with your project's MudBlazor package version.

2. **Restart Claude Desktop**

3. **Start chatting** about MudBlazor components

---

## Common First-Run Issues

### Issue: Repository Clone Fails

**Symptoms:** Server fails to start, Git errors in console

**Solutions:**
```bash
# Check Git is installed
git --version

# Check network connectivity
curl https://github.com

# Try manual clone
git clone https://github.com/MudBlazor/MudBlazor.git ./data/mudblazor-repo
```

### Issue: Port Already in Use

**Symptoms:** `Address already in use` error

**Solutions:**
```bash
# Find process using port 8000
netstat -ano | findstr :8000  # Windows
lsof -i :8000                  # macOS/Linux

# Kill the process or use different port
dotnet run --urls "http://localhost:5181"
```

### Issue: Index Never Completes

**Symptoms:** Health check shows "building" forever

**Solutions:**
1. Check available disk space
2. Verify the cloned repository exists in `./data/mudblazor-repo`
3. Check console logs for parsing errors
4. Restart the server

---

## Next Steps

Now that your server is running:

1. **[Configure your IDE](./09-ide-integration.md)** — Set up VS Code, Visual Studio, or Claude Desktop
2. **[Explore the Tools](./05-tools-reference.md)** — Learn about all 12 available MCP tools
3. **[Test with MCP Inspector](./08-mcp-inspector.md)** — Interactive tool testing
4. **[Deep dive into Architecture](./03-architecture.md)** — Understand how it works

---

## Development Mode

For active development:

```bash
# Run with hot reload
dotnet watch run --project src/MudBlazor.Mcp

# Run tests continuously
dotnet watch test --project tests/MudBlazor.Mcp.Tests

# Run with Aspire for full observability
cd src/MudBlazor.Mcp.AppHost && dotnet run
```

### Useful Development Endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Overall health status |
| `GET /health/ready` | Readiness probe (index built) |
| `GET /health/live` | Liveness probe |
| `POST /mcp` | MCP JSON-RPC endpoint |
