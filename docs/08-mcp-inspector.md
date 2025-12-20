# Testing with MCP Inspector

Step-by-step guide to testing Mud MCP using the official MCP Inspector tool.

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Installing MCP Inspector](#installing-mcp-inspector)
- [Running the Server](#running-the-server)
- [Connecting MCP Inspector](#connecting-mcp-inspector)
- [Testing Tools](#testing-tools)
- [Testing Scenarios](#testing-scenarios)
- [Debugging Tips](#debugging-tips)
- [Common Issues](#common-issues)

---

## Overview

MCP Inspector is the official debugging tool for Model Context Protocol servers. It provides:

- **Visual interface** for testing MCP tools
- **Protocol inspection** of requests/responses
- **Real-time testing** without an AI client
- **JSON payload editing** for custom requests

---

## Prerequisites

| Requirement | Version | Purpose |
|-------------|---------|---------|
| Node.js | 18+ | MCP Inspector runtime |
| npm | 8+ | Package management |
| .NET SDK | 10.0 | Server runtime |

### Verify Prerequisites

```bash
# Check Node.js
node --version
# Output: v18.x.x or higher

# Check npm
npm --version
# Output: 8.x.x or higher

# Check .NET
dotnet --version
# Output: 10.0.xxx
```

---

## Installing MCP Inspector

### Option 1: npx (Recommended)

No installation required:

```bash
npx npx @modelcontextprotocol/inspector
```

### Option 2: Global Install

```bash
npm install -g @modelcontextprotocol/inspector
mcp-inspector
```

---

## Running the Server

### Option A: stdio Transport (Recommended for Inspector)

```bash
cd src/MudBlazor.Mcp
dotnet run -- --stdio
```

The server will:
1. Clone the MudBlazor repository (first run only)
2. Build the component index
3. Start accepting MCP protocol messages via stdin/stdout

### Option B: HTTP Transport

```bash
cd src/MudBlazor.Mcp
dotnet run
```

Server runs at `http://localhost:5180/mcp`

---

## Connecting MCP Inspector

### For stdio Transport

```bash
npx @modelcontextprotocol/inspector \
  --command "dotnet" \
  --args "run --project src/MudBlazor.Mcp -- --stdio"
```

Or from the solution root:

```bash
npx @modelcontextprotocol/inspector \
  --command "dotnet" \
  --args "run --project c:/Mapei/MudBlazor/Mcp/MudBlazor.Mcp/src/MudBlazor.Mcp -- --stdio"
```

### For HTTP Transport

1. Start the server:
   ```bash
   cd src/MudBlazor.Mcp
   dotnet run
   ```

2. In another terminal, start inspector:
   ```bash
   npx @modelcontextprotocol/inspector --url http://localhost:5180/mcp
   ```

### Inspector Interface

Once connected, you'll see:

```
┌─────────────────────────────────────────────────────────────┐
│  MCP Inspector                                              │
│  Connected to: MudBlazor Documentation Server v1.0.0        │
├─────────────────────────────────────────────────────────────┤
│  Tools (12)                                                 │
│  ├── list_components                                        │
│  ├── list_categories                                        │
│  ├── get_component_detail                                   │
│  ├── get_component_parameters                               │
│  ├── get_component_examples                                 │
│  ├── get_example_by_name                                    │
│  ├── list_component_examples                                │
│  ├── search_components                                      │
│  ├── get_components_by_category                             │
│  ├── get_related_components                                 │
│  ├── get_api_reference                                      │
│  └── get_enum_values                                        │
└─────────────────────────────────────────────────────────────┘
```

---

## Testing Tools

### 1. List Components

**Tool:** `list_components`

**Parameters:** None

**Expected Response:**
```markdown
# MudBlazor Components

| Component | Category | Description |
|-----------|----------|-------------|
| MudButton | Buttons | A Material Design button... |
| MudTextField | Form Inputs | Text input component... |
...
```

### 2. Get Component Detail

**Tool:** `get_component_detail`

**Parameters:**
```json
{
  "componentName": "MudButton",
  "includeInherited": false,
  "includeExamples": true
}
```

**Expected Response:**
```markdown
# MudButton

A Material Design button component.

## Parameters

| Name | Type | Default | Description |
|------|------|---------|-------------|
| Color | Color | Default | The button color |
| Variant | Variant | Text | The button variant |
...

## Examples

### Basic
<MudButton>Click Me</MudButton>
```

### 3. Search Components

**Tool:** `search_components`

**Parameters:**
```json
{
  "query": "form input",
  "maxResults": 5
}
```

**Expected Response:**
```markdown
# Search Results for "form input"

Found 5 matching components:

## 1. MudTextField (Score: 85)
Category: Form Inputs & Controls
Text input component for forms...
```

### 4. Get Component Examples

**Tool:** `get_component_examples`

**Parameters:**
```json
{
  "componentName": "MudButton",
  "maxExamples": 3
}
```

**Expected Response:**
```markdown
# MudButton Examples

## Basic
<MudButton>Click</MudButton>

## Variants
<MudButton Variant="Variant.Filled">Filled</MudButton>
<MudButton Variant="Variant.Outlined">Outlined</MudButton>
```

### 5. Get Specific Example

**Tool:** `get_example_by_name`

**Parameters:**
```json
{
  "componentName": "MudButton",
  "exampleName": "Basic"
}
```

### 6. List Categories

**Tool:** `list_categories`

**Expected Response:**
```markdown
# MudBlazor Component Categories

| Category | Components |
|----------|------------|
| Buttons | MudButton, MudIconButton, MudFab... |
| Form Inputs | MudTextField, MudSelect... |
...
```

### 7. Get Components by Category

**Tool:** `get_components_by_category`

**Parameters:**
```json
{
  "category": "Buttons"
}
```

### 8. Get Related Components

**Tool:** `get_related_components`

**Parameters:**
```json
{
  "componentName": "MudButton",
  "relationshipType": "sibling"
}
```

### 9. Get API Reference

**Tool:** `get_api_reference`

**Parameters:**
```json
{
  "typeName": "MudButton"
}
```

### 10. Get Enum Values

**Tool:** `get_enum_values`

**Parameters:**
```json
{
  "enumName": "Color"
}
```

---

## Testing Scenarios

### Scenario 1: Component Discovery

1. Call `list_categories` to see available categories
2. Call `get_components_by_category` with "Buttons"
3. Call `get_component_detail` for "MudButton"
4. Call `get_component_examples` for "MudButton"

### Scenario 2: Search Workflow

1. Call `search_components` with query "dialog"
2. Review results
3. Call `get_component_detail` for top result
4. Call `get_related_components` to find similar

### Scenario 3: Example Exploration

1. Call `list_component_examples` for a component
2. Call `get_example_by_name` for specific example
3. Review code and try different examples

### Scenario 4: Error Handling

1. Call `get_component_detail` with invalid name "NotAComponent"
   - Expect: Error with suggestion to use `list_components`
2. Call `search_components` with empty query
   - Expect: Validation error
3. Call `get_enum_values` with unknown enum "UnknownEnum"
   - Expect: Error with available enums

---

## Debugging Tips

### Enable Debug Logging

Run with increased verbosity:

```bash
# Set environment variable
$env:Logging__LogLevel__MudBlazor_Mcp = "Debug"
dotnet run -- --stdio
```

### View Raw Protocol Messages

MCP Inspector shows raw JSON in the protocol tab:

```json
// Request
{
  "jsonrpc": "2.0",
  "method": "tools/call",
  "params": {
    "name": "get_component_detail",
    "arguments": {
      "componentName": "MudButton"
    }
  },
  "id": 1
}

// Response
{
  "jsonrpc": "2.0",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "# MudButton\n\n..."
      }
    ]
  },
  "id": 1
}
```

### Monitor Server Logs

Server logs go to stderr for MCP compatibility:

```bash
# PowerShell - capture stderr
dotnet run -- --stdio 2>&1 | Tee-Object -FilePath server.log
```

### Health Check

Before testing, verify server health:

```bash
# HTTP transport only
curl http://localhost:5180/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": [{
    "name": "indexer",
    "status": "Healthy",
    "data": {
      "componentCount": 85,
      "categoryCount": 13
    }
  }]
}
```

---

## Common Issues

### Issue: "Index not built"

**Symptom:**
```
Error: Component index is not ready. The server may still be initializing.
```

**Solution:** Wait for initial index build (check logs for "Index built successfully")

### Issue: "Repository clone failed"

**Symptom:**
```
Error: Failed to clone repository
```

**Solutions:**
1. Check network connectivity
2. Verify git is available: `git --version`
3. Check disk space
4. Try manual clone:
   ```bash
   git clone https://github.com/MudBlazor/MudBlazor.git ./data/mudblazor-repo
   ```

### Issue: "Connection refused"

**Symptom:**
```
Error: connect ECONNREFUSED 127.0.0.1:5180
```

**Solutions:**
1. Verify server is running
2. Check port isn't blocked
3. Try different port:
   ```bash
   dotnet run --urls "http://localhost:8080"
   ```

### Issue: "Invalid JSON in response"

**Symptom:**
```
Error: Unexpected token in JSON
```

**Solution:** Ensure logging goes to stderr, not stdout:
```csharp
// In Program.cs - already configured correctly
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
```

### Issue: "Tool not found"

**Symptom:**
```
Error: Tool 'unknown_tool' not found
```

**Solution:** Use `list_tools` to see available tools

### Issue: MCP Inspector won't start

**Symptom:**
```
npm ERR! code ENOENT
```

**Solutions:**
1. Update Node.js: `nvm install 18`
2. Clear npm cache: `npm cache clean --force`
3. Try global install: `npm install -g @modelcontextprotocol/inspector`

---

## Quick Reference

### Inspector Commands

| Command | Purpose |
|---------|---------|
| `tools` | List available tools |
| `call <tool>` | Invoke a tool |
| `params` | Show tool parameters |
| `help` | Show help |
| `quit` | Exit inspector |

### Server Commands

| Command | Purpose |
|---------|---------|
| `dotnet run` | HTTP transport |
| `dotnet run -- --stdio` | stdio transport |
| `dotnet run --urls "..."` | Custom port |

---

## Next Steps

- [IDE Integration](./09-ide-integration.md) — Configure for VS Code and Claude Desktop
- [Troubleshooting](./10-troubleshooting.md) — More debugging techniques
- [Tools Reference](./05-tools-reference.md) — Detailed tool documentation
