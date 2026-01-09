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

# Normalize path separators first (convert forward slashes to backslashes)
$ArtifactPath = $ArtifactPath.Replace('/', '\')
$PhysicalPath = $PhysicalPath.Replace('/', '\')

# Reject relative paths - must be absolute to avoid resolving against unpredictable CWD in CI/CD
if (-not [System.IO.Path]::IsPathRooted($ArtifactPath)) {
    Write-Error "ArtifactPath must be an absolute path. Relative paths are not supported."
    exit 1
}
if (-not [System.IO.Path]::IsPathRooted($PhysicalPath)) {
    Write-Error "PhysicalPath must be an absolute path. Relative paths are not supported."
    exit 1
}

# Reject bare drive roots like "C:" which would resolve against the current directory on that drive
if ($ArtifactPath -match '^[a-zA-Z]:$') {
    Write-Error "ArtifactPath must include at least one directory beyond the drive root (e.g. 'C:\\path\\to\\artifact')."
    exit 1
}
if ($PhysicalPath -match '^[a-zA-Z]:$') {
    Write-Error "PhysicalPath must include at least one directory beyond the drive root (e.g. 'C:\\inetpub\\wwwroot\\MudBlazorMcp')."
    exit 1
}
# Validate against path traversal and invalid characters BEFORE calling GetFullPath
# This prevents bypass attacks where ".." sequences would be resolved away before validation
if ($ArtifactPath -match '\.\.' -or $ArtifactPath -match '[<>"|?*]') {
    Write-Error "Invalid characters or directory traversal detected in artifact path."
    exit 1
}
if ($PhysicalPath -match '\.\.' -or $PhysicalPath -match '[<>"|?*]') {
    Write-Error "Invalid characters or directory traversal detected in physical path."
    exit 1
}

# Now safe to resolve to canonical full paths for consistent comparisons
$ArtifactPath = [System.IO.Path]::GetFullPath($ArtifactPath).TrimEnd('\')
$PhysicalPath = [System.IO.Path]::GetFullPath($PhysicalPath).TrimEnd('\')

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

# Validate physical path is an allowed root
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
