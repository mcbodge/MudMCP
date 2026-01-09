# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Sets folder permissions for an IIS website.

.DESCRIPTION
    Configures file system permissions for the IIS website.
    Grants Read/Execute on the site root and Modify access on logs and data directories.

.PARAMETER PhysicalPath
    Physical path of the website.

.PARAMETER AppPoolName
    Name of the IIS application pool (used for ACL identity).

.EXAMPLE
    .\Set-IisFolderPermissions.ps1 -PhysicalPath "C:\inetpub\wwwroot\MudBlazorMcp" -AppPoolName "MudBlazorMcpPool"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$PhysicalPath,
    
    [Parameter(Mandatory=$true)]
    [ValidateNotNullOrEmpty()]
    [string]$AppPoolName
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Load shared validation functions
. "$PSScriptRoot\Common\PathValidation.ps1"

# Validate app pool name
Test-IisResourceName -Name $AppPoolName -ResourceType 'app pool'

# Validate and normalize physical path
$PhysicalPath = Get-ValidatedPath -Path $PhysicalPath -ParameterName 'PhysicalPath'

$appPoolIdentity = "IIS AppPool\$AppPoolName"
$iisUsers = "BUILTIN\IIS_IUSRS"

# Grant Read/Execute on the site root (binaries, configs)
$acl = Get-Acl $PhysicalPath
$readRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $appPoolIdentity,
    "ReadAndExecute,Synchronize",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($readRule)

# IIS_IUSRS read access
$iisRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    $iisUsers,
    "ReadAndExecute",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($iisRule)
Set-Acl -Path $PhysicalPath -AclObject $acl
Write-Host "Set Read/Execute permissions on site root."

# Grant Modify access only for logs and data directories
foreach ($dir in @("logs", "data")) {
    $subPath = Join-Path $PhysicalPath $dir
    if (-not (Test-Path $subPath)) {
        New-Item -ItemType Directory -Path $subPath -Force | Out-Null
    }
    $subAcl = Get-Acl $subPath
    $writeRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $appPoolIdentity,
        "Modify,Synchronize",
        "ContainerInherit,ObjectInherit",
        "None",
        "Allow"
    )
    $subAcl.SetAccessRule($writeRule)
    Set-Acl -Path $subPath -AclObject $subAcl
    Write-Host "Set Modify permissions on $dir directory."
}

Write-Host "Folder permissions configured successfully."
exit 0
