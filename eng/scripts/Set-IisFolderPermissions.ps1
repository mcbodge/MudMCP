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

# Validate app pool name
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
