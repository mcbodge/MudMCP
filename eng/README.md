# Engineering Folder

This folder contains Azure DevOps pipeline configurations for building, testing, and deploying the MudBlazor MCP Server.

## Pipeline Structure

```
eng/
├── azure-pipelines.yaml          # Main pipeline definition
├── templates/
│   └── deploy-iis.yaml           # Reusable IIS deployment template
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

- Use Azure Key Vault for sensitive configuration
- Configure approval gates for production deployments
- Restrict VM access to deployment service accounts
- Enable IIS request logging for audit trails
- Use HTTPS in production with valid SSL certificates
