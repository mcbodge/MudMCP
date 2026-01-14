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

# Load shared utilities
. "$PSScriptRoot\Common\PathValidation.ps1"
. "$PSScriptRoot\Common\LoggingUtility.ps1"

# Validate names
Test-IisResourceName -Name $AppPoolName -ResourceType 'app pool'
Test-IisResourceName -Name $WebsiteName -ResourceType 'website'

Import-Module WebAdministration -ErrorAction SilentlyContinue

# Start App Pool
Write-InfoLog "Starting application pool: $AppPoolName"
Start-WebAppPool -Name $AppPoolName

# Wait for pool to start
$timeout = 30
$elapsed = 0
$appPool = $null

while ($elapsed -lt $timeout) {
    $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    
    if ($null -eq $appPool) {
        Write-InfoLog "Waiting for app pool to be available..."
    } elseif ($appPool.State -eq 'Started') {
        Write-InfoLog "Application pool started successfully."
        break
    } else {
        Write-InfoLog "Waiting for app pool to start (current state: $($appPool.State))..."
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
Write-InfoLog "Starting website: $WebsiteName"
Start-Website -Name $WebsiteName

Write-InfoLog "IIS Application Pool and Website started successfully."
exit 0
