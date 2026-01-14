# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Pester tests for PathValidation.ps1 shared validation functions.

.DESCRIPTION
    Tests security-critical path validation functions including:
    - Test-PathSecurity: Validates paths against security issues
    - Test-AllowedRoot: Validates paths against allowed root directories
    - Get-ValidatedPath: Combined validation and normalization
    - Test-IisResourceName: Validates IIS resource names

.NOTES
    Run tests with: Invoke-Pester -Path .\PathValidation.Tests.ps1
#>

BeforeAll {
    # Dot-source the module being tested
    . "$PSScriptRoot\PathValidation.ps1"
}

Describe 'Test-PathSecurity' {
    Context 'When given valid absolute paths' {
        It 'Should not throw for valid Windows path' {
            { Test-PathSecurity -Path 'C:\inetpub\wwwroot\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path with forward slashes' {
            { Test-PathSecurity -Path 'C:\inetpub/wwwroot/MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for UNC path' {
            { Test-PathSecurity -Path '\\server\share\folder' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path with spaces' {
            { Test-PathSecurity -Path 'C:\Program Files\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }
    }

    Context 'When given relative paths' {
        It 'Should throw for relative path without drive' {
            { Test-PathSecurity -Path 'folder\subfolder' -ParameterName 'TestPath' } | Should -Throw '*absolute path*'
        }

        It 'Should throw for dot-relative path' {
            { Test-PathSecurity -Path '.\folder' -ParameterName 'TestPath' } | Should -Throw '*absolute path*'
        }

        It 'Should throw for parent-relative path' {
            { Test-PathSecurity -Path '..\folder' -ParameterName 'TestPath' } | Should -Throw
        }
    }

    Context 'When given bare drive roots' {
        It 'Should throw for bare C:' {
            { Test-PathSecurity -Path 'C:' -ParameterName 'TestPath' } | Should -Throw '*drive root*'
        }

        It 'Should throw for bare D:' {
            { Test-PathSecurity -Path 'D:' -ParameterName 'TestPath' } | Should -Throw '*drive root*'
        }

        It 'Should not throw for C:\' {
            { Test-PathSecurity -Path 'C:\' -ParameterName 'TestPath' } | Should -Not -Throw
        }
    }

    Context 'When given paths with directory traversal' {
        It 'Should throw for path containing ..' {
            { Test-PathSecurity -Path 'C:\inetpub\..\Windows' -ParameterName 'TestPath' } | Should -Throw '*traversal*'
        }

        It 'Should throw for path with embedded ..' {
            { Test-PathSecurity -Path 'C:\inetpub\wwwroot\..\..\..\Windows' -ParameterName 'TestPath' } | Should -Throw '*traversal*'
        }
    }

    Context 'When given paths with invalid characters' {
        # Note: For characters like <, >, |, ", .NET's IsPathRooted throws "Illegal characters in path"
        # before our regex check runs. For ? and *, our regex catches them first.
        # Either way, the important thing is that these paths are rejected.

        It 'Should throw for path with <' {
            # IsPathRooted throws before our check
            { Test-PathSecurity -Path 'C:\inetpub\<test>' -ParameterName 'TestPath' } | Should -Throw '*Illegal characters*'
        }

        It 'Should throw for path with >' {
            # IsPathRooted throws before our check
            { Test-PathSecurity -Path 'C:\inetpub\test>' -ParameterName 'TestPath' } | Should -Throw '*Illegal characters*'
        }

        It 'Should throw for path with |' {
            # IsPathRooted throws before our check
            { Test-PathSecurity -Path 'C:\inetpub\test|file' -ParameterName 'TestPath' } | Should -Throw '*Illegal characters*'
        }

        It 'Should throw for path with "' {
            # IsPathRooted throws before our check
            { Test-PathSecurity -Path 'C:\inetpub\"test"' -ParameterName 'TestPath' } | Should -Throw '*Illegal characters*'
        }

        It 'Should throw for path with ?' {
            # IsPathRooted doesn't throw for ?, so our regex catches it
            { Test-PathSecurity -Path 'C:\inetpub\test?' -ParameterName 'TestPath' } | Should -Throw '*Invalid characters*'
        }

        It 'Should throw for path with *' {
            # IsPathRooted doesn't throw for *, so our regex catches it
            { Test-PathSecurity -Path 'C:\inetpub\test*' -ParameterName 'TestPath' } | Should -Throw '*Invalid characters*'
        }
    }

    Context 'Error message includes parameter name' {
        It 'Should include parameter name in error for relative path' {
            { Test-PathSecurity -Path 'relative\path' -ParameterName 'PhysicalPath' } | Should -Throw '*PhysicalPath*'
        }
    }
}

Describe 'Test-AllowedRoot' {
    Context 'When path is under allowed root' {
        It 'Should not throw for path under C:\inetpub' {
            { Test-AllowedRoot -Path 'C:\inetpub\wwwroot\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path exactly matching allowed root' {
            { Test-AllowedRoot -Path 'C:\inetpub' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path under C:\WWW' {
            { Test-AllowedRoot -Path 'C:\WWW\sites\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path under D:\WWW' {
            { Test-AllowedRoot -Path 'D:\WWW\sites\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path under D:\inetpub' {
            { Test-AllowedRoot -Path 'D:\inetpub\wwwroot\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path under C:\wwwroot' {
            { Test-AllowedRoot -Path 'C:\wwwroot\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should not throw for path under D:\wwwroot' {
            { Test-AllowedRoot -Path 'D:\wwwroot\sites\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }
    }

    Context 'When path is not under allowed root' {
        It 'Should throw for path under C:\Windows' {
            { Test-AllowedRoot -Path 'C:\Windows\System32' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }

        It 'Should throw for path under C:\Users' {
            { Test-AllowedRoot -Path 'C:\Users\Admin\Desktop' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }

        It 'Should throw for path that starts with allowed root name but is different' {
            # C:\inetpubFake should NOT match C:\inetpub
            { Test-AllowedRoot -Path 'C:\inetpubFake\wwwroot' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }
    }

    Context 'When using custom allowed roots' {
        It 'Should accept path under custom allowed root' {
            { Test-AllowedRoot -Path 'E:\CustomRoot\MyApp' -AllowedRoots @('E:\CustomRoot') -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should reject path not under custom allowed root' {
            { Test-AllowedRoot -Path 'C:\inetpub\wwwroot' -AllowedRoots @('E:\CustomRoot') -ParameterName 'TestPath' } | Should -Throw
        }
    }

    Context 'When allowed roots array has issues' {
        It 'Should skip empty strings in allowed roots' {
            { Test-AllowedRoot -Path 'C:\inetpub\wwwroot' -AllowedRoots @('', 'C:\inetpub', $null) -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should throw when no valid allowed roots remain' {
            { Test-AllowedRoot -Path 'C:\inetpub\wwwroot' -AllowedRoots @('', $null, '   ') -ParameterName 'TestPath' } | Should -Throw '*No valid allowed roots*'
        }
    }

    Context 'Case sensitivity' {
        It 'Should be case-insensitive for path matching' {
            { Test-AllowedRoot -Path 'C:\INETPUB\WWWROOT\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }
    }

    Context 'Path traversal security' {
        # Test-AllowedRoot uses GetFullPath internally which resolves ".." sequences.
        # These tests verify that path traversal attempts are properly handled.

        It 'Should reject path that traverses out of allowed root via ..' {
            # C:\inetpub\..\Windows resolves to C:\Windows which is not allowed
            { Test-AllowedRoot -Path 'C:\inetpub\..\Windows' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }

        It 'Should reject path with embedded traversal that escapes allowed root' {
            # C:\inetpub\wwwroot\..\..\Windows resolves to C:\Windows
            { Test-AllowedRoot -Path 'C:\inetpub\wwwroot\..\..\Windows' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }

        It 'Should reject path that looks like allowed root but traverses out' {
            # C:\inetpub..\Windows is equivalent to C:\inetpub\..\Windows and resolves to C:\Windows (outside the allowed root)
            { Test-AllowedRoot -Path 'C:\inetpub..\Windows' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }

        It 'Should accept path with .. that stays within allowed root' {
            # C:\inetpub\wwwroot\subdir\..\file resolves to C:\inetpub\wwwroot\file (still under allowed root)
            { Test-AllowedRoot -Path 'C:\inetpub\wwwroot\subdir\..\MyApp' -ParameterName 'TestPath' } | Should -Not -Throw
        }

        It 'Should reject multiple traversal sequences that escape' {
            { Test-AllowedRoot -Path 'C:\inetpub\a\b\c\..\..\..\..\..\Windows' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }

        It 'Should handle forward slash traversal attempts' {
            # Mixed slashes with traversal
            { Test-AllowedRoot -Path 'C:\inetpub/../Windows' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }
    }
}

Describe 'Get-ValidatedPath' {
    Context 'When given valid path under allowed root' {
        It 'Should return normalized path' {
            $result = Get-ValidatedPath -Path 'C:\inetpub\wwwroot\MyApp' -ParameterName 'TestPath'
            $result | Should -Be 'C:\inetpub\wwwroot\MyApp'
        }

        It 'Should normalize forward slashes to backslashes' {
            $result = Get-ValidatedPath -Path 'C:\inetpub/wwwroot/MyApp' -ParameterName 'TestPath'
            $result | Should -Be 'C:\inetpub\wwwroot\MyApp'
        }

        It 'Should remove trailing backslash' {
            $result = Get-ValidatedPath -Path 'C:\inetpub\wwwroot\MyApp\' -ParameterName 'TestPath'
            $result | Should -Be 'C:\inetpub\wwwroot\MyApp'
        }
    }

    Context 'When given invalid path' {
        It 'Should throw for relative path' {
            { Get-ValidatedPath -Path 'relative\path' -ParameterName 'TestPath' } | Should -Throw
        }

        It 'Should throw for path with traversal' {
            { Get-ValidatedPath -Path 'C:\inetpub\..\Windows' -ParameterName 'TestPath' } | Should -Throw
        }

        It 'Should throw for path not under allowed root' {
            { Get-ValidatedPath -Path 'C:\Windows\System32' -ParameterName 'TestPath' } | Should -Throw
        }
    }

    Context 'Integration of security and root validation' {
        It 'Should reject path that passes security but fails root check' {
            { Get-ValidatedPath -Path 'C:\Windows\System32' -ParameterName 'TestPath' } | Should -Throw '*allowed roots*'
        }

        It 'Should reject path that fails security before root check' {
            { Get-ValidatedPath -Path 'C:\inetpub\..\Windows' -ParameterName 'TestPath' } | Should -Throw '*traversal*'
        }
    }
}

Describe 'Test-IisResourceName' {
    Context 'When given valid names' {
        It 'Should not throw for alphanumeric name' {
            { Test-IisResourceName -Name 'MyWebsite123' -ResourceType 'website' } | Should -Not -Throw
        }

        It 'Should not throw for name with underscores' {
            { Test-IisResourceName -Name 'My_Website' -ResourceType 'website' } | Should -Not -Throw
        }

        It 'Should not throw for name with hyphens' {
            { Test-IisResourceName -Name 'My-Website' -ResourceType 'website' } | Should -Not -Throw
        }

        It 'Should not throw for mixed valid characters' {
            { Test-IisResourceName -Name 'MudBlazor-Mcp_Pool123' -ResourceType 'app pool' } | Should -Not -Throw
        }
    }

    Context 'When given invalid names' {
        It 'Should throw for name with spaces' {
            { Test-IisResourceName -Name 'My Website' -ResourceType 'website' } | Should -Throw '*Invalid*'
        }

        It 'Should throw for name with dots' {
            { Test-IisResourceName -Name 'My.Website' -ResourceType 'website' } | Should -Throw '*Invalid*'
        }

        It 'Should throw for name with special characters' {
            { Test-IisResourceName -Name 'My@Website!' -ResourceType 'website' } | Should -Throw '*Invalid*'
        }

        It 'Should throw for name with forward slash' {
            { Test-IisResourceName -Name 'My/Website' -ResourceType 'website' } | Should -Throw '*Invalid*'
        }

        It 'Should throw for name with backslash' {
            { Test-IisResourceName -Name 'My\Website' -ResourceType 'website' } | Should -Throw '*Invalid*'
        }

        It 'Should throw for empty string' {
            # NOTE: Mandatory [string] parameters in PowerShell reject empty strings before our regex is evaluated.
            # See project docs (PathValidation PowerShell notes) or PowerShell help for [Parameter(Mandatory=$true)] for details.
            { Test-IisResourceName -Name '' -ResourceType 'website' } | Should -Throw '*empty string*'
        }
    }

    Context 'Error message quality' {
        It 'Should include resource type in error message' {
            { Test-IisResourceName -Name 'Invalid Name' -ResourceType 'app pool' } | Should -Throw '*app pool*'
        }

        It 'Should include the invalid name in error message' {
            { Test-IisResourceName -Name 'Bad@Name' -ResourceType 'website' } | Should -Throw '*Bad@Name*'
        }
    }
}
