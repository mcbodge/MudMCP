# IDE Integration

Configure Mud MCP for use with VS Code, Claude Desktop, and other AI assistants.

## Table of Contents

- [Overview](#overview)
- [Launching via dnx](#launching-via-dnx)
- [VS Code with GitHub Copilot](#vs-code-with-github-copilot)
- [Claude Desktop](#claude-desktop)
- [Continue.dev](#continuedev)
- [Other Clients](#other-clients)
- [Transport Options](#transport-options)
- [Troubleshooting](#troubleshooting)

---

## Overview

Mud MCP supports two transport mechanisms:

| Transport | Use Case | Endpoint |
|-----------|----------|----------|
| **stdio** | Local CLI clients (recommended) | stdin/stdout |
| **HTTP** | Web-based clients, remote access | `http://localhost:8000/mcp` |

The HTTP URL shown here is the local `dotnet run` default. IIS deployments use the protocol resolved during deployment: `auto` selects HTTPS when a certificate thumbprint or existing HTTPS IIS binding is available, and otherwise uses HTTP.

---

## Launching via dnx

`dnx` (bundled with the **.NET 10 SDK**) downloads and runs a tool package on demand — no global install and no manually cloned repo. This project is published to nuget.org as the .NET tool **[`MudDocs`](https://www.nuget.org/packages/MudDocs)** (command `muddocs`), so every client below can launch it the same way.

### Prerequisites

- **.NET 10 SDK** (provides the `dnx` command).
- Nothing else — `dnx` restores **`MudDocs`** from nuget.org on first run.

> **Building from source instead of nuget.org?** Pack it locally (`dotnet pack src/MudDocs/MudDocs.csproj -c Release -o ./nupkg`) and add `"--source", "<absolute-path>/nupkg"` to the `args` below. Use an **absolute** path — each client sets its own working directory, so a relative `./nupkg` will not resolve.

### Key differences from the `dotnet run` configs

- The docs version is passed through the **`MUDBLAZOR_VERSION`** environment variable, because `dnx` reserves `--version` for the *package* version.
- Everything after `--` (here `--stdio`) is forwarded to the server. You may append `"--version", "9.0.0"` there instead of the env var.
- **Shared cache (recommended):** `dnx` runs from the client's working directory, so by default each project clones MudBlazor (~500 MB) into its own `./data`. Set **`MudBlazor__Repository__DataPath`** to one fixed folder (as in the configs below) so every project shares a single cache. VS Code expands `${userHome}`; other clients need a literal absolute path.

### VS Code — `.vscode/mcp.json`

```json
{
  "servers": {
    "mudblazor": {
      "command": "dnx",
      "args": ["MudDocs@1.0.2", "--yes", "--", "--stdio"],
      "env": {
        "MUDBLAZOR_VERSION": "9.0.0",
        "MudBlazor__Repository__DataPath": "${userHome}/.muddocs"
      }
    }
  }
}
```

### Claude Desktop — `claude_desktop_config.json`

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "dnx",
      "args": ["MudDocs@1.0.2", "--yes", "--", "--stdio"],
      "env": {
        "MUDBLAZOR_VERSION": "9.0.0",
        "MudBlazor__Repository__DataPath": "C:/Users/you/.muddocs"
      }
    }
  }
}
```

### Continue.dev — `~/.continue/config.json`

```json
{
  "experimental": {
    "mcpServers": {
      "mudblazor": {
        "transport": {
          "type": "stdio",
          "command": "dnx",
          "args": ["MudDocs@1.0.2", "--yes", "--", "--stdio"]
        },
        "env": {
          "MUDBLAZOR_VERSION": "9.0.0",
          "MudBlazor__Repository__DataPath": "C:/Users/you/.muddocs"
        }
      }
    }
  }
}
```

---

## VS Code with GitHub Copilot

### Prerequisites

- VS Code 1.99+ (or Insiders)
- GitHub Copilot extension
- GitHub Copilot Chat extension

### Configuration

1. Open VS Code settings (JSON):
   - Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on macOS)
   - Type "Preferences: Open User Settings (JSON)"

2. Add MCP server configuration:

```json
{
  "github.copilot.chat.experimental.mcpServers": {
    "mudblazor": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/MudBlazor/Mcp/MudDocs/src/MudDocs",
        "--",
        "--stdio"
      ]
    }
  }
}
```

### Alternative: Workspace Configuration

Create `.vscode/mcp.json` in your project:

```json
{
  "servers": {
    "mudblazor": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/MudDocs",
        "--",
        "--stdio"
      ]
    }
  }
}
```

### Using Pre-built Binary

For faster startup, use a published executable:

```bash
# Build the executable
cd src/MudDocs
dotnet publish -c Release -o ./publish
```

```json
{
  "github.copilot.chat.experimental.mcpServers": {
    "mudblazor": {
      "command": "C:/MudBlazor/Mcp/MudDocs/src/MudDocs/publish/MudDocs.exe",
      "args": ["--stdio"]
    }
  }
}
```

### Verification

1. Open GitHub Copilot Chat
2. Type: `@mudblazor list components`
3. Verify MudBlazor tools are available in tool picker

### Recommended Copilot Agents (Clowder)

Drive this server with the **[Clowder](https://github.com/mcbodge/clowder)** MudBlazor agents, which query the `mudblazor/*` MCP tools effectively:

- **[CAT MudBlazor Engineer](https://github.com/mcbodge/clowder/blob/main/agents/cat-mudblazor-engineer.agent.md)** — Blazor Server + MudBlazor UI, every component API confirmed through this server.
- **[CAT Planner (MudBlazor)](https://github.com/mcbodge/clowder/blob/main/agents/cat-planner-mudblazor.agent.md)** and **[CAT Planner (DevOps, MudBlazor)](https://github.com/mcbodge/clowder/blob/main/agents/cat-planner-devops-mudblazor.agent.md)** — plan MudBlazor work, researching APIs here first.
- **[CAT Plan Executor (MudBlazor)](https://github.com/mcbodge/clowder/blob/main/agents/cat-plan-executor-mudblazor.agent.md)** — executes those plans, consulting this server before writing MudBlazor markup.

**Setup:**

1. Configure this server under the name `mudblazor` in your MCP client (see [VS Code with GitHub Copilot](#vs-code-with-github-copilot) above).
2. Install the agents from [github.com/mcbodge/clowder](https://github.com/mcbodge/clowder) into `.github/agents/` or your user profile.
3. Select an agent in Copilot Chat; it queries the live documentation through `mudblazor/*`.

---

## Claude Desktop

### Configuration File Location

| OS | Path |
|----|------|
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Linux | `~/.config/claude/claude_desktop_config.json` |

### Windows Configuration

Create or edit `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\MudBlazor\\Mcp\\MudDocs\\src\\MudDocs",
        "--",
        "--stdio"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

### macOS/Linux Configuration

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/MudDocs/src/MudDocs",
        "--",
        "--stdio"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

### Using Published Binary (Recommended)

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "C:\\MudBlazor\\Mcp\\MudDocs\\src\\MudDocs\\publish\\MudDocs.exe",
      "args": ["--stdio"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production"
      }
    }
  }
}
```

### Verification

1. Restart Claude Desktop
2. Open a new conversation
3. Ask: "What MudBlazor tools are available?"
4. Claude should list the 12 available tools

---

## Continue.dev

### Configuration

Edit `~/.continue/config.json`:

```json
{
  "experimental": {
    "mcpServers": {
      "mudblazor": {
        "transport": {
          "type": "stdio",
          "command": "dotnet",
          "args": [
            "run",
            "--project",
            "C:/MudBlazor/Mcp/MudDocs/src/MudDocs",
            "--",
            "--stdio"
          ]
        }
      }
    }
  }
}
```

---

## Other Clients

### Generic stdio Client

Any MCP client supporting stdio transport can connect:

```bash
# Start server in stdio mode
dotnet run --project src/MudDocs -- --stdio
```

### Generic HTTP Client

For clients supporting HTTP transport:

```bash
# Start server in HTTP mode (default)
dotnet run --project src/MudDocs

# Server available at: http://localhost:8000/mcp
```

### Custom Client Integration

For programmatic integration:

```csharp
// Using ModelContextProtocol.Client package
var client = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new StdioClientTransportOptions
    {
        Command = "dotnet",
        Arguments = ["run", "--project", "path/to/MudDocs", "--", "--stdio"]
    }));

// List tools
var tools = await client.ListToolsAsync();

// Call a tool
var result = await client.CallToolAsync("get_component_detail", new
{
    componentName = "MudButton",
    includeExamples = true
});
```

---

## Transport Options

### stdio vs HTTP

| Aspect | stdio | HTTP |
|--------|-------|------|
| **Latency** | Lower (direct pipe) | Higher (HTTP overhead) |
| **Security** | Process isolation | Network exposure |
| **Setup** | Simpler | Requires port config |
| **Remote** | Local only | Supports remote |
| **Debug** | Harder | Easier (curl, browser) |

### stdio Transport Details

- Server starts fresh per client session
- All communication via stdin/stdout
- Logs go to stderr (MCP requirement)
- Recommended for local development

### HTTP Transport Details

- Long-running server instance
- Multiple clients can connect
- Health checks available: `/health`
- SSE endpoint: `/sse` (Server-Sent Events)

---

## Advanced Configuration

### Environment Variables

Pass environment variables to configure the server:

**VS Code:**
```json
{
  "github.copilot.chat.experimental.mcpServers": {
    "mudblazor": {
      "command": "dotnet",
      "args": ["run", "--project", "...", "--", "--stdio"],
      "env": {
        "DOTNET_ENVIRONMENT": "Development",
        "MudBlazor__Repository__Branch": "master",
        "Logging__LogLevel__Default": "Debug"
      }
    }
  }
}
```

**Claude Desktop:**
```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "...",
      "args": ["--stdio"],
      "env": {
        "DOTNET_ENVIRONMENT": "Production",
        "MudBlazor__Cache__RefreshIntervalMinutes": "120"
      }
    }
  }
}
```

### Working Directory

Set the working directory for the server:

```json
{
  "mcpServers": {
    "mudblazor": {
      "command": "dotnet",
      "args": ["run", "--", "--stdio"],
      "cwd": "C:\\MudBlazor\\Mcp\\MudDocs\\src\\MudDocs"
    }
  }
}
```

### Multiple Configurations

Run different configurations for different projects:

```json
{
  "mcpServers": {
    "mudblazor-dev": {
      "command": "dotnet",
      "args": ["run", "--project", "...", "--", "--stdio"],
      "env": {
        "MudBlazor__Repository__Branch": "dev"
      }
    },
    "mudblazor-stable": {
      "command": "dotnet",
      "args": ["run", "--project", "...", "--", "--stdio"],
      "env": {
        "MudBlazor__Repository__Branch": "master"
      }
    }
  }
}
```

---

## Troubleshooting

### Server Not Starting

**Check logs:**
```bash
# Run manually to see errors
dotnet run --project src/MudDocs -- --stdio 2>&1
```

**Verify .NET SDK:**
```bash
dotnet --list-sdks
# Should show 10.0.xxx
```

### Tools Not Appearing

1. Verify server is running (check task manager)
2. Check IDE logs for MCP errors
3. Restart IDE after config changes
4. Ensure JSON syntax is valid

### Slow First Response

First request may take 30-60 seconds due to:
1. Repository cloning (first run)
2. Index building

**Solutions:**
- Pre-warm: Run server once before configuring IDE
- Use published binary instead of `dotnet run`

### Connection Issues

**stdio:**
```bash
# Test manually
echo '{"jsonrpc":"2.0","method":"initialize","params":{},"id":1}' | dotnet run -- --stdio
```

**HTTP:**
```bash
# Health check
curl http://localhost:8000/health

# MCP endpoint
curl -X POST http://localhost:8000/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/list","id":1}'
```

### Path Issues on Windows

Use forward slashes or escaped backslashes:
```json
// ✅ Forward slashes
"args": ["run", "--project", "C:/Path/To/Project"]

// ✅ Escaped backslashes
"args": ["run", "--project", "C:\\Path\\To\\Project"]

// ❌ Unescaped backslashes (invalid JSON)
"args": ["run", "--project", "C:\Path\To\Project"]
```

### Permission Issues

**Windows:** Run as Administrator for first clone
**macOS/Linux:** Check directory permissions:
```bash
chmod -R 755 ./data/mudblazor-repo
```

---

## Quick Reference

### VS Code Settings Path

| OS | Path |
|----|------|
| Windows | `%APPDATA%\Code\User\settings.json` |
| macOS | `~/Library/Application Support/Code/User/settings.json` |
| Linux | `~/.config/Code/User/settings.json` |

### Claude Desktop Config Path

| OS | Path |
|----|------|
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |
| Linux | `~/.config/claude/claude_desktop_config.json` |

### Common Commands

```bash
# Build executable
dotnet publish -c Release -o ./publish

# Run with stdio
dotnet run -- --stdio

# Run with HTTP
dotnet run

# Run with custom config
DOTNET_ENVIRONMENT=Development dotnet run -- --stdio
```

---

## Next Steps

- [Troubleshooting](./10-troubleshooting.md) — Common issues and solutions
- [MCP Inspector](./08-mcp-inspector.md) — Debug tool interactions
- [Configuration](./06-configuration.md) — Server configuration options
