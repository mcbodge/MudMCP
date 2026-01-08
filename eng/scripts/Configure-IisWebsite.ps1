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

# Validate names (alphanumeric, underscores, hyphens only)
if ($WebsiteName -notmatch '^[a-zA-Z0-9_-]+$') {
    Write-Error "Invalid website name. Only alphanumeric characters, underscores, and hyphens are allowed."
    exit 1
}

if ($AppPoolName -notmatch '^[a-zA-Z0-9_-]+$') {
    Write-Error "Invalid app pool name. Only alphanumeric characters, underscores, and hyphens are allowed."
    exit 1
}

# Normalize and validate physical path
$PhysicalPath = $PhysicalPath.TrimEnd('\')

$allowedRoots = @('C:\inetpub', 'C:\WWW', 'D:\WWW')
$isAllowedPath = $false
foreach ($root in $allowedRoots) {
    if ($PhysicalPath -like "$root\*" -or $PhysicalPath -eq $root) {
        $isAllowedPath = $true
        break
    }
}

if (-not $isAllowedPath) {
    Write-Error "Physical path must be under one of the allowed roots: $($allowedRoots -join ', ')"
    exit 1
}

if ($PhysicalPath -match '\.\.' -or $PhysicalPath -match '[<>"|?*:]') {
    Write-Error "Invalid characters or directory traversal detected in path."
    exit 1
}

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
