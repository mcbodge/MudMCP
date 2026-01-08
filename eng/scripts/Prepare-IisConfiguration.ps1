# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Prepares IIS configuration files for deployment.

.DESCRIPTION
    Creates web.config and logs directory for IIS hosting if they don't exist.

.PARAMETER PublishPath
    Path to the published application where web.config should be created.

.EXAMPLE
    .\Prepare-IisConfiguration.ps1 -PublishPath "C:\BuildAgent\_work\1\a\publish"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$PublishPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Normalize path
$PublishPath = $PublishPath.TrimEnd('\')

# Validate path exists
if (-not (Test-Path $PublishPath)) {
    Write-Error "Publish path does not exist: $PublishPath"
    exit 1
}

# Ensure path doesn't contain directory traversal or invalid characters
# Note: Colon (:) is excluded from validation as it's valid for Windows drive letters (e.g., C:\)
if ($PublishPath -match '\.\.' -or $PublishPath -match '[<>"|?*]') {
    Write-Error "Invalid characters or directory traversal detected in path."
    exit 1
}

# Resolve to full path for subsequent operations
# Note: This script is called during the build stage to prepare the artifact with web.config.
# Path restriction to IIS roots is NOT enforced here because:
# 1. During build, we're creating web.config in the artifact staging directory (e.g., D:\a\1\a\publish)
# 2. Deployment scripts (Deploy-IisContent.ps1) enforce path restrictions when copying to IIS
$PublishPath = [System.IO.Path]::GetFullPath($PublishPath)
# Create web.config if it doesn't exist in publish output
$webConfigPath = Join-Path $PublishPath "web.config"
if (-not (Test-Path $webConfigPath)) {
    $webConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet" arguments=".\MudBlazor.Mcp.dll" stdoutLogEnabled="true" stdoutLogFile=".\logs\stdout" hostingModel="InProcess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
"@
    $webConfig | Out-File -FilePath $webConfigPath -Encoding UTF8
    Write-Host "Created web.config for IIS hosting"
} else {
    Write-Host "web.config already exists"
}

# Create logs directory
$logsPath = Join-Path $PublishPath "logs"
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
    Write-Host "Created logs directory"
} else {
    Write-Host "Logs directory already exists"
}

exit 0
