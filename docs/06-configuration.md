# Configuration

Complete guide to configuring the Mud MCP server.

## Table of Contents

- [Configuration Sources](#configuration-sources)
- [Configuration Sections](#configuration-sections)
- [Repository Options](#repository-options)
- [Cache Options](#cache-options)
- [Parsing Options](#parsing-options)
- [Logging Configuration](#logging-configuration)
- [Server Configuration](#server-configuration)
- [Environment Variables](#environment-variables)
- [Configuration Examples](#configuration-examples)

---

## Configuration Sources

Mud MCP follows the standard .NET configuration hierarchy (highest priority first):

1. **Command-line arguments**
2. **Environment variables**
3. **User secrets** (Development)
4. **appsettings.{Environment}.json**
5. **appsettings.json**

### Configuration Files

| File | Purpose |
|------|---------|
| `appsettings.json` | Base configuration |
| `appsettings.Development.json` | Development overrides |
| `appsettings.Production.json` | Production overrides |

---

## Configuration Sections

The main configuration structure:

```json
{
  "MudBlazor": {
    "Repository": { /* Git repository settings */ },
    "Cache": { /* Caching behavior */ },
    "Parsing": { /* Parser options */ }
  },
  "Logging": { /* Log levels */ },
  "AllowedHosts": "*"
}
```

---

## Repository Options

Controls how the MudBlazor repository is cloned and updated.

```json
{
  "MudBlazor": {
    "Repository": {
      "Url": "https://github.com/MudBlazor/MudBlazor.git",
      "Branch": "main",
      "LocalPath": "./data/mudblazor-repo"
    }
  }
}
```

### Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Url` | string | `https://github.com/MudBlazor/MudBlazor.git` | Repository URL |
| `Branch` | string | `main` | Branch to clone/track |
| `LocalPath` | string | `./data/mudblazor-repo` | Local clone path |

### Use Cases

**Use development branch:**
```json
{
  "Repository": {
    "Branch": "dev"
  }
}
```

**Custom clone location:**
```json
{
  "Repository": {
    "LocalPath": "C:/repos/mudblazor"
  }
}
```

**Private fork:**
```json
{
  "Repository": {
    "Url": "https://github.com/myorg/MudBlazor.git"
  }
}
```

---

## Parsing Options

Controls what gets indexed and how.

```json
{
  "MudBlazor": {
    "Parsing": {
      "IncludeInternalComponents": false,
      "IncludeDeprecatedComponents": true,
      "MaxExamplesPerComponent": 20
    }
  }
}
```

### Options Reference

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `IncludeInternalComponents` | bool | `false` | Index internal components |
| `IncludeDeprecatedComponents` | bool | `true` | Index deprecated components |
| `MaxExamplesPerComponent` | int | `20` | Max examples per component |

### Use Cases

**Include all components:**
```json
{
  "Parsing": {
    "IncludeInternalComponents": true,
    "IncludeDeprecatedComponents": true
  }
}
```

**Limit examples for faster indexing:**
```json
{
  "Parsing": {
    "MaxExamplesPerComponent": 5
  }
}
```

---

## Logging Configuration

Standard .NET logging configuration.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "MudBlazor.Mcp": "Debug",
      "Microsoft.AspNetCore": "Warning",
      "System.Net.Http.HttpClient": "Warning"
    }
  }
}
```

### Log Levels

| Level | Use Case |
|-------|----------|
| `Trace` | Very detailed diagnostic info |
| `Debug` | Development troubleshooting |
| `Information` | General operational events |
| `Warning` | Potential issues |
| `Error` | Errors requiring attention |
| `Critical` | System failures |
| `None` | Disable logging |

### Development Configuration

`appsettings.Development.json`:
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

### Production Configuration

`appsettings.Production.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "MudBlazor.Mcp": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

---

## Server Configuration

### Port Configuration

**Via launchSettings.json:**
```json
{
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": false,
      "applicationUrl": "http://localhost:8000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Via command line:**
```bash
dotnet run --urls "http://localhost:8080"
```

**Via environment:**
```bash
export ASPNETCORE_URLS="http://localhost:8080"
dotnet run
```

### Kestrel Configuration

For advanced scenarios:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:8000"
      }
    },
    "Limits": {
      "MaxRequestBodySize": 10485760,
      "RequestHeadersTimeout": "00:00:30"
    }
  }
}
```

### IIS Deployment Protocol

The `dotnet run --urls`, `ASPNETCORE_URLS`, and Kestrel settings above control local or container runtime binding. IIS deployment resolves its site binding separately in `eng/scripts/Configure-IisWebsite.ps1`; it does not load certificate thumbprints into Kestrel.

The Azure Pipeline default `iisBindingProtocol` is `auto`. Auto mode selects HTTPS when `iisDevSslCertificateThumbprint`, `iisTestSslCertificateThumbprint`, or `iisProdSslCertificateThumbprint` is supplied, or when the target IIS site already has an HTTPS binding on the deployment port with a certificate. If neither condition is true, deployment falls back to HTTP. The configure step emits `iisEffectiveBindingProtocol`, and the IIS health check uses that resolved value unless `healthScheme` is intentionally overridden.

---

## Environment Variables

### Override Configuration

Use double underscore (`__`) to set nested options:

```bash
# Repository settings
export MudBlazor__Repository__Branch=master
export MudBlazor__Repository__LocalPath=/custom/path

# Cache settings
export MudBlazor__Cache__RefreshIntervalMinutes=30

# Parsing settings
export MudBlazor__Parsing__MaxExamplesPerComponent=10

# Logging
export Logging__LogLevel__Default=Debug
```

### Windows PowerShell

```powershell
$env:MudBlazor__Repository__Branch = "main"
$env:MudBlazor__Cache__RefreshIntervalMinutes = "30"
dotnet run
```

### Docker / Container

```dockerfile
ENV MudBlazor__Repository__Branch=main
ENV MudBlazor__Cache__RefreshIntervalMinutes=30
ENV Logging__LogLevel__Default=Warning
```

### Common Environment Variables

| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | Set environment (Development, Production) |
| `ASPNETCORE_URLS` | Server URL binding |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OpenTelemetry endpoint |
| `DOTNET_RUNNING_IN_CONTAINER` | Container detection |

---

## Configuration Examples

### Development Configuration

`appsettings.Development.json`:
```json
{
  "MudBlazor": {
    "Repository": {
      "Branch": "main",
      "LocalPath": "./data/mudblazor-repo"
    },
    "Cache": {
      "RefreshIntervalMinutes": 5,
      "ComponentCacheDurationMinutes": 5
    },
    "Parsing": {
      "MaxExamplesPerComponent": 50
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "MudBlazor.Mcp": "Trace"
    }
  }
}
```

### Production Configuration

`appsettings.Production.json`:
```json
{
  "MudBlazor": {
    "Repository": {
      "Branch": "main",
      "LocalPath": "/app/data/mudblazor-repo"
    },
    "Parsing": {
      "IncludeInternalComponents": false,
      "MaxExamplesPerComponent": 20
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "MudBlazor.Mcp": "Information"
    }
  }
}
```

### Docker Compose

```yaml
version: '3.8'
services:
  mudblazor-mcp:
    build: .
    ports:
      - "8000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - MudBlazor__Repository__DataPath=/app/data
      - Logging__LogLevel__Default=Warning
    volumes:
      - mudblazor-data:/app/data

volumes:
  mudblazor-data:
```

### Kubernetes ConfigMap

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: mudblazor-mcp-config
data:
  appsettings.Production.json: |
    {
      "MudBlazor": {
        "Repository": {
          "Branch": "master",
          "LocalPath": "/app/data/repo"
        },
        "Cache": {
          "RefreshIntervalMinutes": 120
        }
      },
      "Logging": {
        "LogLevel": {
          "Default": "Warning"
        }
      }
    }
```

---

## Strongly-Typed Options

Configuration is bound to these C# classes:

```csharp
public sealed class MudBlazorOptions
{
    public const string SectionName = "MudBlazor";
    
    public RepositoryOptions Repository { get; set; } = new();
    public CacheOptions Cache { get; set; } = new();
    public ParsingOptions Parsing { get; set; } = new();
}

public sealed class RepositoryOptions
{
    public string Url { get; set; } = "https://github.com/MudBlazor/MudBlazor.git";
    public string Branch { get; set; } = "dev";
    public string LocalPath { get; set; } = "./data/mudblazor-repo";
}

public sealed class CacheOptions
{
    public int RefreshIntervalMinutes { get; set; } = 60;
    public int ComponentCacheDurationMinutes { get; set; } = 30;
    public int ExampleCacheDurationMinutes { get; set; } = 120;
    public int SlidingExpirationMinutes { get; set; } = 60;
    public int AbsoluteExpirationMinutes { get; set; } = 1440;
}

public sealed class ParsingOptions
{
    public bool IncludeInternalComponents { get; set; } = false;
    public bool IncludeDeprecatedComponents { get; set; } = true;
    public int MaxExamplesPerComponent { get; set; } = 20;
}
```

### Accessing Options

```csharp
// Via IOptions<T>
public ComponentIndexer(IOptions<MudBlazorOptions> options)
{
    _options = options.Value;
    var branch = _options.Repository.Branch;
}

// Via IOptionsSnapshot<T> (scoped, reloads on change)
public MyService(IOptionsSnapshot<MudBlazorOptions> options)
{
    _options = options.Value;
}

// Via IOptionsMonitor<T> (singleton, reloads on change)
public MyService(IOptionsMonitor<MudBlazorOptions> options)
{
    options.OnChange(newOptions => {
        // React to configuration changes
    });
}
```

---

## Next Steps

- [Testing](./07-testing.md) — Unit testing the configuration
- [MCP Inspector](./08-mcp-inspector.md) — Test with different configurations
- [Troubleshooting](./10-troubleshooting.md) — Common configuration issues
