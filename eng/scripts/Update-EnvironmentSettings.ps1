# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Updates the ASP.NET Core environment setting in web.config.

.DESCRIPTION
    Modifies the web.config file to set the ASPNETCORE_ENVIRONMENT variable.

.PARAMETER PhysicalPath
    Physical path where web.config is located.

.PARAMETER Environment
    ASP.NET Core environment name (e.g., Development, Staging, Production).

.EXAMPLE
    .\Update-EnvironmentSettings.ps1 -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp" -Environment "Production"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$PhysicalPath,
    
    [Parameter(Mandatory=$true)]
    [ValidateSet('Development', 'Staging', 'Production')]
    [string]$Environment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Load shared utilities
. "$PSScriptRoot\Common\PathValidation.ps1"
. "$PSScriptRoot\Common\LoggingUtility.ps1"

# Validate and normalize physical path
$PhysicalPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName 'PhysicalPath'

$webConfigPath = Join-Path $PhysicalPath "web.config"

if (Test-Path $webConfigPath) {
    Write-InfoLog "Updating web.config for environment: $Environment"
    
    [xml]$webConfig = Get-Content $webConfigPath
    
    # Find or create environmentVariables section
    $aspNetCore = $webConfig.SelectSingleNode("/*[local-name()='configuration']/*[local-name()='location']/*[local-name()='system.webServer']/*[local-name()='aspNetCore']")
    if ($aspNetCore) {
        # Use SelectSingleNode to safely check for environmentVariables (avoids strict mode errors)
        $envVars = $aspNetCore.SelectSingleNode("environmentVariables")
        if (-not $envVars) {
            $envVars = $webConfig.CreateElement("environmentVariables")
            $aspNetCore.AppendChild($envVars) | Out-Null
        }
        
        # Update or add ASPNETCORE_ENVIRONMENT
        $envVar = $envVars.SelectSingleNode("environmentVariable[@name='ASPNETCORE_ENVIRONMENT']")
        if ($envVar) {
            $envVar.SetAttribute("value", $Environment)
        } else {
            $newEnvVar = $webConfig.CreateElement("environmentVariable")
            $newEnvVar.SetAttribute("name", "ASPNETCORE_ENVIRONMENT")
            $newEnvVar.SetAttribute("value", $Environment)
            $envVars.AppendChild($newEnvVar) | Out-Null
        }
        
        $webConfig.Save($webConfigPath)
        Write-InfoLog "web.config updated successfully."
    } else {
        Write-WarnLog "Could not find aspNetCore section in web.config"
    }
} else {
    Write-WarnLog "web.config not found at: $webConfigPath"
}

exit 0
