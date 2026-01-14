# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Stops an IIS website and application pool gracefully.

.DESCRIPTION
    Stops the specified IIS website and application pool if they exist and are running.
    The website is stopped first to release port bindings, then the application pool.
    Waits for resources to reach stable states before attempting to stop them.

.PARAMETER AppPoolName
    The name of the IIS application pool to stop.

.PARAMETER WebsiteName
    The name of the IIS website to stop.

.EXAMPLE
    .\Stop-IisSiteAndAppPool.ps1 -AppPoolName "MudBlazorMcpPool" -WebsiteName "MudBlazorMcp"
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
$ErrorActionPreference = 'Continue'

# Validate app pool name (alphanumeric, underscores, hyphens only)
if ($AppPoolName -notmatch '^[a-zA-Z0-9_-]+$') {
    Write-Error "Invalid app pool name. Only alphanumeric characters, underscores, and hyphens are allowed."
    exit 1
}

# Validate website name (alphanumeric, underscores, hyphens only)
if ($WebsiteName -notmatch '^[a-zA-Z0-9_-]+$') {
    Write-Error "Invalid website name. Only alphanumeric characters, underscores, and hyphens are allowed."
    exit 1
}

Import-Module WebAdministration -ErrorAction SilentlyContinue

# ============================================
# Stop Website First (to release port bindings)
# ============================================
$website = Get-Website -Name $WebsiteName -ErrorAction SilentlyContinue
if ($website) {
    if ($website.State -eq 'Started') {
        Write-Host "Stopping website: $WebsiteName"
        Stop-Website -Name $WebsiteName
        
        # Wait for website to stop
        $timeout = 30
        $elapsed = 0
        $website = Get-Website -Name $WebsiteName -ErrorAction SilentlyContinue
        while ($website -and $website.State -ne 'Stopped' -and $elapsed -lt $timeout) {
            Start-Sleep -Seconds 1
            $elapsed++
            $website = Get-Website -Name $WebsiteName -ErrorAction SilentlyContinue
        }
        
        if ($website -and $website.State -eq 'Stopped') {
            Write-Host "Website stopped successfully."
        } elseif (-not $website) {
            Write-Host "Website no longer exists; assuming stopped."
        } else {
            Write-Warning "Website did not stop within $timeout seconds (current state: $($website.State))."
        }
    } else {
        Write-Warning "Website '$WebsiteName' was already stopped (state: $($website.State))."
    }
} else {
    Write-Host "Website '$WebsiteName' does not exist. Will be created during deployment."
}

# ============================================
# Stop Application Pool
# ============================================
if (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue) {
    $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    
    # Wait for pool to reach stable state first
    $stableStates = @('Started', 'Stopped')
    $timeout = 30
    $elapsed = 0
    while ($appPool -and $appPool.State -notin $stableStates -and $elapsed -lt $timeout) {
        Write-Host "Waiting for app pool to reach stable state (current: $($appPool.State))..."
        Start-Sleep -Seconds 1
        $appPool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        $elapsed++
    }
    
    if ($appPool -and $appPool.State -eq 'Started') {
        Write-Host "Stopping application pool: $AppPoolName"
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
            Write-Host "Application pool no longer exists; assuming stopped."
        } else {
            Write-Host "Application pool stopped."
        }
    } elseif (-not $appPool) {
        Write-Host "Application pool no longer exists; nothing to stop."
    } else {
        Write-Warning "Application pool '$AppPoolName' was already stopped (state: $($appPool.State))."
    }
} else {
    Write-Host "Application pool '$AppPoolName' does not exist. Will be created during deployment."
}

exit 0
