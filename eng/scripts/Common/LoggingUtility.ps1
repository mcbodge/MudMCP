# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Logging utility functions for deployment scripts.

.DESCRIPTION
    Provides functions for logging messages with path redaction to prevent
    sensitive information disclosure in CI/CD logs.

.NOTES
    Dot-source this file at the beginning of deployment scripts:
    . "$PSScriptRoot\Common\LoggingUtility.ps1"
    
    SECURITY:
    This module redacts file system paths to prevent leaking server directory
    structure in public CI logs. See SECURITY.md for more information.
#>

# Paths that should be redacted in logs for security
# These are replaced with friendly labels to maintain log readability
#
# REPLACEMENT ORDER (critical for correct behavior):
# 1. Static IIS paths (most specific first: wwwroot before inetpub)
# 2. Dynamic CI/CD environment variable paths
# 3. Generic fallback for any remaining absolute paths
#
# EDGE CASES:
# - Environment variable paths must include trailing backslash to match correctly
#   (e.g., 'D:\a\1\' matches 'D:\a\1\artifacts' but not 'D:\a\1artifacts')
# - If an environment variable overlaps with a static path (e.g., PIPELINE_WORKSPACE='C:\inetpub\temp'),
#   the static path replacement wins because it runs first
$script:PathRedactions = [ordered]@{
    # IIS deployment paths - more specific first
    # Keys are literal paths (not regex) - they get escaped when used
    'C:\inetpub\wwwroot\' = '[WEBROOT]\'
    'D:\inetpub\wwwroot\' = '[WEBROOT]\'
    'C:\wwwroot\' = '[WEBROOT]\'
    'D:\wwwroot\' = '[WEBROOT]\'
    'C:\WWW\' = '[WEBROOT]\'
    'D:\WWW\' = '[WEBROOT]\'
    # Less specific IIS paths
    'C:\inetpub\' = '[IIS]\'
    'D:\inetpub\' = '[IIS]\'
}

<#
.SYNOPSIS
    Redacts sensitive paths from a message string.

.DESCRIPTION
    Replaces known sensitive paths with redacted placeholders.
    Also redacts dynamic CI/CD environment paths if available.

.PARAMETER Message
    The message to redact.

.EXAMPLE
    $safeMessage = Get-RedactedMessage -Message "Deploying to C:\inetpub\wwwroot\MyApp"
    # Returns: "Deploying to [WEBROOT]\MyApp"
#>
function Get-RedactedMessage {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory=$true, ValueFromPipeline=$true)]
        [AllowEmptyString()]
        [string]$Message
    )
    
    process {
        if ([string]::IsNullOrEmpty($Message)) {
            return $Message
        }
        
        $redacted = $Message
        
        # IMPORTANT: Apply static IIS path redactions FIRST (before generic fallback)
        # This ensures specific patterns like [WEBROOT] are preserved
        foreach ($path in $script:PathRedactions.Keys) {
            $redacted = $redacted -replace [regex]::Escape($path), $script:PathRedactions[$path]
        }
        
        # Redact CI/CD pipeline paths (dynamic, from environment variables)
        if ($env:PIPELINE_WORKSPACE) {
            $pipelinePath = $env:PIPELINE_WORKSPACE.TrimEnd('\') + '\'
            $redacted = $redacted -replace [regex]::Escape($pipelinePath), '[PIPELINE]\'
        }
        if ($env:BUILD_ARTIFACTSTAGINGDIRECTORY) {
            $artifactPath = $env:BUILD_ARTIFACTSTAGINGDIRECTORY.TrimEnd('\') + '\'
            $redacted = $redacted -replace [regex]::Escape($artifactPath), '[ARTIFACTS]\'
        }
        if ($env:BUILD_SOURCESDIRECTORY) {
            $sourcePath = $env:BUILD_SOURCESDIRECTORY.TrimEnd('\') + '\'
            $redacted = $redacted -replace [regex]::Escape($sourcePath), '[SOURCE]\'
        }
        if ($env:AGENT_TEMPDIRECTORY) {
            $tempPath = $env:AGENT_TEMPDIRECTORY.TrimEnd('\') + '\'
            $redacted = $redacted -replace [regex]::Escape($tempPath), '[TEMP]\'
        }
        
        # LAST: Redact any remaining absolute paths that look like server paths
        # This catches paths not in our known list but still potentially sensitive
        # Pattern: Drive letter followed by backslash and path segments (but NOT already redacted)
        # The negative lookbehind specifically checks for our redaction token prefixes to avoid
        # double-redaction. This is more precise than just checking for '[' which could match
        # legitimate text like "File not found] C:\path".
        $redacted = $redacted -replace '(?<!\[(?:WEBROOT|IIS|PATH|PIPELINE|ARTIFACTS|SOURCE|TEMP)\\)([A-Z]):\\(?:[^\\]+\\)+', '[PATH]\'
        
        return $redacted
    }
}

<#
.SYNOPSIS
    Writes an informational message with path redaction.

.DESCRIPTION
    Writes to the host with sensitive paths redacted.

.PARAMETER Message
    The message to write.

.EXAMPLE
    Write-InfoLog -Message "Deploying to $PhysicalPath"
#>
function Write-InfoLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$Message
    )
    
    Write-Host (Get-RedactedMessage -Message $Message)
}

<#
.SYNOPSIS
    Writes a warning message with path redaction.

.DESCRIPTION
    Writes a warning with sensitive paths redacted.

.PARAMETER Message
    The message to write.

.EXAMPLE
    Write-WarnLog -Message "Path not found: $FilePath"
#>
function Write-WarnLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$Message
    )
    
    Write-Warning (Get-RedactedMessage -Message $Message)
}

<#
.SYNOPSIS
    Writes an error message with path redaction.

.DESCRIPTION
    Writes an error with sensitive paths redacted.

.PARAMETER Message
    The message to write.

.EXAMPLE
    Write-ErrorLog -Message "Failed to access $FilePath"
#>
function Write-ErrorLog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true, Position=0)]
        [string]$Message
    )
    
    Write-Error (Get-RedactedMessage -Message $Message)
}

<#
.SYNOPSIS
    Gets a redacted path for display purposes.

.DESCRIPTION
    Returns a redacted version of a path suitable for logging.
    Preserves the last segment (filename or folder name) for context.

.PARAMETER Path
    The path to redact.

.EXAMPLE
    $displayPath = Get-RedactedPath -Path "C:\inetpub\wwwroot\MyApp\web.config"
    # Returns: "[WEBROOT]\MyApp\web.config"
#>
function Get-RedactedPath {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory=$true)]
        [string]$Path
    )
    
    return Get-RedactedMessage -Message $Path
}

<#
.SYNOPSIS
    Redacts HTTP response content for safe logging.

.DESCRIPTION
    Truncates and sanitizes HTTP response content to prevent
    leaking sensitive data in logs.

.PARAMETER Content
    The response content to redact.

.PARAMETER MaxLength
    Maximum length of content to show (default: 200).

.EXAMPLE
    $safeContent = Get-RedactedHttpContent -Content $response.Content
#>
function Get-RedactedHttpContent {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory=$false)]
        [AllowEmptyString()]
        [AllowNull()]
        [string]$Content,
        
        [Parameter(Mandatory=$false)]
        [int]$MaxLength = 200
    )
    
    if ([string]::IsNullOrEmpty($Content)) {
        return "[empty]"
    }
    
    # Truncate if too long
    if ($Content.Length -gt $MaxLength) {
        $Content = $Content.Substring(0, $MaxLength) + "... [truncated]"
    }
    
    # Redact any paths that might appear in response
    $Content = Get-RedactedMessage -Message $Content
    
    # Redact potential secrets/tokens
    # Pattern handles multiple formats:
    # - JSON with/without space: "password":"value" or "password": "value"
    # - Key=value: password=secret
    # - Values in quotes (including spaces): "password": "my secret value"
    # - XML-style: <password>value</password>
    # For security, we aggressively redact the entire value portion
    
    # JSON/query string format: key":"value" or key": "value" or key=value
    $Content = $Content -replace '(?i)(password|secret|key|token|bearer|authorization|credential|apikey|api_key|conn(?:ection)?_?string)["'']?\s*[:=]\s*["'']?([^"''\s,}<]+|["''][^"'']*["''])', '$1=[REDACTED]'
    
    # XML format: <password>value</password>
    $Content = $Content -replace '(?i)<(password|secret|key|token|credential|connectionstring)>([^<]*)</', '<$1>[REDACTED]</'
    
    return $Content
}
