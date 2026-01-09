# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Configures an IIS website and application pool.

.DESCRIPTION
    Creates or updates an IIS website and application pool with the specified settings.
    Configures the app pool for ASP.NET Core hosting.

.PARAMETER WebsiteName
    The name of the IIS website.

.PARAMETER AppPoolName
    The name of the IIS application pool.

.PARAMETER PhysicalPath
    Physical path for the website.

.PARAMETER Port
    HTTP port for the website (default: 5180).

.EXAMPLE
    .\Configure-IisWebsite.ps1 -WebsiteName "MudBlazorMcp" -AppPoolName "MudBlazorMcpPool" -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp" -Port 5180
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$WebsiteName,
    
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$AppPoolName,
    
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$PhysicalPath,
    
    [Parameter(Mandatory=$false)]
    [ValidateRange(1, 65535)]
    [int]$Port = 5180
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Load shared validation functions
. "$PSScriptRoot\Common\PathValidation.ps1"

# Validate names
Test-IisResourceName -Name $WebsiteName -ResourceType 'website'
Test-IisResourceName -Name $AppPoolName -ResourceType 'app pool'

# Validate and normalize physical path
$PhysicalPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName 'PhysicalPath'

Import-Module WebAdministration -ErrorAction SilentlyContinue
Import-Module IISAdministration -ErrorAction SilentlyContinue

# Create Application Pool if it doesn't exist
if (-not (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue)) {
    Write-Host "Creating application pool: $AppPoolName"
    New-WebAppPool -Name $AppPoolName
}

# Configure Application Pool
Write-Host "Configuring application pool..."
$appPool = Get-Item "IIS:\AppPools\$AppPoolName"
$appPool.managedRuntimeVersion = ""  # No managed code (use .NET Core hosting)
$appPool.startMode = "AlwaysRunning"
$appPool.processModel.idleTimeout = [TimeSpan]::FromMinutes(0)
$appPool | Set-Item

# Create Website if it doesn't exist
if (-not (Get-Website -Name $WebsiteName -ErrorAction SilentlyContinue)) {
    Write-Host "Creating website: $WebsiteName"
    New-Website -Name $WebsiteName `
                -PhysicalPath $PhysicalPath `
                -ApplicationPool $AppPoolName `
                -Port $Port
} else {
    # Update existing website
    Write-Host "Updating website: $WebsiteName"
    Set-ItemProperty "IIS:\Sites\$WebsiteName" -Name physicalPath -Value $PhysicalPath
    Set-ItemProperty "IIS:\Sites\$WebsiteName" -Name applicationPool -Value $AppPoolName
}

Write-Host "IIS configuration completed."
exit 0
