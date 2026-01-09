# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

<#
.SYNOPSIS
    Pester tests for LoggingUtility.ps1

.DESCRIPTION
    Tests the path redaction and logging functions in the LoggingUtility module.
#>

BeforeAll {
    # Import the module under test
    . "$PSScriptRoot\LoggingUtility.ps1"
}

Describe 'Get-RedactedMessage' {
    Context 'IIS path redaction' {
        It 'Redacts C:\inetpub\wwwroot\ paths' {
            $message = 'Deploying to C:\inetpub\wwwroot\MyApp'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Deploying to [WEBROOT]\MyApp'
        }
        
        It 'Redacts D:\inetpub\wwwroot\ paths' {
            $message = 'Deploying to D:\inetpub\wwwroot\MyApp'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Deploying to [WEBROOT]\MyApp'
        }
        
        It 'Redacts C:\inetpub\ paths' {
            $message = 'Path is C:\inetpub\logs\file.log'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Path is [IIS]\logs\file.log'
        }
        
        It 'Redacts C:\wwwroot\ paths' {
            $message = 'Deploying to C:\wwwroot\MyApp'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Deploying to [WEBROOT]\MyApp'
        }
        
        It 'Redacts C:\WWW\ paths' {
            $message = 'Deploying to C:\WWW\MyApp'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Deploying to [WEBROOT]\MyApp'
        }
    }
    
    Context 'CI/CD environment path redaction' {
        BeforeEach {
            # Save original environment variables
            $script:origPipelineWorkspace = $env:PIPELINE_WORKSPACE
            $script:origArtifacts = $env:BUILD_ARTIFACTSTAGINGDIRECTORY
            $script:origSource = $env:BUILD_SOURCESDIRECTORY
            $script:origTemp = $env:AGENT_TEMPDIRECTORY
        }
        
        AfterEach {
            # Restore original environment variables
            $env:PIPELINE_WORKSPACE = $script:origPipelineWorkspace
            $env:BUILD_ARTIFACTSTAGINGDIRECTORY = $script:origArtifacts
            $env:BUILD_SOURCESDIRECTORY = $script:origSource
            $env:AGENT_TEMPDIRECTORY = $script:origTemp
        }
        
        It 'Redacts PIPELINE_WORKSPACE paths' {
            $env:PIPELINE_WORKSPACE = 'D:\a\1'
            $message = 'Artifact at D:\a\1\artifacts\file.zip'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Artifact at [PIPELINE]\artifacts\file.zip'
        }
        
        It 'Redacts BUILD_ARTIFACTSTAGINGDIRECTORY paths' {
            $env:BUILD_ARTIFACTSTAGINGDIRECTORY = 'D:\a\1\a'
            $message = 'Publishing to D:\a\1\a\publish'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Publishing to [ARTIFACTS]\publish'
        }
        
        It 'Redacts BUILD_SOURCESDIRECTORY paths' {
            $env:BUILD_SOURCESDIRECTORY = 'D:\a\1\s'
            $message = 'Source at D:\a\1\s\src\MyProject'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Source at [SOURCE]\src\MyProject'
        }
    }
    
    Context 'Edge cases' {
        It 'Returns empty string for empty input' {
            $result = Get-RedactedMessage -Message ''
            $result | Should -Be ''
        }
        
        It 'Handles null gracefully' {
            $result = Get-RedactedMessage -Message $null
            # PowerShell converts null to empty string in string parameters
            $result | Should -BeNullOrEmpty
        }
        
        It 'Preserves messages without paths' {
            $message = 'Application started successfully on port 5180'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be $message
        }
        
        It 'Redacts multiple paths in same message' {
            $message = 'Copying from C:\inetpub\wwwroot\OldApp to C:\inetpub\wwwroot\NewApp'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Copying from [WEBROOT]\OldApp to [WEBROOT]\NewApp'
        }
    }
    
    Context 'Generic path fallback redaction' {
        It 'Redacts unknown absolute paths' {
            $message = 'Found file at C:\Users\admin\secrets\config.json'
            $result = Get-RedactedMessage -Message $message
            $result | Should -Be 'Found file at [PATH]\config.json'
        }
    }
}

Describe 'Get-RedactedPath' {
    It 'Returns redacted path' {
        $path = 'C:\inetpub\wwwroot\MyApp\web.config'
        $result = Get-RedactedPath -Path $path
        $result | Should -Be '[WEBROOT]\MyApp\web.config'
    }
}

Describe 'Get-RedactedHttpContent' {
    Context 'Content truncation' {
        It 'Truncates content longer than MaxLength' {
            $longContent = 'A' * 300
            $result = Get-RedactedHttpContent -Content $longContent -MaxLength 200
            $result | Should -Match '\[truncated\]$'
            $result.Length | Should -BeLessThan 250
        }
        
        It 'Does not truncate short content' {
            $shortContent = 'OK'
            $result = Get-RedactedHttpContent -Content $shortContent -MaxLength 200
            $result | Should -Be 'OK'
        }
    }
    
    Context 'Empty content handling' {
        It 'Returns [empty] for null content' {
            $result = Get-RedactedHttpContent -Content $null
            $result | Should -Be '[empty]'
        }
        
        It 'Returns [empty] for empty string' {
            $result = Get-RedactedHttpContent -Content ''
            $result | Should -Be '[empty]'
        }
    }
    
    Context 'Secret redaction' {
        It 'Redacts password patterns' {
            $content = '{"password": "secret123"}'
            $result = Get-RedactedHttpContent -Content $content
            $result | Should -Match 'password=\[REDACTED\]'
        }
        
        It 'Redacts token patterns' {
            $content = 'Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9'
            $result = Get-RedactedHttpContent -Content $content
            $result | Should -Match 'Authorization=\[REDACTED\]'
        }
        
        It 'Redacts key patterns' {
            $content = 'api_key=abc123xyz'
            $result = Get-RedactedHttpContent -Content $content
            $result | Should -Match 'key=\[REDACTED\]'
        }
    }
}

Describe 'Write-InfoLog' {
    It 'Outputs redacted message via Write-Host' {
        # We can't easily capture Write-Host output in Pester, but we can verify it doesn't throw
        { Write-InfoLog -Message 'Deploying to C:\inetpub\wwwroot\MyApp' } | Should -Not -Throw
    }
}

Describe 'Write-WarnLog' {
    It 'Outputs redacted warning' {
        # Capture warning stream
        $warnings = Write-WarnLog -Message 'Path not found: C:\inetpub\wwwroot\MyApp' 3>&1
        $warnings | Should -Match '\[WEBROOT\]'
    }
}
