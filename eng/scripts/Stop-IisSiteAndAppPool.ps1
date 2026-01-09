# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Stops an IIS application pool gracefully.

.DESCRIPTION
    Stops the specified IIS application pool if it exists and is running.
    Waits for the pool to reach a stable state before attempting to stop it.

.PARAMETER AppPoolName
    The name of the IIS application pool to stop.

.EXAMPLE
    .\Stop-IisSiteAndAppPool.ps1 -AppPoolName "MudBlazorMcpPool"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$AppPoolName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

# Load shared utilities
. "$PSScriptRoot\Common\PathValidation.ps1"
. "$PSScriptRoot\Common\LoggingUtility.ps1"

# Validate app pool name
Test-IisResourceName -Name $AppPoolName -ResourceType 'app pool'

Import-Module WebAdministration -ErrorAction SilentlyContinue

# Check if app pool exists
if (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue) {
    $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    
    # Wait for pool to reach stable state first
    $stableStates = @('Started', 'Stopped')
    $timeout = 30
    $elapsed = 0
    while ($appPool -and $appPool.State -notin $stableStates -and $elapsed -lt $timeout) {
        Write-InfoLog "Waiting for app pool to reach stable state (current: $($appPool.State))..."
        Start-Sleep -Seconds 1
        $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        $elapsed++
    }
    
    if ($appPool -and $appPool.State -eq 'Started') {
        Write-InfoLog "Stopping application pool: $AppPoolName"
        Stop-WebAppPool -Name $AppPoolName
        
        # Wait for pool to stop
        $elapsed = 0
        $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        while ($appPool -and $appPool.State -ne 'Stopped' -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $elapsed++
            $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        }
        
        if (-not $appPool) {
            Write-InfoLog "Application pool no longer exists; assuming stopped."
        } else {
            Write-InfoLog "Application pool stopped."
        }
    } elseif (-not $appPool) {
        Write-InfoLog "Application pool no longer exists; nothing to stop."
    } else {
        Write-InfoLog "Application pool is already stopped."
    }
} else {
    Write-InfoLog "Application pool does not exist. Will be created during deployment."
}

exit 0
