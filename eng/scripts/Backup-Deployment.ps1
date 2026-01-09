# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Creates a timestamped backup of the current deployment.

.DESCRIPTION
    Backs up the current deployment directory to a timestamped backup folder.
    Maintains only the last N backups (default: 3) to prevent disk space issues.

.PARAMETER PhysicalPath
    The physical path of the deployment to backup.

.PARAMETER MaxBackups
    Maximum number of backups to keep (default: 3).

.EXAMPLE
    .\Backup-Deployment.ps1 -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp" -MaxBackups 3
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$PhysicalPath,
    
    [Parameter(Mandatory=$false)]
    [ValidateRange(1, 10)]
    [int]$MaxBackups = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

# Load shared validation functions
. "$PSScriptRoot\Common\PathValidation.ps1"

# Validate and normalize the physical path
$PhysicalPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName 'PhysicalPath'

$backupPath = "${PhysicalPath}_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

if (Test-Path $PhysicalPath) {
    Write-Host "Creating backup at: $backupPath"
    Copy-Item -Path $PhysicalPath -Destination $backupPath -Recurse -Force
    Write-Host "Backup created successfully."
    
    # Keep only last N backups
    $parentPath = Split-Path $PhysicalPath
    $siteName = Split-Path $PhysicalPath -Leaf
    $backups = Get-ChildItem -Path $parentPath -Directory | 
               Where-Object { $_.Name -like "${siteName}_backup_*" } | 
               Sort-Object CreationTime -Descending | 
               Select-Object -Skip $MaxBackups
    
    if ($backups) {
        foreach ($backup in $backups) {
            Write-Host "Removing old backup: $($backup.FullName)"
            Remove-Item -Path $backup.FullName -Recurse -Force
        }
    }
} else {
    Write-Host "No existing deployment to backup."
}

exit 0
