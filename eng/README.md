# Engineering Folder

This folder contains Azure DevOps pipeline configurations for building, testing, and deploying the MudBlazor MCP Server.

## Pipeline Structure

```
eng/
├── azure-pipelines.yaml          # Main pipeline definition
├── templates/
│   └── deploy-iis.yaml           # Reusable IIS deployment template
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

The pipeline consists of three stages:

### 1. Build Stage
- Restores NuGet packages (with caching)
- Builds the solution in Release configuration
- Runs unit tests with code coverage
- Publishes the application
- Creates deployment artifact

### 2. Deploy to Development
- Triggers on `develop` branch
- Deploys to `mudblazor-mcp-dev` environment
- Uses IIS deployment template

### 3. Deploy to Production
- Triggers on `main` branch
- Deploys to `mudblazor-mcp-prod` environment
- Includes health checks and rollback notifications

## Prerequisites

### Azure DevOps Setup

1. **Create Environments**:
   - Go to Pipelines → Environments
   - Create `mudblazor-mcp-dev` for development
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

# Test health
.\eng\scripts\Test-DeploymentHealth.ps1 -Port 5180 -AppPoolName "MudBlazorMcpPool" -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp"
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

### Environment-Specific Settings

For production, create `appsettings.Production.json` on the server:

```json
{
  "MudBlazor": {
    "Repository": {
      "LocalPath": "C:\\ProgramData\\MudBlazorMcp\\mudblazor-repo"
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
- **Main branch**: Triggers Build + Deploy to Production

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
netstat -an | Select-String "5180"

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

> **⚠️ IMPORTANT**: Special security measures are in place to prevent unauthorized deployments from fork PRs and untrusted sources. See [SECURITY.md](/SECURITY.md) for the full threat model.

### Pipeline Security Model

The deployment pipeline implements defense-in-depth to protect against unauthorized deployments:

| Threat | Mitigation |
|--------|------------|
| Malicious fork PR deploys code | `Build.Reason` check blocks PR validation builds |
| Path traversal in deployment | Whitelist validation rejects out-of-scope paths |
| Secret leakage via logs | Secrets marked as secret, not logged |
| Injection via parameters | Strict regex validation on all inputs |
| Unauthorized deployment trigger | Kill switch + branch protection + environment approvals |

### Deployment Stage Protection

All deployment stages require **ALL** of these conditions:

```yaml
condition: |
  and(
    succeeded(),                                          # Build must pass
    eq(variables['deployEnabled'], 'true'),               # Kill switch enabled
    ne(variables['Build.Reason'], 'PullRequest'),         # Not a PR build
    eq(variables['Build.SourceBranch'], 'refs/heads/X')   # Protected branch only
  )
```

### Emergency Kill Switch

To immediately disable all deployments:

1. **Azure DevOps Portal**: Pipelines → Edit → Variables → Set `deployEnabled` = `false`
2. **Per-Run Override**: Run pipeline manually with `deployEnabled` = `false`

### Azure DevOps Environment Setup Checklist

#### Fork PR Security (Critical for Public Repos)

- [ ] **Disable fork PR builds** OR configure with limited permissions:
  - Project Settings → Repositories → Security
  - Limit build access for fork PRs
- [ ] **Require PR comments to trigger builds** from first-time contributors
- [ ] **Disable secret access** for fork PR builds

#### Environment Protection

- [ ] **Production environment approvals**: At least 1 required approver
- [ ] **Branch filters**: Production only from `refs/heads/main`
- [ ] **Exclusive locks**: Prevent concurrent deployments

#### Secret Protection

- [ ] Store secrets in **Variable Groups** marked as secret
- [ ] Use **Azure Key Vault integration** for production credentials
- [ ] **Never** reference secrets in script output/logs

#### Branch Policies (GitHub)

- [ ] Require PR reviews before merge to `main`/`develop`
- [ ] Require status checks to pass
- [ ] Require linear history (no merge commits from forks)

### Path Validation

All deployment scripts validate paths against an allowlist:

```powershell
# Allowed deployment roots (from PathValidation.ps1)
$script:DefaultAllowedRoots = @(
    'C:\inetpub',
    'D:\inetpub',
    'C:\wwwroot',
    'D:\wwwroot',
    'C:\WWW',
    'D:\WWW'
)
```

Paths outside these roots are rejected. Path traversal sequences (`..`) are detected and blocked **before** normalization.

### Deployment Scripts Security

All deployment scripts in `eng/scripts/` implement security hardening:

| Control | Implementation |
|---------|---------------|
| Strict mode | `Set-StrictMode -Version Latest` |
| Error handling | `$ErrorActionPreference = 'Stop'` |
| Path validation | `Get-ValidatedPath` with allowlist |
| Name validation | `Test-IisResourceName` with regex `^[a-zA-Z0-9_-]+$` |
| Traversal protection | Pre-normalization `..` detection |
| No secrets in output | Scripts don't log sensitive data |

### Security Review Checklist

Before merging changes to `eng/`:

- [ ] No new secret variables exposed in logs
- [ ] Deployment conditions unchanged or more restrictive
- [ ] Path parameters validated via `PathValidation.ps1`
- [ ] IIS names validated via `Test-IisResourceName`
- [ ] Pester tests pass for validation functions
- [ ] No dynamic command construction with user input

**IMPORTANT**: All changes to deployment scripts and pipeline configurations in `eng/` require code review by repository maintainers (enforced via `.github/CODEOWNERS`).

