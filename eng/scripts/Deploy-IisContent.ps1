# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Deploys application files to an IIS physical path.

.DESCRIPTION
    Copies application files from the artifact path to the IIS physical path.
    Preserves server-specific files like logs, data, and production configuration.

.PARAMETER ArtifactPath
    Path to the published artifact (source).

.PARAMETER PhysicalPath
    Physical path on the server where files should be deployed (destination).

.EXAMPLE
    .\Deploy-IisContent.ps1 -ArtifactPath "C:\Agent\_work\1\a\mudblazor-mcp-server" -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$ArtifactPath,
    
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$PhysicalPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Load shared validation functions
. "$PSScriptRoot\Common\PathValidation.ps1"

# Validate ArtifactPath security (IIS root validation happens below with dynamic CI roots)
Test-PathSecurity -Path $ArtifactPath -ParameterName 'ArtifactPath'

# Validate PhysicalPath with full IIS root validation
$PhysicalPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName 'PhysicalPath'

# Normalize ArtifactPath to canonical full path
try {
    $ArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath).TrimEnd('\')
}
catch {
    Write-Error "Failed to normalize 'ArtifactPath' path '$ArtifactPath': $_"
    exit 1
}

# Validate artifact path is under expected CI roots when available
$allowedArtifactRoots = @()
if ($env:PIPELINE_WORKSPACE) {
    $allowedArtifactRoots += $env:PIPELINE_WORKSPACE.TrimEnd('\')
}
if ($env:BUILD_ARTIFACTSTAGINGDIRECTORY) {
    $allowedArtifactRoots += $env:BUILD_ARTIFACTSTAGINGDIRECTORY.TrimEnd('\')
}

if ($allowedArtifactRoots.Count -gt 0) {
    $isArtifactPathAllowed = $false
    foreach ($root in $allowedArtifactRoots) {
        if ($ArtifactPath -like "$root\*" -or $ArtifactPath -eq $root) {
            $isArtifactPathAllowed = $true
            break
        }
    }

    if (-not $isArtifactPathAllowed) {
        Write-Error "Artifact path must be under one of the allowed roots: $($allowedArtifactRoots -join ', ')"
        exit 1
    }
}

# Validate artifact path exists
if (-not (Test-Path $ArtifactPath)) {
    Write-Error "Artifact path does not exist: $ArtifactPath"
    exit 1
}

# Find the actual source - could be directly in artifact or in a subfolder
# Look for the main DLL to determine correct source path (limit recursion depth for performance)
$mainDll = Get-ChildItem -Path $ArtifactPath -Filter "MudBlazor.Mcp.dll" -Recurse -Depth 3 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($mainDll -and $mainDll.DirectoryName) {
    $sourcePath = $mainDll.DirectoryName
} else {
    # Fallback to artifact root
    Write-Warning "Could not locate MudBlazor.Mcp.dll, falling back to artifact root."
    $sourcePath = $ArtifactPath
}

Write-Host "Deploying from: $sourcePath"
Write-Host "Deploying to: $PhysicalPath"

# Ensure destination directory exists
if (-not (Test-Path $PhysicalPath)) {
    New-Item -ItemType Directory -Path $PhysicalPath -Force | Out-Null
    Write-Host "Created destination directory."
}

# Clear existing files (except logs, data, and server-managed config)
# Note: appsettings.Production.json is excluded to preserve server-specific settings.
# This file should be manually managed on the server and not included in the artifact.
Get-ChildItem -Path $PhysicalPath -Exclude 'logs', 'data', 'appsettings.Production.json' | 
ForEach-Object {
    Remove-Item -Path $_.FullName -Recurse -Force
    Write-Host "Removed: $($_.Name)"
}

# Copy new files
Copy-Item -Path "$sourcePath\*" -Destination $PhysicalPath -Recurse -Force
Write-Host "Application files deployed successfully."

exit 0
