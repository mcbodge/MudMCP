# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Tests deployment health by making HTTP(S) requests to the health endpoint.

.DESCRIPTION
    Performs health checks against the deployed application and collects diagnostic
    information if the health check fails.

.PARAMETER Port
    Port for the health check (default: 8000).

.PARAMETER Scheme
    URI scheme for the health check (default: https).

.PARAMETER HostName
    Host name for the health check (default: localhost).

.PARAMETER AppPoolName
    Name of the IIS application pool (for diagnostics).

.PARAMETER PhysicalPath
    Physical path of the deployment (for diagnostics).

.PARAMETER MaxRetries
    Maximum number of retry attempts (default: 6).

.PARAMETER RetryDelaySeconds
    Delay between retries in seconds (default: 10).

.PARAMETER SkipCertificateValidation
    Set to true to skip HTTPS certificate validation for loopback health checks. Use only for explicit dev/test scenarios.

.EXAMPLE
    .\Test-DeploymentHealth.ps1 -HostName "dev.muddocs.org" -Port 8000 -Scheme https -AppPoolName "MudBlazorMcpPool" -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [ValidateRange(1, 65535)]
    [int]$Port = 8000,

    [Parameter(Mandatory=$false)]
    [ValidateSet('http', 'https')]
    [string]$Scheme = 'https',

    [Parameter(Mandatory=$false)]
    [string]$HostName = 'localhost',
    
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$AppPoolName,
    
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$PhysicalPath,
    
    [Parameter(Mandatory=$false)]
    [ValidateRange(1, 20)]
    [int]$MaxRetries = 6,
    
    [Parameter(Mandatory=$false)]
    [ValidateRange(1, 60)]
    [int]$RetryDelaySeconds = 10,

    [Parameter(Mandatory=$false)]
    [bool]$SkipCertificateValidation = $false
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

# Load shared validation functions
. "$PSScriptRoot\Common\PathValidation.ps1"
. "$PSScriptRoot\Common\DeploymentWebRequest.ps1"

# Validate app pool name
Test-IisResourceName -Name $AppPoolName -ResourceType 'app pool'

# Validate and normalize physical path
$PhysicalPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName 'PhysicalPath'
$Scheme = $Scheme.ToLowerInvariant()

if ($HostName -match '^\$\([^)]+\)$' -or [string]::IsNullOrWhiteSpace($HostName)) {
    $HostName = 'localhost'
}

$HostName = $HostName.Trim()

if ($HostName.Contains('://') -or $HostName.Contains('/') -or $HostName.Contains('\') -or $HostName.Contains('?') -or $HostName.Contains('#')) {
    throw "HostName must be a DNS name or IP address without scheme, path, query, or fragment."
}

$normalizedHostName = $HostName
if ($normalizedHostName.StartsWith('[') -and $normalizedHostName.EndsWith(']')) {
    $normalizedHostName = $normalizedHostName.Substring(1, $normalizedHostName.Length - 2)
}

$parsedIpAddress = $null
$isIpAddress = [System.Net.IPAddress]::TryParse($normalizedHostName, [ref]$parsedIpAddress)

if ($HostName.Contains(':') -and -not $isIpAddress) {
    throw "HostName must not include a port. Provide only a DNS name or IP address and use the Port parameter for the port number."
}

$hostNameType = [System.Uri]::CheckHostName($normalizedHostName)
if (-not $isIpAddress -and $hostNameType -eq [System.UriHostNameType]::Unknown) {
    throw "HostName must be a valid DNS name or IP address."
}

$HostName = $normalizedHostName

$uriBuilder = New-Object System.UriBuilder -ArgumentList $Scheme, $HostName, $Port, 'health'
$healthUri = $uriBuilder.Uri
$skipCertificateValidation = $SkipCertificateValidation -and $healthUri.Scheme -eq 'https' -and $healthUri.IsLoopback

Write-Host "Waiting for application to start..."
Start-Sleep -Seconds 5

Write-Host "Health check URI: $healthUri"

if ($skipCertificateValidation) {
    Write-Host "Using loopback HTTPS health check with local certificate validation bypass."
}

$retryCount = 0
$lastError = $null

function Get-ExceptionMessages {
    param(
        [Parameter(Mandatory=$true)]
        [System.Exception]$Exception
    )

    $messages = New-Object System.Collections.Generic.List[string]
    $currentException = $Exception

    while ($currentException) {
        $messages.Add($currentException.Message)
        $currentException = $currentException.InnerException
    }

    return $messages
}

while ($retryCount -lt $MaxRetries) {
    try {
        Write-Host "Health check attempt $($retryCount + 1)..."
        $response = Invoke-DeploymentHealthRequest -Uri $healthUri -TimeoutSec 10 -SkipCertificateValidation:$skipCertificateValidation
        
        if ($response.StatusCode -eq 200) {
            Write-Host "##vso[task.complete result=Succeeded;]Deployment verified successfully!"
            Write-Host "Health check response: $($response.Content)"
            exit 0
        }
    } catch {
        $lastError = $_
        $exceptionMessages = Get-ExceptionMessages -Exception $_.Exception
        Write-Host "Attempt failed: $($exceptionMessages -join ' | Inner: ')"
    }
    
    $retryCount++
    if ($retryCount -lt $MaxRetries) {
        Write-Host "Retrying in $RetryDelaySeconds seconds..."
        Start-Sleep -Seconds $RetryDelaySeconds
    }
}

# Health check failed - collect diagnostic information
Write-Host ""
Write-Host "##[error]Health check did not pass after $MaxRetries attempts."
Write-Host ""
Write-Host "========== DIAGNOSTIC INFORMATION =========="

# App Pool Status
Write-Host ""
Write-Host "--- App Pool Status ---"
try {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    $pool = Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    if ($pool) {
        Write-Host "App Pool '$AppPoolName': $($pool.State)"
    } else {
        Write-Host "App Pool '$AppPoolName' not found!"
    }
} catch {
    Write-Host "Could not retrieve app pool status: $_"
}

# Deployed Files Check
Write-Host ""
Write-Host "--- Deployed Files Check ---"
$mainDll = Join-Path $PhysicalPath "MudDocs.dll"
if (Test-Path $mainDll) {
    Write-Host "Main DLL exists: $mainDll"
} else {
    Write-Host "##[error]Main DLL NOT FOUND: $mainDll"
    Write-Host "Contents of ${PhysicalPath}:"
    Get-ChildItem -Path $PhysicalPath -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  $($_.Name)" }
}

# stdout Logs
Write-Host ""
Write-Host "--- Application stdout Logs (last 50 lines) ---"
$logsPath = Join-Path $PhysicalPath "logs"
$stdoutLogs = Get-ChildItem -Path $logsPath -Filter "stdout*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($stdoutLogs) {
    Get-Content $stdoutLogs.FullName -Tail 50 -ErrorAction SilentlyContinue
} else {
    Write-Host "No stdout log files found in $logsPath"
}

# Windows Event Log
# NOTE: We filter by LogName in FilterHashtable, then filter by ProviderName with Where-Object.
# Using ProviderName directly in FilterHashtable doesn't support wildcards and would require
# an exact match like 'IIS AspNetCore Module V2'. The two-step approach captures events from
# any module version (V1, V2, future V3) at the cost of retrieving more events initially.
Write-Host ""
Write-Host "--- IIS ASP.NET Core Module Event Log (last 10 entries) ---"
try {
    Get-WinEvent -FilterHashtable @{ LogName = 'Application' } -MaxEvents 100 -ErrorAction SilentlyContinue |
        Where-Object { $_.ProviderName -like 'IIS AspNetCore Module*' } |
        Select-Object -First 10 |
        ForEach-Object { Write-Host "[$($_.TimeCreated)] $($_.LevelDisplayName): $($_.Message)" }
} catch {
    Write-Host "Could not retrieve event log entries: $_"
}

# HTTP Response Details
Write-Host ""
Write-Host "--- Last HTTP Error Details ---"
if ($lastError) {
    Write-Host "Exception: $($lastError.Exception.Message)"
    $innerException = $lastError.Exception.InnerException
    while ($innerException) {
        Write-Host "Inner Exception: $($innerException.Message)"
        $innerException = $innerException.InnerException
    }

    if ($lastError.Exception.Response) {
        Write-Host "Status Code: $($lastError.Exception.Response.StatusCode)"
        try {
            $stream = $lastError.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = [System.IO.StreamReader]::new($stream)
                try {
                    $responseBody = $reader.ReadToEnd()
                    Write-Host "Response Body: $responseBody"
                } finally {
                    $reader.Dispose()
                }
            }
        } catch {
            Write-Host "Could not read response body"
        }
    }
}

Write-Host ""
Write-Host "============================================="
Write-Host "##vso[task.complete result=Failed;]Deployment verification failed!"
exit 1
