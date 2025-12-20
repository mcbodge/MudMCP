# Troubleshooting Guide

Solutions for common issues when running Mud MCP.

## Table of Contents

- [Quick Diagnostics](#quick-diagnostics)
- [Startup Issues](#startup-issues)
- [Repository Issues](#repository-issues)
- [Indexing Issues](#indexing-issues)
- [MCP Protocol Issues](#mcp-protocol-issues)
- [IDE Integration Issues](#ide-integration-issues)
- [Performance Issues](#performance-issues)
- [Logging and Debugging](#logging-and-debugging)

---

## Quick Diagnostics

### Health Check (HTTP Mode)

```bash
curl http://localhost:5180/health
```

**Healthy Response:**
```json
{
  "status": "Healthy",
  "totalDuration": 15.2,
  "checks": [{
    "name": "indexer",
    "status": "Healthy",
    "data": {
      "status": "ready",
      "componentCount": 85,
      "categoryCount": 13,
      "isIndexed": true,
      "lastIndexed": "2025-01-15T10:30:00Z"
    }
  }]
}
```

### Manual Tool Test (stdio)

```bash
cd src/MudBlazor.Mcp
echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | dotnet run -- --stdio 2>nul
```

### Check .NET Version

```bash
dotnet --version
# Expected: 10.0.xxx
```

---

## Startup Issues

### Issue: "SDK not found"

**Error:**
```
A compatible .NET SDK was not found.
```

**Solution:**
```bash
# Install .NET 10 SDK
winget install Microsoft.DotNet.SDK.10
# Or download from https://dotnet.microsoft.com/download
```

### Issue: "Project not found"

**Error:**
```
Could not find a project to run.
```

**Solution:**
```bash
# Ensure you're in the correct directory
cd src/MudBlazor.Mcp
dotnet run

# Or specify full path
dotnet run --project "C:\Path\To\MudBlazor.Mcp\src\MudBlazor.Mcp"
```

### Issue: "Port already in use"

**Error:**
```
System.IO.IOException: Failed to bind to address http://127.0.0.1:5180
```

**Solutions:**

```bash
# Find what's using the port
netstat -ano | findstr :5180

# Kill the process (replace PID)
taskkill /PID <PID> /F

# Or use a different port
dotnet run --urls "http://localhost:5181"
```

### Issue: "Build errors"

**Error:**
```
error CS0234: The type or namespace name '...' does not exist
```

**Solution:**
```bash
# Restore packages
dotnet restore

# Clean and rebuild
dotnet clean
dotnet build
```

---

## Repository Issues

### Issue: "Clone failed - network error"

**Error:**
```
LibGit2Sharp.LibGit2SharpException: network error
```

**Solutions:**

1. **Check network connectivity:**
   ```bash
   ping github.com
   ```

2. **Manual clone:**
   ```bash
   git clone https://github.com/MudBlazor/MudBlazor.git ./data/mudblazor-repo
   ```

3. **Use SSH instead:**
   ```json
   {
     "MudBlazor": {
       "Repository": {
         "Url": "git@github.com:MudBlazor/MudBlazor.git"
       }
     }
   }
   ```

### Issue: "Clone failed - access denied"

**Error:**
```
UnauthorizedAccessException: Access to the path is denied
```

**Solutions:**

1. **Run as Administrator** (first time)

2. **Check folder permissions:**
   ```bash
   icacls ./data /grant Users:F /T
   ```

3. **Change clone location:**
   ```json
   {
     "MudBlazor": {
       "Repository": {
         "LocalPath": "C:/Temp/mudblazor-repo"
       }
     }
   }
   ```

### Issue: "Repository locked"

**Error:**
```
LibGit2Sharp.LockedFileException: The index is locked
```

**Solutions:**

```bash
# Remove lock files
del /f ./data/mudblazor-repo/.git/index.lock
del /f ./data/mudblazor-repo/.git/HEAD.lock

# Or delete and re-clone
rd /s /q ./data/mudblazor-repo
```

### Issue: "Disk space"

**Error:**
```
IOException: There is not enough space on the disk
```

**Solution:**
The MudBlazor repo requires ~500MB. Free up disk space or change clone location.

---

## Indexing Issues

### Issue: "Index not ready"

**Error:**
```
Component index is not ready. The server may still be initializing.
```

**Solutions:**

1. **Wait for indexing** - First startup takes 30-60 seconds
2. **Check logs** for progress:
   ```bash
   dotnet run 2>&1 | findstr "Index"
   ```
3. **Check health endpoint:**
   ```bash
   curl http://localhost:5180/health
   ```

### Issue: "No components found"

**Error:**
```
Index built successfully with 0 components
```

**Solutions:**

1. **Verify repository structure:**
   ```bash
   dir ./data/mudblazor-repo/src/MudBlazor/Components
   ```

2. **Check parsing errors in logs**

3. **Force re-clone:**
   ```bash
   rd /s /q ./data/mudblazor-repo
   dotnet run
   ```

### Issue: "Component not found"

**Error:**
```
Component 'MudDataGrid' not found.
```

**Solutions:**

1. **Check spelling** - Names are case-insensitive
2. **Use list_components** to see available components
3. **Try with/without "Mud" prefix:**
   - Both `MudButton` and `Button` should work

---

## MCP Protocol Issues

### Issue: "Invalid JSON response"

**Error:**
```
SyntaxError: Unexpected token in JSON at position 0
```

**Cause:** Server logging to stdout instead of stderr

**Solution:** Already configured correctly in Program.cs:
```csharp
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
```

**Verify no console writes:**
```csharp
// ❌ Don't use Console.WriteLine
Console.WriteLine("Debug");

// ✅ Use logging
_logger.LogDebug("Debug");
```

### Issue: "Tool not found"

**Error:**
```
McpException: Tool 'unknown_tool' not found
```

**Solution:** Check available tools:
```json
{"jsonrpc":"2.0","method":"tools/list","id":1}
```

### Issue: "Invalid parameters"

**Error:**
```
McpException: Parameter 'componentName' cannot be null or empty
```

**Solution:** Check tool parameters:
```json
{
  "method": "tools/call",
  "params": {
    "name": "get_component_detail",
    "arguments": {
      "componentName": "MudButton"  // Required
    }
  }
}
```

### Issue: "Connection timeout"

**Error:**
```
TimeoutException: The operation has timed out
```

**Solutions:**

1. **Increase timeout in client**
2. **Pre-build index:** Start server and wait before connecting
3. **Use HTTP transport** for long-running server

---

## IDE Integration Issues

### Issue: VS Code - Server not starting

**Symptoms:** No MCP tools appear, no errors shown

**Solutions:**

1. **Check Output panel:**
   - View → Output → Select "GitHub Copilot"
   
2. **Verify configuration:**
   ```json
   // settings.json
   {
     "github.copilot.chat.experimental.mcpServers": {
       "mudblazor-mcp": {
         "command": "dotnet",
         "args": ["run", "--project", "FULL_PATH", "--", "--stdio"]
       }
     }
   }
   ```

3. **Test manually:**
   ```bash
   dotnet run --project "FULL_PATH" -- --stdio
   ```

### Issue: Claude Desktop - Server crashes

**Symptoms:** Claude shows "MCP server unavailable"

**Solutions:**

1. **Check Claude logs:**
   - Windows: `%APPDATA%\Claude\logs`
   
2. **Simplify config:**
   ```json
   {
     "mcpServers": {
       "mudblazor-mcp": {
         "command": "C:\\full\\path\\to\\MudBlazor.Mcp.exe",
         "args": ["--stdio"]
       }
     }
   }
   ```

3. **Use published binary** (faster startup):
   ```bash
   dotnet publish -c Release -o ./publish
   ```

### Issue: Path with spaces

**Error:**
```
The system cannot find the path specified
```

**Solution:** Quote paths properly:
```json
{
  "args": ["run", "--project", "\"C:/Path With Spaces/MudBlazor.Mcp\"", "--", "--stdio"]
}
```

Or use short paths:
```json
{
  "args": ["run", "--project", "C:/PathWi~1/MudBlazor.Mcp", "--", "--stdio"]
}
```

---

## Performance Issues

### Issue: Slow startup

**Cause:** Repository cloning + index building on first run

**Solutions:**

1. **Pre-clone repository:**
   ```bash
   git clone https://github.com/MudBlazor/MudBlazor.git ./data/mudblazor-repo
   ```

2. **Use published binary:**
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained
   ```

3. **Increase cache duration:**
   ```json
   {
     "MudBlazor": {
       "Cache": {
         "AbsoluteExpirationMinutes": 2880
       }
     }
   }
   ```

### Issue: High memory usage

**Solutions:**

1. **Limit examples:**
   ```json
   {
     "MudBlazor": {
       "Parsing": {
         "MaxExamplesPerComponent": 10
       }
     }
   }
   ```

2. **Reduce cache size:**
   ```json
   {
     "MudBlazor": {
       "Cache": {
         "ComponentCacheDurationMinutes": 15
       }
     }
   }
   ```

### Issue: Slow searches

**Solutions:**

1. **Limit results:**
   ```json
   {
     "arguments": {
       "query": "button",
       "maxResults": 5
     }
   }
   ```

2. **Search specific fields:**
   ```json
   {
     "arguments": {
       "query": "button",
       "searchFields": "name"
     }
   }
   ```

---

## Logging and Debugging

### Enable Debug Logging

**appsettings.Development.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "MudBlazor.Mcp": "Trace",
      "Microsoft.AspNetCore": "Debug"
    }
  }
}
```

### Capture Logs

**PowerShell:**
```powershell
# Capture stderr to file
$proc = Start-Process -FilePath "dotnet" -ArgumentList "run","--","--stdio" `
  -RedirectStandardError "server.log" -PassThru -NoNewWindow
```

**Command Prompt:**
```cmd
dotnet run -- --stdio 2>server.log
```

### Debug with VS Code

**launch.json:**
```json
{
  "configurations": [
    {
      "name": "Debug MCP Server",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "${workspaceFolder}/src/MudBlazor.Mcp/bin/Debug/net10.0/MudBlazor.Mcp.dll",
      "args": ["--stdio"],
      "cwd": "${workspaceFolder}/src/MudBlazor.Mcp",
      "console": "integratedTerminal"
    }
  ]
}
```

### Common Log Messages

| Message | Meaning |
|---------|---------|
| `Cloning MudBlazor repository` | First-time clone starting |
| `Successfully cloned` | Clone completed |
| `Building index...` | Index build starting |
| `Index built successfully with N components` | Ready to serve |
| `Repository already up to date` | No update needed |

---

## Getting Help

### Collect Diagnostic Info

```bash
# System info
dotnet --info

# Project info
dotnet list package

# Health check
curl http://localhost:5180/health
```

### Report Issues

Include in bug reports:
1. Error message (full stack trace)
2. Steps to reproduce
3. `dotnet --info` output
4. Configuration files (sanitized)
5. Server logs

### Resources

- [GitHub Issues](https://github.com/YourOrg/MudBlazor.Mcp/issues)
- [MCP Protocol Spec](https://spec.modelcontextprotocol.io/)
- [MudBlazor Docs](https://mudblazor.com/docs)
- [.NET Troubleshooting](https://docs.microsoft.com/dotnet/core/tools/troubleshoot-usage-issues)

---

## Quick Reference

### Diagnostic Commands

```bash
# Health check
curl http://localhost:5180/health

# List tools
echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | dotnet run -- --stdio 2>nul

# Test specific tool
curl -X POST http://localhost:5180/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"list_components"},"id":1}'
```

### Reset Commands

```bash
# Clean rebuild
dotnet clean && dotnet build

# Reset repository
rd /s /q ./data/mudblazor-repo

# Reset all
rd /s /q ./bin ./obj ./data
dotnet restore
dotnet build
```
