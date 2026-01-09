# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Common path validation functions for deployment scripts.

.DESCRIPTION
    Provides reusable functions for validating file system paths, including:
    - Security validation (path traversal, invalid characters)
    - IIS root validation
    - Name validation for IIS resources

.NOTES
    Dot-source this file at the beginning of deployment scripts:
    . "$PSScriptRoot\Common\PathValidation.ps1"
    
    ERROR HANDLING:
    These functions use 'throw' for validation failures, which creates terminating errors.
    When the calling script has $ErrorActionPreference = 'Stop' (recommended), thrown
    exceptions will terminate the script. This is intentional for security-critical
    validation - we want to fail fast and prevent any further execution with invalid paths.
    
    SECURITY:
    This module is critical for preventing path traversal attacks and unauthorized
    file system access during IIS deployments. See SECURITY.md for threat model.
#>

# Default allowed IIS roots - can be overridden by scripts if needed
# SECURITY: Restrict deployment paths to standard IIS directories only
$script:DefaultAllowedRoots = @(
    'C:\inetpub',
    'D:\inetpub',
    'C:\wwwroot',
    'D:\wwwroot',
    'C:\WWW',
    'D:\WWW'
)

<#
.SYNOPSIS
    Validates a path for security issues.

.DESCRIPTION
    Checks that a path is absolute, not a bare drive root, and doesn't contain
    path traversal sequences or invalid characters.

.PARAMETER Path
    The path to validate.

.PARAMETER ParameterName
    The name of the parameter (for error messages).

.EXAMPLE
    Test-PathSecurity -Path "C:\inetpub\wwwroot\MyApp" -ParameterName "PhysicalPath"
#>
function Test-PathSecurity {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        
        [Parameter(Mandatory=$true)]
        [string]$ParameterName
    )
    
    # Reject relative paths - must be absolute to avoid resolving against unpredictable CWD in CI/CD
    if (-not [System.IO.Path]::IsPathRooted($Path)) {
        throw "$ParameterName must be an absolute path. Relative paths are not supported."
    }
    
    # Reject bare drive roots like "C:" which would resolve against the current directory on that drive
    if ($Path -match '^[a-zA-Z]:$') {
        throw "$ParameterName must include at least one directory beyond the drive root (e.g. 'C:\path\to\folder')."
    }
    
    # Validate against path traversal and invalid characters BEFORE calling GetFullPath
    # This prevents bypass attacks where ".." sequences would be resolved away before validation.
    # Note: Colon (:) is intentionally excluded from the invalid character check because:
    # 1. It's required for Windows drive letters (e.g., C:\)
    # 2. Colons after the drive letter (e.g., C:\path:stream) would fail in GetFullPath anyway
    # 3. The IsPathRooted check ensures colons are used correctly for drive letters, and any misplaced colons
    #    (e.g., alternate data stream syntax like 'C:\path:stream') will fail during GetFullPath normalization
    if ($Path -match '\.\.' -or $Path -match '[<>"|?*]') {
        throw "Invalid characters or directory traversal detected in $ParameterName."
    }
}

<#
.SYNOPSIS
    Validates that a path is under an allowed IIS root.

.DESCRIPTION
    Checks that a path starts with one of the allowed root directories.
    The path will be normalized using [System.IO.Path]::GetFullPath before validation.

.PARAMETER Path
    The path to validate. May be normalized or non-normalized; it will be normalized internally using GetFullPath.

.PARAMETER AllowedRoots
    Array of allowed root paths. Defaults to C:\inetpub, C:\WWW, D:\WWW.

.PARAMETER ParameterName
    The name of the parameter (for error messages).

.EXAMPLE
    Test-AllowedRoot -Path "C:\inetpub\wwwroot\MyApp" -ParameterName "PhysicalPath"
#>
function Test-AllowedRoot {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        
        [Parameter(Mandatory=$false)]
        [string[]]$AllowedRoots = $script:DefaultAllowedRoots,
        
        [Parameter(Mandatory=$true)]
        [string]$ParameterName
    )
    
    # Normalize input path to a canonical absolute form without trailing slashes.
    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')

    # Normalize allowed roots once before comparison to avoid representation mismatches.
    $normalizedAllowedRoots = @()
    foreach ($root in $AllowedRoots) {
        if ([string]::IsNullOrWhiteSpace($root)) {
            continue
        }
        try {
            $normalizedRoot = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
            $normalizedAllowedRoots += $normalizedRoot
        }
        catch {
            # Skip malformed allowed roots but log a warning - this indicates a configuration issue
            Write-Warning "Skipping invalid allowed root '$root': $($_.Exception.Message)"
        }
    }
    
    if ($normalizedAllowedRoots.Count -eq 0) {
        throw "No valid allowed roots configured. Cannot validate $ParameterName."
    }
    
    $isAllowedPath = $false
    foreach ($root in $normalizedAllowedRoots) {
        if ($normalizedPath -like "$root\*" -or $normalizedPath -eq $root) {
            $isAllowedPath = $true
            break
        }
    }
    
    if (-not $isAllowedPath) {
        throw "$ParameterName must be under one of the allowed roots: $($normalizedAllowedRoots -join ', ')"
    }
}

<#
.SYNOPSIS
    Validates and normalizes a physical path for IIS deployment.

.DESCRIPTION
    Performs security validation, normalizes the path, and validates it's under an allowed root.
    Returns the normalized path.

.PARAMETER Path
    The path to validate and normalize.

.PARAMETER AllowedRoots
    Array of allowed root paths. Defaults to C:\inetpub, C:\WWW, D:\WWW.

.PARAMETER ParameterName
    The name of the parameter (for error messages).

.EXAMPLE
    $normalizedPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName "PhysicalPath"
#>
function Get-ValidatedPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path,
        
        [Parameter(Mandatory=$false)]
        [string[]]$AllowedRoots = $script:DefaultAllowedRoots,
        
        [Parameter(Mandatory=$true)]
        [string]$ParameterName
    )
    
    # Security validation on raw input
    Test-PathSecurity -Path $Path -ParameterName $ParameterName
    
    # Normalize to canonical full path
    try {
        $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
    }
    catch {
        throw "Invalid path value for parameter '$ParameterName'. The provided path '$Path' is malformed and could not be resolved to a full path: $($_.Exception.Message)"
    }
    
    # Validate against allowed roots
    Test-AllowedRoot -Path $normalizedPath -AllowedRoots $AllowedRoots -ParameterName $ParameterName
    
    return $normalizedPath
}

<#
.SYNOPSIS
    Validates an IIS resource name.

.DESCRIPTION
    Checks that a name contains only alphanumeric characters, underscores, and hyphens.

.PARAMETER Name
    The name to validate.

.PARAMETER ResourceType
    The type of resource (for error messages), e.g., "website", "app pool".

.EXAMPLE
    Test-IisResourceName -Name "MudBlazorMcp" -ResourceType "website"
#>
function Test-IisResourceName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Name,
        
        [Parameter(Mandatory=$true)]
        [string]$ResourceType
    )
    
    if ($Name -notmatch '^[a-zA-Z0-9_-]+$') {
        throw "Invalid $ResourceType name '$Name'. Only alphanumeric characters, underscores, and hyphens are allowed."
    }
}
