# Security Policy

## Overview

MudMCP is an open-source project with a public repository. This document outlines our security practices, CI/CD threat model, and guidance for safe operation.

## Reporting Security Vulnerabilities

If you discover a security vulnerability in MudMCP, please report it privately:

1. **Do NOT** open a public issue for security vulnerabilities
2. Email the maintainers or use GitHub's private vulnerability reporting feature
3. Provide detailed information about the vulnerability and potential impact
4. Allow reasonable time for a fix before public disclosure

We will acknowledge receipt within 48 hours and provide a timeline for resolution.

## CI/CD Security Model

### Architecture

MudMCP uses Azure DevOps for CI/CD with the following security model:

```
Pull Request → Build + Test (No Deployment)
    ↓
Code Review + Approval
    ↓
Merge to develop → Build + Test + Deploy to Dev Environment
    ↓
Merge to main → Build + Test + Deploy to Production (with Approval Gate)
```

### Threat Model

#### Threats We Protect Against

1. **Malicious Pull Requests**
   - **Risk**: Attacker submits PR with code that exfiltrates secrets or deploys malicious code
   - **Controls**: 
     - PR builds cannot trigger deployment stages (explicit pipeline conditions)
     - Secrets are not available to PR validation builds
     - Code review required before merge

2. **Unauthorized Deployments**
   - **Risk**: Untrusted user or compromised account attempts to deploy
   - **Controls**:
     - Environment-based access control (only trusted users can deploy)
     - Approval gates for production deployments
     - Deployment only from protected branches (`main`, `develop`)

3. **Secret Leakage**
   - **Risk**: Secrets exposed via logs, artifacts, or error messages
   - **Controls**:
     - Secrets stored in Azure Key Vault or pipeline secret variables (masked in logs)
     - Reduced logging in deployment scripts (paths, credentials, responses redacted)
     - No secrets in source code or configuration files

4. **Path Traversal in Deployment**
   - **Risk**: Malicious deployment path writes files outside intended directory
   - **Controls**:
     - Input validation on all file paths
     - Whitelist of allowed deployment root directories
     - PowerShell strict mode prevents common mistakes

5. **Compromised Build Agent**
   - **Risk**: Attacker gains access to build agent or deployment VM
   - **Controls**:
     - Microsoft-hosted agents for builds (ephemeral, isolated)
     - Self-hosted deployment agents restricted to deployment operations
     - Least-privilege file system permissions on deployment targets

#### Threats We Do NOT Fully Protect Against

1. **Compromised Maintainer Account**: If a maintainer account with merge and approval rights is compromised, attacker can merge and deploy malicious code
   - **Partial Mitigation**: Require 2FA, multiple approvers, audit logging
   - **Residual Risk**: High-privilege accounts are always high-value targets

2. **Supply Chain Attacks**: Malicious dependencies (NuGet packages) introduced into the build
   - **Partial Mitigation**: Dependency vulnerability scanning (if configured)
   - **Residual Risk**: Zero-day vulnerabilities in dependencies

3. **Azure DevOps Platform Compromise**: If Azure DevOps itself is compromised
   - **Mitigation**: None (rely on Microsoft's security)
   - **Residual Risk**: Accept platform risk

## Pipeline Security Features

### Implemented Controls

#### 1. PR Build Isolation
- **Location**: `eng/azure-pipelines.yaml` deploy stage conditions
- **Control**: `ne(variables['Build.Reason'], 'PullRequest')`
- **Effect**: Deploy stages are skipped entirely for PR validation builds

#### 2. Branch Restrictions
- **Location**: `eng/azure-pipelines.yaml` deploy stage conditions
- **Control**: `eq(variables['Build.SourceBranch'], 'refs/heads/main')` or `refs/heads/develop`
- **Effect**: Only commits to protected branches can trigger deployment

#### 3. Emergency Kill Switch
- **Location**: `eng/azure-pipelines.yaml` variables section
- **Control**: `deployEnabled` variable (default: `true`)
- **Usage**: Set to `false` via pipeline variables UI to immediately disable all deployments

#### 4. PowerShell Script Hardening
- **Location**: `eng/templates/deploy-iis.yaml`
- **Controls**:
  - `Set-StrictMode -Version Latest`: Catches common scripting errors
  - `$ErrorActionPreference = 'Stop'`: Fail fast on errors
  - Input validation: Rejects invalid characters in names and paths
  - Path normalization: Uses `[System.IO.Path]::GetFullPath()` to resolve paths
  - Whitelist validation: Deployment paths must be under `C:\inetpub` or `D:\inetpub`

#### 5. Reduced Logging
- **Location**: `eng/templates/deploy-iis.yaml` diagnostic steps
- **Controls**:
  - Avoid logging full file paths where possible
  - Redact HTTP response bodies (may contain sensitive data)
  - Only log necessary information for troubleshooting

#### 6. Least-Privilege Permissions
- **Location**: `eng/templates/deploy-iis.yaml` permission step
- **Controls**:
  - App pool identity has Read/Execute on application root
  - App pool identity has Modify only on `logs` and `data` directories
  - Prevents application from modifying its own binaries or configuration

## Azure DevOps Configuration Requirements

### Essential Security Settings

To operate safely with a public repository, configure the following in Azure DevOps:

#### 1. Fork PR Build Protection
- **Setting**: Project Settings → Pipelines → Settings → "Build fork pull requests"
- **Recommended**: "Require approval" or "Disable"
- **Why**: Prevents untrusted forks from running pipeline code in your environment

#### 2. Secret Variable Protection
- **Setting**: Pipelines → Library → Variable Groups → Secret variables
- **Recommended**: Do NOT make secrets available to pull request validation
- **Why**: Prevents PR code from reading secrets via environment variables

#### 3. Environment Access Control
- **Setting**: Pipelines → Environments → `mudblazor-mcp-dev`, `mudblazor-mcp-prod` → Security
- **Recommended**:
  - Restrict "User permissions" to trusted maintainers only
  - Enable "Approvals and checks" with named approvers
- **Why**: Provides manual review gate before deployment

#### 4. Branch Policies
- **Setting**: Repos → Branches → `main`, `develop` → Branch policies
- **Recommended**:
  - Minimum 2 reviewers for `main`
  - Build validation required
  - Reset votes on new push
  - Disable self-approval
- **Why**: Ensures code review before merge to deployment branches

#### 5. Service Connections
- **Setting**: Project Settings → Service connections
- **Recommended**:
  - Use service principal with least-privilege permissions
  - Enable "Grant access to all pipelines" only for low-risk connections
  - Review connection usage regularly
- **Why**: Limits blast radius of compromised service connection

## Secure Operation Guidelines

### For Maintainers

#### Merging Pull Requests
1. Review all changes carefully, especially to `eng/` directory
2. Verify PR builds pass (build + test only, no deployment)
3. Check for suspicious file access, network calls, or obfuscation
4. Require 2 reviewers for changes to pipeline or deployment scripts
5. Do NOT merge PRs that modify security controls without thorough review

#### Approving Deployments
1. Verify the commit being deployed is expected
2. Review build logs for anomalies before approving
3. Check deployment approval context (branch, author, changes)
4. For production, ensure deploy-to-dev completed successfully first
5. Monitor application logs after deployment for unexpected behavior

#### Managing Secrets
1. Store all secrets in Azure Key Vault or pipeline secret variables
2. Rotate secrets regularly (every 90 days minimum)
3. Never commit secrets to source code, even in commented-out form
4. Use separate secrets for dev and production environments
5. Audit secret access via Azure Key Vault logs

#### Responding to Security Incidents
1. If deployment compromise is suspected:
   - Set `deployEnabled` variable to `false` immediately
   - Revoke approvals for all pending deployments
   - Investigate build and deployment logs
2. If secrets are leaked:
   - Rotate all potentially exposed secrets immediately
   - Review pipeline logs to determine scope of exposure
   - Notify users if user data was potentially exposed
3. If VM is compromised:
   - Isolate VM from network
   - Preserve logs and disk for forensics
   - Rebuild VM from clean base image
   - Rotate all secrets that VM had access to

### For Contributors

#### Submitting Pull Requests
1. Do NOT include secrets, credentials, or API keys in your PR
2. Do NOT modify pipeline or deployment scripts unless necessary
3. Explain security implications of changes in PR description
4. Test locally before submitting PR
5. Be responsive to security questions from reviewers

#### Reporting Security Issues
1. Use private communication channels for security vulnerabilities
2. Provide detailed reproduction steps
3. Suggest mitigations if possible
4. Allow maintainers reasonable time to fix before disclosure

## Security Best Practices for Deployment

### IIS Server Hardening

The deployment target VMs should follow these hardening practices:

1. **Least-Privilege Service Account**: App pool runs under virtual account (IIS AppPool\PoolName)
2. **File System Permissions**: Read-only on binaries, write only on logs/data directories
3. **HTTPS Only**: Configure SSL/TLS certificates, disable HTTP in production
4. **Firewall Rules**: Restrict inbound traffic to required ports only
5. **Windows Updates**: Enable automatic security updates
6. **Audit Logging**: Enable IIS request logging and Windows security auditing
7. **Antivirus**: Deploy enterprise antivirus with real-time scanning
8. **Network Isolation**: Place deployment VMs in restricted network segment

### Monitoring and Alerting

Set up monitoring for:

1. **Failed Deployments**: Alert on deployment stage failures
2. **Approval Bypasses**: Alert if deployment runs without expected approvals
3. **Secret Access**: Monitor Azure Key Vault access logs for anomalies
4. **Application Errors**: Monitor IIS logs and application exceptions
5. **Unauthorized Access Attempts**: Monitor Windows Security event log

## Compliance Considerations

If MudMCP is used in a regulated environment:

1. **Audit Trail**: Azure DevOps maintains audit logs of all pipeline runs and approvals
2. **Access Control**: Environment-based access control provides segregation of duties
3. **Change Management**: Branch policies and approvals provide change control
4. **Secret Management**: Azure Key Vault provides compliant secret storage
5. **Data Protection**: Ensure deployment does not log PII or sensitive data

Consult your compliance team for specific requirements (HIPAA, PCI-DSS, SOC 2, etc.).

## Security Review Checklist

Use this checklist when reviewing security-related changes:

- [ ] Pipeline conditions prevent PR builds from deploying
- [ ] Secrets are stored in Key Vault or secret variables (not source code)
- [ ] Any deployment scripts in this change validate and sanitize all inputs (leave unchecked if no deployment scripts are modified or added)
- [ ] Logging does not expose secrets, paths, or sensitive data
- [ ] File system permissions follow least-privilege principle
- [ ] Error messages do not expose internal system details
- [ ] Branch policies enforce code review on protected branches
- [ ] Environment approvals are configured for production
- [ ] Deployment paths are within whitelisted directories
- [ ] Dependencies have no known vulnerabilities (scan with `dotnet list package --vulnerable`)

## Version History

| Version | Date       | Changes                                      |
|---------|------------|----------------------------------------------|
| 1.0     | 2026-01-07 | Initial security policy and CI/CD threat model |

## References

- [Azure DevOps Security Best Practices](https://learn.microsoft.com/en-us/azure/devops/organizations/security/security-best-practices)
- [OWASP CI/CD Security Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/CI_CD_Security_Cheat_Sheet.html)
- [GitHub Actions Security Hardening](https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions)
- [ASP.NET Core Security Best Practices](https://learn.microsoft.com/en-us/aspnet/core/security/)

## Contact

For security questions or to report vulnerabilities, contact the maintainers via GitHub or email.

---

Last Updated: 2026-01-07
