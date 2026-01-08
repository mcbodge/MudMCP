# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Starts an IIS application pool and website.

.DESCRIPTION
    Starts the specified IIS application pool and website, waiting for the pool to start.

.PARAMETER AppPoolName
    The name of the IIS application pool to start.

.PARAMETER WebsiteName
    The name of the IIS website to start.

.EXAMPLE
    .\Start-IisSiteAndAppPool.ps1 -AppPoolName "MudBlazorMcpPool" -WebsiteName "MudBlazorMcp"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$AppPoolName,
    
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$WebsiteName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Validate names (alphanumeric, underscores, hyphens only)
if ($AppPoolName -notmatch '^[a-zA-Z0-9_-]+$') {
    Write-Error "Invalid app pool name. Only alphanumeric characters, underscores, and hyphens are allowed."
    exit 1
}

if ($WebsiteName -notmatch '^[a-zA-Z0-9_-]+$') {
    Write-Error "Invalid website name. Only alphanumeric characters, underscores, and hyphens are allowed."
    exit 1
}

Import-Module WebAdministration -ErrorAction SilentlyContinue

# Start App Pool
Write-Host "Starting application pool: $AppPoolName"
Start-WebAppPool -Name $AppPoolName

# Wait for pool to start
$timeout = 30
$elapsed = 0
$appPool = $null

while ($elapsed -lt $timeout) {
    $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    
    if ($null -eq $appPool) {
        Write-Host "Waiting for app pool to be available..."
    } elseif ($appPool.State -eq 'Started') {
        Write-Host "Application pool started successfully."
        break
    } else {
        Write-Host "Waiting for app pool to start (current state: $($appPool.State))..."
    }
    
    Start-Sleep -Seconds 1
    $elapsed++
}

if ($null -eq $appPool) {
    Write-Error "IIS application pool '$AppPoolName' was not found after $timeout seconds."
    exit 1
}

if ($appPool.State -ne 'Started') {
    Write-Error "IIS application pool '$AppPoolName' did not reach the 'Started' state within $timeout seconds."
    exit 1
}
# Start Website
Write-Host "Starting website: $WebsiteName"
Start-Website -Name $WebsiteName

Write-Host "IIS Application Pool and Website started successfully."
exit 0
