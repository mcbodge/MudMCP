# Engineering Folder

This folder contains Azure DevOps pipeline configurations for building, testing, and deploying the MudBlazor MCP Server.

## CLI Startup Benchmark

From the repository root, with PowerShell 7 and an indexed MudBlazor version:

```powershell
pwsh -NoProfile -File eng\scripts\Measure-CliLatency.ps1 -Version 9.0.0
pwsh -NoProfile -File eng\scripts\Measure-CliLatency.ps1 -Version 9.0.0 -NoBuild -StrictBudget
pwsh -NoProfile -File eng\scripts\Test-MeasureCliLatency.ps1
```

The benchmark starts a fresh Release process for each sample (20 per command).
It reports nearest-rank p50/p95 for `elapsed-ms` (CLI entry through stdout flush)
and `process-ms` (including runtime startup). Warm OS file caches do not eliminate
first-use JIT compilation or JSON metadata initialization. Timing diagnostics
run after the measured flush and are excluded from `elapsed-ms`.

The default 50 ms target is advisory: measurements above it produce an explicit
stderr warning and exit 0 if all commands succeeded. `-StrictBudget` turns an
overrun into exit 1; `-BudgetMs` changes the target. Build failures, failed
queries, or invalid/missing timing data always exit 1. An unindexed version exits
2 with an indexing hint. `-DataPath` selects a different cache, and the script
restores the caller's `MUDMCP_TIMING` value even on failure.

### Profiling Findings (2026-10-21)

- A startup CPU trace identified JSON type-info creation/configuration in the
  manifest and component reads. In a separate stage probe, the first manifest
  read took 80.85 ms and the first component read 38.92 ms; later reads were
  approximately 1 ms. These are individual diagnostic samples, not p95 results.
- The same in-process props query took 192.80 ms initially and 3.79/2.12 ms on
  subsequent calls. The benchmark intentionally retains fresh-process samples;
  it does not use those warm-process results to claim the 50 ms target is met.
- The query probe observed no loaded ASP.NET Core, MCP, Roslyn, or LibGit2Sharp
  assemblies. Buffered output flush cost 5.36-7.98 ms in three separate runs;
  replacing asynchronous output was not justified.
- A 20-pair experiment with parameterless JSON record constructors showed no
  material median improvement (53.95 versus 53.62 ms). A partial-manifest reader
  offered only a small median gain and changed validation behavior. Neither
  experiment was adopted; source-generated JSON and the store schema stay intact.
- The adopted changes capture elapsed time at the actual flush boundary and
  avoid building an unused API-reference model for compact `api` output.
  The timing correction improves measurement accuracy, not the query algorithm.
  No GC, globalization, AOT, ReadyToRun, or package configuration was changed.

The following comparison alternated the original and updated binaries, changing
execution order on each iteration, for 20 fresh-process samples per command.
Every paired stdout result was byte-for-byte identical. Timings on this shared
machine varied substantially, so median improvements are modest and tail improvements
are not consistent; these figures do not establish a general startup speedup.

| Command | Before p50 | After p50 | Before p95 | After p95 |
|---------|-----------:|----------:|-----------:|----------:|
| `props MudButton --filter Color,Variant,Size` | 121.7 ms | 121.3 ms | 180.1 ms | 159.6 ms |
| `api MudButton` | 137.5 ms | 127.7 ms | 169.5 ms | 190.1 ms |
| `list` | 144.4 ms | 141.4 ms | 170.5 ms | 201.8 ms |

A separate complete 13-command, 20-sample strict run reported p95 values from
130.7 to 257.2 ms and correctly exited 1; the default benchmark completed with
exit 0 and above-target warnings. The overruns are accepted under the revised
TASK-024 scope: cold metadata/JIT and process costs remain visible, while more
invasive format or distribution changes are deferred rather than introduced
solely to satisfy an arbitrary threshold.

## Pipeline Structure

```
eng/
├── azure-pipelines.yaml          # Main pipeline definition
├── templates/
│   ├── deploy-iis-stage.yaml     # Reusable environment deployment stage template
│   └── deploy-iis.yaml           # Reusable IIS deployment step template
├── scripts/                      # PowerShell deployment scripts
│   ├── Stop-IisSiteAndAppPool.ps1
│   ├── Backup-Deployment.ps1
│   ├── Deploy-IisContent.ps1
│   ├── Configure-IisWebsite.ps1
│   ├── Update-EnvironmentSettings.ps1
│   ├── Set-IisFolderPermissions.ps1
│   ├── Start-IisSiteAndAppPool.ps1
│   ├── Test-DeploymentHealth.ps1
│   └── Prepare-IisConfiguration.ps1
└── README.md                     # This file
```

## Pipeline Overview

The pipeline consists of build validation plus shared IIS deployment stages:

### 1. Build Stage
- Restores NuGet packages (with caching)
- Builds the solution in Release configuration
- Runs unit tests with code coverage
- Publishes the application
- Creates deployment artifact

### 2. Deploy to Development
- Triggers on `develop` branch
- Deploys to `mudblazor-mcp-dev` environment
- Uses the shared IIS deployment stage and step templates

### 3. Deploy to Test
- Triggers on `main` branch after Build
- Deploys to `mudblazor-mcp-test` environment
- Uses `ASPNETCORE_ENVIRONMENT=Staging`
- Uses the same IIS deployment configuration as development and production

### 4. Deploy to Production
- Triggers on `main` branch
- Runs after the Test deployment succeeds
- Deploys to `mudblazor-mcp-prod` environment
- Includes health checks and rollback notifications
- Uses the same IIS deployment configuration as development and test

## Prerequisites

### Azure DevOps Setup

1. **Create Environments**:
    - Go to Pipelines → Environments
    - Create `mudblazor-mcp-dev` for development
    - Create `mudblazor-mcp-test` for test
    - Create `mudblazor-mcp-prod` for production (with approval gates)

2. **Register VM as Deployment Target**:
   - In each environment, click "Add resource" → "Virtual machines"
   - Follow the registration script for your Windows VM
   - **Important**: The agent pool name used during VM registration must match your environment configuration
   - Deployment jobs use environment-registered agents (no explicit pool specified in YAML)
   - Ensure the VM has:
     - IIS installed with ASP.NET Core Hosting Bundle
     - .NET 10 Runtime
     - PowerShell 5.1+

3. **Configure Service Connections** (if using Azure resources):
   - Go to Project Settings → Service connections
   - Create connections for any Azure resources needed

### VM Requirements

The target VM must have:

```powershell
# Install IIS
Install-WindowsFeature -Name Web-Server -IncludeManagementTools

# Install ASP.NET Core Hosting Bundle
# Download from: https://dotnet.microsoft.com/download/dotnet/10.0

# Verify installation
Get-WindowsFeature Web-Server
dotnet --list-runtimes
```

## Deployment Scripts

The `eng/scripts/` directory contains PowerShell scripts used by the deployment pipeline. These scripts are version-controlled and reviewed to ensure security and reliability.

### Script Overview

| Script | Purpose |
|--------|---------|
| `Prepare-IisConfiguration.ps1` | Creates web.config and logs directory during build |
| `Stop-IisSiteAndAppPool.ps1` | Gracefully stops IIS app pool before deployment |
| `Backup-Deployment.ps1` | Creates timestamped backup with retention policy |
| `Deploy-IisContent.ps1` | Copies application files to IIS physical path |
| `Configure-IisWebsite.ps1` | Creates/updates IIS website and app pool |
| `Update-EnvironmentSettings.ps1` | Updates web.config environment variables |
| `Set-IisFolderPermissions.ps1` | Configures file system ACLs |
| `Start-IisSiteAndAppPool.ps1` | Starts IIS app pool and website |
| `Test-DeploymentHealth.ps1` | Verifies deployment with health checks and diagnostics |

### Script Security Features

All deployment scripts implement hardening measures:
- **Input validation**: Parameters use `[ValidateNotNullOrEmpty()]`, `[ValidateRange()]`, and `[ValidateSet()]`
- **Path validation**: Physical paths restricted to allowed roots (`C:\inetpub`, `C:\WWW`, `D:\WWW`)
- **Name validation**: IIS names restricted to alphanumeric characters, underscores, and hyphens
- **Path traversal protection**: Blocks `..` and invalid characters in paths
- **Strict mode**: Scripts use `Set-StrictMode -Version Latest`
- **Error handling**: Proper `$ErrorActionPreference` settings
- **No secrets**: Scripts never log sensitive data

### Using Scripts Locally

Scripts can be executed manually for troubleshooting:

```powershell
# Stop app pool
.\eng\scripts\Stop-IisSiteAndAppPool.ps1 -AppPoolName "MudBlazorMcpPool"

# Create backup
.\eng\scripts\Backup-Deployment.ps1 -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp"

# Configure IIS binding. Auto selects HTTPS when a thumbprint or existing HTTPS certificate is available; otherwise HTTP.
.\eng\scripts\Configure-IisWebsite.ps1 -WebsiteName "MudBlazorMcp" -AppPoolName "MudBlazorMcpPool" -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp" -Port 8000 -BindingProtocol auto -SslCertificateThumbprint "ABCD1234..."

# Test health with the protocol reported by Configure-IisWebsite.ps1
.\eng\scripts\Test-DeploymentHealth.ps1 -Port 8000 -Scheme https -AppPoolName "MudBlazorMcpPool" -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp"
```

## Configuration

### Pipeline Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `buildConfiguration` | Build configuration | `Release` |
| `dotnetVersion` | .NET SDK version | `10.x` |
| `iisWebsiteName` | IIS website name | `MudBlazorMcp` |
| `iisAppPoolName` | IIS app pool name | `MudBlazorMcpPool` |
| `iisPhysicalPath` | Deployment path | `C:\inetpub\wwwroot\MudBlazorMcp` |
| `iisPort` | IIS website binding port | `8000` |
| `iisBindingProtocol` | IIS website binding protocol (`auto`, `http`, or `https`) | `auto` |
| `iisDevSslCertificateThumbprint` | Development HTTPS certificate thumbprint from `Cert:\LocalMachine\My`; enables HTTPS in auto mode | unset |
| `iisTestSslCertificateThumbprint` | Test HTTPS certificate thumbprint from `Cert:\LocalMachine\My`; enables HTTPS in auto mode | unset |
| `iisProdSslCertificateThumbprint` | Production HTTPS certificate thumbprint from `Cert:\LocalMachine\My`; enables HTTPS in auto mode | unset |
| `deploymentHealthMaxRetries` | Health check retry count | `6` |
| `deploymentHealthRetryDelaySeconds` | Delay between health check retries | `10` |
| `mudBlazorVersion` | MudBlazor docs version served by the MCP server | `9.0.0` |

### Environment-Specific Settings

Dev, test, and production share the same IIS deployment settings from the pipeline variables above and the same deployment lifecycle from `eng/templates/deploy-iis-stage.yaml`. Only the Azure DevOps environment name and `ASPNETCORE_ENVIRONMENT` value differ by environment.

The shared deployment uses `auto` protocol selection by default. In auto mode, the IIS script selects HTTPS when a per-environment certificate thumbprint is supplied or when the target site already has an HTTPS binding on the deployment port with a certificate. If neither is true, it falls back to HTTP. The configure step publishes `iisEffectiveBindingProtocol`, and the health check uses that resolved protocol.

- **Recommended HTTPS:** install a certificate in `Cert:\LocalMachine\My`, then set `iisDevSslCertificateThumbprint`, `iisTestSslCertificateThumbprint`, or `iisProdSslCertificateThumbprint` as Azure DevOps pipeline variables or variable group values.
- **HTTP fallback:** leave the thumbprint unset and use the default `auto` mode when an environment does not have a certificate yet.
- **Force HTTP:** set `iisBindingProtocol` to `http`.
- **Force HTTPS:** set `iisBindingProtocol` to `https`; this requires a supplied thumbprint or an existing HTTPS binding with a certificate.

For development or test servers, you can create and trust a self-signed certificate. Replace `localhost` with the DNS name clients and health checks will use if it is different:

```powershell
$cert = New-SelfSignedCertificate -DnsName "localhost" -CertStoreLocation "Cert:\LocalMachine\My"
$certificatePath = Join-Path $env:TEMP "mudmcp-local.cer"
Export-Certificate -Cert $cert -FilePath $certificatePath
Import-Certificate -FilePath $certificatePath -CertStoreLocation "Cert:\LocalMachine\Root"
$cert.Thumbprint
```

Use the printed thumbprint as the appropriate `iis*SslCertificateThumbprint` variable for that environment.

If a server needs environment-specific application settings, create the appropriate `appsettings.{Environment}.json` file on that server. The deployment preserves server-managed `appsettings.*.json` files. For example, production can use `appsettings.Production.json`:

```json
{
  "MudBlazor": {
    "Repository": {
      "DataPath": "C:\\ProgramData\\MudBlazorMcp"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

## Running the Pipeline

### Automatic Triggers

- **Develop branch**: Triggers Build + Deploy to Dev
- **Main branch**: Triggers Build + Deploy to Test + Deploy to Production

### Manual Run

1. Go to Pipelines → Select the pipeline
2. Click "Run pipeline"
3. Select branch
4. Click "Run"

## Troubleshooting

### Common Issues

**App pool won't start**:
```powershell
# Check event log
Get-EventLog -LogName System -Source "IIS*" -Newest 10
```

**Health check fails**:
```powershell
# Check if app is listening
netstat -an | Select-String "8000"

# Check application logs
Get-Content "C:\inetpub\wwwroot\MudBlazorMcp\logs\stdout*.log" -Tail 50
```

**Permission issues**:
```powershell
# Grant minimal required permissions
$sitePath = "C:\inetpub\wwwroot\MudBlazorMcp"
$appPoolIdentity = "IIS AppPool\MudBlazorMcpPool"

# Read/Execute on site root
$acl = Get-Acl $sitePath
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $appPoolIdentity, "ReadAndExecute,Synchronize", "ContainerInherit,ObjectInherit", "None", "Allow")
$acl.SetAccessRule($rule)
Set-Acl $sitePath $acl

# Modify on logs and data directories only
foreach ($dir in @("logs", "data")) {
    $subPath = Join-Path $sitePath $dir
    if (-not (Test-Path $subPath)) { New-Item -ItemType Directory -Path $subPath -Force }
    $subAcl = Get-Acl $subPath
    $writeRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $appPoolIdentity, "Modify,Synchronize", "ContainerInherit,ObjectInherit", "None", "Allow")
    $subAcl.SetAccessRule($writeRule)
    Set-Acl -Path $subPath -AclObject $subAcl
}
```

## Security Considerations

- Use Azure Key Vault for sensitive configuration
- Configure approval gates for production deployments
- Restrict VM access to deployment service accounts
- Enable IIS request logging for audit trails
- Use HTTPS in production with valid SSL certificates

### Deployment Scripts Security

All deployment scripts in `eng/scripts/` implement security hardening:
- Input validation using parameter whitelists and allowed path roots
- Protection against path traversal attacks
- Strict mode and error handling
- No sensitive data logging
- Clear parameter definitions with mandatory/optional attributes

**IMPORTANT**: All changes to deployment scripts and pipeline configurations in `eng/` require code review by repository maintainers (enforced via `.github/CODEOWNERS`).
