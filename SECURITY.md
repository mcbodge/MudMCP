# Security Policy

This document describes the security measures implemented in the MudBlazor MCP Server project, particularly around CI/CD pipeline security to protect against unauthorized deployments.

## Reporting Security Vulnerabilities

If you discover a security vulnerability, please report it by:

1. **DO NOT** open a public GitHub issue
2. Email the maintainers directly (see repository contacts)
3. Include a detailed description and reproduction steps
4. Allow reasonable time for a fix before public disclosure

## Architecture Security Model

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                          PUBLIC GITHUB REPOSITORY                           │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐         │
│  │   Source Code   │    │   Pull Request  │    │   Fork PR       │         │
│  │   (Trusted)     │    │   (Semi-trusted)│    │   (Untrusted)   │         │
│  └────────┬────────┘    └────────┬────────┘    └────────┬────────┘         │
│           │                      │                      │                   │
└───────────┼──────────────────────┼──────────────────────┼───────────────────┘
            │                      │                      │
            ▼                      ▼                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            CI/CD PIPELINE                                   │
│                                                                             │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                         BUILD STAGE                                   │  │
│  │  • Runs for ALL triggers (CI, PR, Fork PR)                           │  │
│  │  • Microsoft-hosted agents (isolated)                                 │  │
│  │  • No access to deployment secrets                                    │  │
│  │  • Produces artifacts only                                            │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                    │                                        │
│               ┌────────────────────┼────────────────────┐                  │
│               │                    │                    │                  │
│               ▼                    ▼                    ▼                  │
│  ┌─────────────────┐   ┌─────────────────┐   ┌─────────────────┐          │
│  │  Deploy Dev     │   │  Deploy Prod    │   │  BLOCKED        │          │
│  │  ────────────   │   │  ────────────   │   │  ────────────   │          │
│  │  Branch: develop│   │  Branch: main   │   │  Fork PRs       │          │
│  │  Reason: CI     │   │  Reason: CI     │   │  External PRs   │          │
│  │  Kill switch: ✓ │   │  Kill switch: ✓ │   │  Invalid branch │          │
│  │  Approvals: Opt │   │  Approvals: Req │   │                 │          │
│  └─────────────────┘   └─────────────────┘   └─────────────────┘          │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Threat Model

### Threat 1: Malicious Fork PR Deploys Code

**Attack Vector**: Attacker forks repository, adds malicious code, submits PR. Pipeline builds and deploys to production.

**Mitigations**:
1. **Build.Reason check**: Deployment stages require `ne(variables['Build.Reason'], 'PullRequest')`
2. **Branch protection**: Only `refs/heads/main` and `refs/heads/develop` can deploy
3. **Environment approvals**: Production requires manual approval via environment gates
4. **Artifact separation**: Build stage produces artifacts but has no deployment access

**Residual Risk**: LOW - Multiple layers must all fail simultaneously

### Threat 2: Path Traversal in Deployment Scripts

**Attack Vector**: Manipulated path parameter escapes allowed directories (e.g., `C:\inetpub\..\Windows\System32`)

**Mitigations**:
1. **Pre-normalization validation**: `Test-PathSecurity` validates before `GetFullPath()` resolves `..`
2. **Whitelist validation**: Paths must be under allowed roots:
   - `C:\inetpub`, `D:\inetpub`
   - `C:\wwwroot`, `D:\wwwroot`
   - `C:\WWW`, `D:\WWW`
3. **Invalid character rejection**: Blocks `<`, `>`, `|`, `"`, `?`, `*`, `..`
4. **Absolute path requirement**: Relative paths rejected to prevent CWD-relative attacks

**Residual Risk**: LOW - Defense in depth with validation before and after normalization

### Threat 3: Secret Leakage via Logs

**Attack Vector**: Pipeline logs expose secrets, connection strings, or internal paths

**Mitigations**:
1. **Secret masking**: Variables marked as secret are auto-masked in logs
2. **Redacted logging**: `LoggingUtility.ps1` redacts file paths and sensitive patterns
3. **No secrets in scripts**: Scripts never reference secret variables directly
4. **HTTP content truncation**: Response bodies truncated and secret patterns removed

**Residual Risk**: LOW - Multiple redaction layers in place

### Threat 4: Parameter Injection

**Attack Vector**: Malicious input in IIS website/app pool names executes commands

**Mitigations**:
1. **Strict regex validation**: IIS names restricted to `^[a-zA-Z0-9_-]+$`
2. **ValidateNotNullOrEmpty**: PowerShell rejects null/empty parameters
3. **Parameter type safety**: Strong typing prevents type confusion
4. **No dynamic command construction**: Parameters never interpolated into commands

**Residual Risk**: LOW - Whitelist approach prevents injection

### Threat 5: Unauthorized Deployment Trigger

**Attack Vector**: Attacker triggers deployment outside normal workflow

**Mitigations**:
1. **Kill switch**: `deployEnabled` variable can disable all deployments instantly
2. **Branch conditions**: Hardcoded branch requirements in pipeline YAML
3. **Environment permissions**: Environment configuration restricts who can deploy
4. **Audit trail**: All pipeline runs logged with user/trigger information

**Residual Risk**: LOW - Multiple authorization layers

## Pipeline Security Controls

### Deployment Stage Conditions

All deployment stages require ALL of the following:

```yaml
condition: |
  and(
    succeeded(),                                          # Build must pass
    eq(variables['deployEnabled'], 'true'),               # Kill switch enabled
    ne(variables['Build.Reason'], 'PullRequest'),         # Not a PR build
    eq(variables['Build.SourceBranch'], 'refs/heads/X')   # Protected branch
  )
```

### Emergency Kill Switch

To immediately disable all deployments:

1. Go to pipeline settings → Variables
2. Set `deployEnabled` to `false`
3. All subsequent runs will skip deployment stages

Or override per-run:
1. Run pipeline manually
2. Set `deployEnabled` to `false` in run parameters

### Environment Configuration

**Development Environment** (`mudblazor-mcp-dev`):
- Approvals: Optional (can be enabled)
- Branch filter: `refs/heads/develop`
- Deployment: Self-hosted VM agent

**Production Environment** (`mudblazor-mcp-prod`):
- Approvals: **REQUIRED** - at least one approver
- Branch filter: `refs/heads/main`
- Deployment: Self-hosted VM agent
- Business hours: Consider restricting deployment windows

## PowerShell Script Security

### Required Headers

All deployment scripts must include:

```powershell
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'  # Or 'Continue' for diagnostic scripts

# Load shared modules
. "$PSScriptRoot\Common\PathValidation.ps1"
. "$PSScriptRoot\Common\LoggingUtility.ps1"
```

### Redacted Logging Pattern

Use `LoggingUtility.ps1` functions instead of `Write-Host`/`Write-Warning`:

```powershell
# Instead of: Write-Host "Deploying to $PhysicalPath"
Write-InfoLog "Deploying to $PhysicalPath"  # Outputs: Deploying to [WEBROOT]\...

# For HTTP responses (truncated + secrets redacted)
$safeContent = Get-RedactedHttpContent -Content $response.Content -MaxLength 200
Write-InfoLog "Response: $safeContent"
```

### Path Validation Pattern

```powershell
# Load shared validation
. "$PSScriptRoot\Common\PathValidation.ps1"

# Validate IIS resource names
Test-IisResourceName -Name $AppPoolName -ResourceType 'app pool'

# Validate and normalize paths
$PhysicalPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName 'PhysicalPath'
```

### Allowed Roots Configuration

Default allowed roots (defined in `PathValidation.ps1`):

```powershell
$script:DefaultAllowedRoots = @(
    'C:\inetpub',
    'D:\inetpub',
    'C:\wwwroot',
    'D:\wwwroot',
    'C:\WWW',
    'D:\WWW'
)
```

To use custom roots:

```powershell
$customRoots = @('E:\WebApps', 'F:\Sites')
$normalizedPath = Get-ValidatedPath -Path $inputPath -AllowedRoots $customRoots -ParameterName 'DeployPath'
```

## Security Checklist for Code Reviews

### Pipeline Changes (`eng/azure-pipelines.yaml`)

- [ ] No new secret variables exposed in logs
- [ ] Deployment conditions unchanged or more restrictive
- [ ] No new `PullRequest` triggers for deployment stages
- [ ] Kill switch variable still referenced in conditions
- [ ] Branch filters match protected branches only

### Script Changes (`eng/scripts/*.ps1`)

- [ ] `Set-StrictMode -Version Latest` present
- [ ] `$ErrorActionPreference` explicitly set
- [ ] Path parameters validated via `PathValidation.ps1`
- [ ] IIS names validated via `Test-IisResourceName`
- [ ] Uses `LoggingUtility.ps1` for output (not raw `Write-Host`)
- [ ] No dynamic command construction with user input
- [ ] No secret values logged or displayed
- [ ] New parameters have `[ValidateNotNullOrEmpty()]`

### Pester Test Changes (`eng/scripts/Common/*.Tests.ps1`)

- [ ] Security-critical validation has test coverage
- [ ] Path traversal scenarios tested
- [ ] Edge cases (empty, null, special chars) tested
- [ ] Tests pass locally before merge

## Incident Response

### Suspected Credential Leak

1. **Immediately** rotate affected credentials
2. Disable deployments: Set `deployEnabled` to `false`
3. Review pipeline audit logs for unauthorized access
4. Check deployment logs for anomalies
5. Document incident timeline and remediation

### Suspected Malicious Deployment

1. Disable deployments: Set `deployEnabled` to `false`
2. Roll back to known-good deployment:
   ```powershell
   # On target server
   .\eng\scripts\Restore-Deployment.ps1 -BackupPath "C:\inetpub\backups\MudBlazorMcp\<timestamp>"
   ```
3. Review deployment logs and compare artifacts
4. Check file integrity on target server
5. Document and report to maintainers

### Security Patch Deployment

1. Create fix on protected branch (not fork)
2. Ensure all Pester tests pass
3. Request expedited review from CODEOWNERS
4. Deploy to Dev first, verify
5. Deploy to Prod with approval

## Compliance Notes

- **Audit Trail**: All pipeline runs logged with full history
- **Access Control**: Organization access controlled via identity provider
- **Secret Management**: Use secure vault for production secrets
- **Data Protection**: No PII stored or processed by deployment pipeline

## CODEOWNERS Protection

The `.github/CODEOWNERS` file enforces maintainer review for security-critical paths:

| Path | Protection Reason |
|------|-------------------|
| `SECURITY.md` | Security documentation |
| `eng/azure-pipelines.yaml` | Pipeline conditions and deployment logic |
| `eng/templates/**` | Deployment templates |
| `eng/scripts/**` | Deployment scripts with path validation |
| `.github/**` | GitHub configuration including CODEOWNERS itself |
| `nuget.config`, `Directory.*.props` | Supply chain configuration |

**Note**: CODEOWNERS requires branch protection rules to be enabled in GitHub repository settings.

## References

- [OWASP CI/CD Security Guidelines](https://owasp.org/www-project-ci-cd-security/)
- [PowerShell Security Best Practices](https://docs.microsoft.com/en-us/powershell/scripting/security)
