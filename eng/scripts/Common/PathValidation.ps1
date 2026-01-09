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
#>

# Default allowed IIS roots - can be overridden by scripts if needed
$script:DefaultAllowedRoots = @('C:\inetpub', 'C:\WWW', 'D:\WWW')

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
    Checks that a normalized path starts with one of the allowed root directories.

.PARAMETER Path
    The path to validate (should already be normalized via GetFullPath).

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
    
    $isAllowedPath = $false
    foreach ($root in $AllowedRoots) {
        if ($Path -like "$root\*" -or $Path -eq $root) {
            $isAllowedPath = $true
            break
        }
    }
    
    if (-not $isAllowedPath) {
        throw "$ParameterName must be under one of the allowed roots: $($AllowedRoots -join ', ')"
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
    $normalizedPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
    
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
