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

`dnx` (bundled with the **.NET 10 SDK**) downloads and runs a tool package on demand — no global install and no manually cloned repo. This project is packaged as the .NET tool **`MudBlazor.Mcp`** (command `mudblazor-mcp`), so every client below can launch it the same way.

### Prerequisites

- **.NET 10 SDK** (provides the `dnx` command).
- The package is not on a public feed yet, so build it locally first:
  ```bash
  dotnet pack src/MudBlazor.Mcp/MudBlazor.Mcp.csproj -c Release -o ./nupkg
  ```
  Then add `"--source", "<absolute-path>/nupkg"` to the `args` below. Use an **absolute** path — each client sets its own working directory, so a relative `./nupkg` will not resolve. Once the package is published to nuget.org (or a private feed configured in `NuGet.config`), remove the `--source` args.

### Key differences from the `dotnet run` configs

- The docs version is passed through the **`MUDBLAZOR_VERSION`** environment variable, because `dnx` reserves `--version` for the *package* version.
- Everything after `--` (here `--stdio`) is forwarded to the server. You may append `"--version", "9.0.0"` there instead of the env var.
- `dnx` runs from the client's working directory, so the ~500 MB clone lands in `./data` there. Set **`MudBlazor__Repository__DataPath`** to a fixed absolute path to share one cache across projects.

### VS Code — `.vscode/mcp.json`

```json
{
  "servers": {
    "mudblazor-mcp": {
      "command": "dnx",
      "args": ["MudBlazor.Mcp@1.0.0", "--yes", "--", "--stdio"],
      "env": {
        "MUDBLAZOR_VERSION": "9.0.0",
        "MudBlazor__Repository__DataPath": "C:/mud-mcp-cache"
      }
    }
  }
}
```

### Claude Desktop — `claude_desktop_config.json`

```json
{
  "mcpServers": {
    "mudblazor-mcp": {
      "command": "dnx",
      "args": ["MudBlazor.Mcp@1.0.0", "--yes", "--", "--stdio"],
      "env": { "MUDBLAZOR_VERSION": "9.0.0" }
    }
  }
}
```

### Continue.dev — `~/.continue/config.json`

```json
{
  "experimental": {
    "mcpServers": {
      "mudblazor-mcp": {
        "transport": {
          "type": "stdio",
          "command": "dnx",
          "args": ["MudBlazor.Mcp@1.0.0", "--yes", "--", "--stdio"]
        },
        "env": { "MUDBLAZOR_VERSION": "9.0.0" }
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
    "mudblazor-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/MudBlazor/Mcp/MudBlazor.Mcp/src/MudBlazor.Mcp",
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
    "mudblazor-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${workspaceFolder}/src/MudBlazor.Mcp",
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
cd src/MudBlazor.Mcp
dotnet publish -c Release -o ./publish
```

```json
{
  "github.copilot.chat.experimental.mcpServers": {
    "mudblazor-mcp": {
      "command": "C:/MudBlazor/Mcp/MudBlazor.Mcp/src/MudBlazor.Mcp/publish/MudBlazor.Mcp.exe",
      "args": ["--stdio"]
    }
  }
}
```

### Verification

1. Open GitHub Copilot Chat
2. Type: `@mudblazor list components`
3. Verify MudBlazor tools are available in tool picker

### Using the MudBlazor Expert Agent

To maximize the value of the MCP server, we provide a specialized agent file that teaches GitHub Copilot how to effectively use the MudBlazor MCP tools.

**Location:** `.github/agents/mudblazor-expert.agent.md`

This agent file:
- Provides decision logic for selecting the right MCP tool
- Enforces best practices (always query before answering)
- Includes Blazor and MudBlazor development guidelines
- Prevents hallucination by requiring tool-backed responses

**Usage in VS Code:**

1. Ensure the `.github/agents/` folder exists in your project
2. Copy or reference `mudblazor-expert.agent.md`
3. In Copilot Chat, the agent will automatically be available
4. Use `@workspace` to activate the agent context

**Agent Capabilities:**
- Component discovery and search
- Parameter and API reference lookup
- Code example retrieval
- Enum value queries
- Best practice guidance for Blazor development

> **Credits:** This agent file is derived from work in the [github/awesome-copilot](https://github.com/github/awesome-copilot) repository.

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
    "mudblazor-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:\\MudBlazor\\Mcp\\MudBlazor.Mcp\\src\\MudBlazor.Mcp",
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
    "mudblazor-mcp": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/MudBlazor.Mcp/src/MudBlazor.Mcp",
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
    "mudblazor-mcp": {
      "command": "C:\\MudBlazor\\Mcp\\MudBlazor.Mcp\\src\\MudBlazor.Mcp\\publish\\MudBlazor.Mcp.exe",
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
      "mudblazor-mcp": {
        "transport": {
          "type": "stdio",
          "command": "dotnet",
          "args": [
            "run",
            "--project",
            "C:/MudBlazor/Mcp/MudBlazor.Mcp/src/MudBlazor.Mcp",
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
dotnet run --project src/MudBlazor.Mcp -- --stdio
```

### Generic HTTP Client

For clients supporting HTTP transport:

```bash
# Start server in HTTP mode (default)
dotnet run --project src/MudBlazor.Mcp

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
        Arguments = ["run", "--project", "path/to/MudBlazor.Mcp", "--", "--stdio"]
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
    "mudblazor-mcp": {
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
    "mudblazor-mcp": {
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
    "mudblazor-mcp": {
      "command": "dotnet",
      "args": ["run", "--", "--stdio"],
      "cwd": "C:\\MudBlazor\\Mcp\\MudBlazor.Mcp\\src\\MudBlazor.Mcp"
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
dotnet run --project src/MudBlazor.Mcp -- --stdio 2>&1
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
