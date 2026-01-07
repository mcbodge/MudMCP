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

### Public Repository Security

This repository is public. The following security controls are in place:

#### Pipeline Security Features

1. **PR Build Protection**: 
   - Deploy stages have explicit conditions that prevent PR validation builds from deploying
   - Only non-PR builds on `main` and `develop` branches can trigger deployments
   - See `condition:` blocks in `azure-pipelines.yaml` for implementation

2. **Emergency Deployment Control**:
   - Variable `deployEnabled` (default: `true`) provides a kill switch
   - Set to `false` in pipeline variables to disable all deployments immediately

3. **PowerShell Hardening**:
   - Strict mode enabled (`Set-StrictMode -Version Latest`)
   - Input validation on all file paths and configuration parameters
   - Path traversal protection on deployment directories
   - Reduced logging of sensitive data (paths, credentials, response bodies)

#### Required Azure DevOps Configuration

To maintain security in a public repository environment:

1. **Disable or Restrict Fork PR Builds**:
   - Go to Project Settings → Pipelines → Settings
   - Set "Build fork pull requests" to require approval
   - Or disable fork builds entirely if not needed
   - **Critical**: Prevents untrusted code from running in your pipeline

2. **Protect Secrets from PR Validation**:
   - Go to Pipelines → Library → Variable Groups
   - Ensure secret variables are NOT available to pull request validation builds
   - Use "Let user override" sparingly and only for non-secret variables
   - **Critical**: Prevents secret exfiltration via PR builds

3. **Environment Deployment Permissions**:
   - Go to Pipelines → Environments → Select environment → "..." → Security
   - Restrict "User permissions" to trusted maintainers only
   - Enable "Approvals and checks" for production environment
   - Add approvers who can review and approve deployments
   - **Critical**: Prevents unauthorized deployments

4. **Branch Policies and Protected Branches**:
   - Go to Repos → Branches → Select `main` branch → Branch policies
   - Require minimum number of reviewers (recommend 2 for production)
   - Require build validation (link your pipeline)
   - Disable "Allow requestors to approve their own changes"
   - Reset votes when new commits are pushed
   - **Critical**: Ensures code review before merge to deployment branches

5. **Use Azure Key Vault or Pipeline Secret Variables**:
   - Store connection strings, API keys, and credentials in Azure Key Vault
   - Reference via service connection in pipeline
   - Or use Pipeline Secret Variables (encrypted at rest)
   - **Never** commit secrets to source code or logs
   - **Critical**: Prevents credential exposure

6. **Restrict Pipeline Permissions**:
   - Go to Project Settings → Pipelines → Settings
   - Limit "Access token" scope to minimum required
   - Disable "Let scripts access OAuth token" unless specifically needed
   - Review service connection permissions regularly

7. **Enable Audit Logging**:
   - Monitor pipeline runs, approvals, and configuration changes
   - Review Azure DevOps audit logs regularly
   - Set up alerts for suspicious activity

### Security Checklist for Maintainers

Before enabling the pipeline on a new repository or Azure DevOps project:

- [ ] Fork PR builds disabled or require manual approval
- [ ] Secret variables NOT available to PR validation
- [ ] Environment deployment restricted to trusted users
- [ ] Approval gates configured for production environment
- [ ] Branch policies enabled on `main` and `develop`
- [ ] All secrets stored in Key Vault or secret variables
- [ ] Pipeline token scope limited to minimum required
- [ ] Deployment VM access restricted to service accounts
- [ ] IIS request logging enabled for audit trails
- [ ] HTTPS configured in production with valid certificates

### Threat Model

**Attack Vectors in Public Repositories**:

1. **Malicious PR Submission**: Attacker submits PR with code that exfiltrates secrets or deploys malicious code
   - **Mitigation**: PR builds cannot deploy (pipeline conditions), secrets not available to PR builds

2. **Compromised Contributor Account**: Attacker gains access to maintainer account
   - **Mitigation**: Require 2FA, multiple approvers for production, audit logging

3. **Pipeline Configuration Tampering**: Attacker modifies pipeline YAML to bypass controls
   - **Mitigation**: Branch policies require review, environment approvals as second gate

4. **Deployment VM Compromise**: Attacker gains access to deployment target
   - **Mitigation**: Least-privilege permissions, network isolation, regular patching

5. **Secret Leakage via Logs**: Secrets accidentally logged to build output
   - **Mitigation**: Reduced logging in scripts, secret variables masked automatically, log review

For detailed CI/CD security guidance, see [SECURITY.md](/SECURITY.md).

## Security Considerations (Legacy)

- Use Azure Key Vault for sensitive configuration
- Configure approval gates for production deployments
- Restrict VM access to deployment service accounts
- Enable IIS request logging for audit trails
- Use HTTPS in production with valid SSL certificates
