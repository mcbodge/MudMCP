# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

BeforeAll {
    . "$PSScriptRoot\DeploymentWebRequest.ps1"
}

Describe 'Invoke-DeploymentHealthRequest' {
    BeforeEach {
        $script:originalCertificateValidationCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
        $script:originalSecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $null
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls
        $script:validationResultForTarget = $null
        $script:validationResultForRemote = $null
        $script:callbackDuringRequest = $null
        $script:securityProtocolDuringRequest = $null
    }

    AfterEach {
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $script:originalCertificateValidationCallback
        [System.Net.ServicePointManager]::SecurityProtocol = $script:originalSecurityProtocol
    }

    It 'Allows certificate errors only for the target loopback HTTPS request' {
        Mock Invoke-WebRequest {
            $script:callbackDuringRequest = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
            $script:securityProtocolDuringRequest = [System.Net.ServicePointManager]::SecurityProtocol
            $loopbackRequest = [System.Net.WebRequest]::Create([Uri]'https://localhost:8000/health')
            $remoteRequest = [System.Net.WebRequest]::Create([Uri]'https://example.com/health')

            $script:validationResultForTarget = $script:callbackDuringRequest.Invoke(
                $loopbackRequest,
                $null,
                $null,
                [System.Net.Security.SslPolicyErrors]::RemoteCertificateChainErrors)

            $script:validationResultForRemote = $script:callbackDuringRequest.Invoke(
                $remoteRequest,
                $null,
                $null,
                [System.Net.Security.SslPolicyErrors]::RemoteCertificateChainErrors)

            [pscustomobject]@{ StatusCode = 200; Content = 'Healthy' }
        }

        $response = Invoke-DeploymentHealthRequest -Uri ([Uri]'https://localhost:8000/health') -TimeoutSec 7 -SkipCertificateValidation

        $response.StatusCode | Should -Be 200
        $script:validationResultForTarget | Should -BeTrue
        $script:validationResultForRemote | Should -BeFalse
        $script:callbackDuringRequest.Method.DeclaringType.FullName | Should -Be 'MudDocs.DeploymentHealthCertificateValidator'
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback | Should -Be $null
        [System.Net.ServicePointManager]::SecurityProtocol | Should -Be ([System.Net.SecurityProtocolType]::Tls)
        Should -Invoke Invoke-WebRequest -Times 1 -Exactly -Scope It -ParameterFilter {
            $Uri.AbsoluteUri -eq 'https://localhost:8000/health' -and $TimeoutSec -eq 7
        }
    }

    It 'Does not install a certificate callback when bypass is not requested' {
        Mock Invoke-WebRequest {
            $script:callbackDuringRequest = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
            $script:securityProtocolDuringRequest = [System.Net.ServicePointManager]::SecurityProtocol
            [pscustomobject]@{ StatusCode = 200; Content = 'Healthy' }
        }

        $response = Invoke-DeploymentHealthRequest -Uri ([Uri]'https://localhost:8000/health')

        $response.StatusCode | Should -Be 200
        $script:callbackDuringRequest | Should -Be $null
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback | Should -Be $null
        [System.Net.ServicePointManager]::SecurityProtocol | Should -Be ([System.Net.SecurityProtocolType]::Tls)
    }

    It 'Enables TLS 1.2 during HTTPS requests' {
        Mock Invoke-WebRequest {
            $script:securityProtocolDuringRequest = [System.Net.ServicePointManager]::SecurityProtocol
            [pscustomobject]@{ StatusCode = 200; Content = 'Healthy' }
        }

        $response = Invoke-DeploymentHealthRequest -Uri ([Uri]'https://localhost:8000/health')

        $response.StatusCode | Should -Be 200
        ($script:securityProtocolDuringRequest -band [System.Net.SecurityProtocolType]::Tls12) |
            Should -Be ([System.Net.SecurityProtocolType]::Tls12)
        [System.Net.ServicePointManager]::SecurityProtocol | Should -Be ([System.Net.SecurityProtocolType]::Tls)
    }

    It 'Restores the certificate callback when the request throws' {
        Mock Invoke-WebRequest {
            $script:callbackDuringRequest = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
            $script:securityProtocolDuringRequest = [System.Net.ServicePointManager]::SecurityProtocol
            throw 'Network failure'
        }

        { Invoke-DeploymentHealthRequest -Uri ([Uri]'https://localhost:8000/health') -SkipCertificateValidation } |
            Should -Throw '*Network failure*'

        $script:callbackDuringRequest | Should -Not -Be $null
        $script:callbackDuringRequest.Method.DeclaringType.FullName | Should -Be 'MudDocs.DeploymentHealthCertificateValidator'
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback | Should -Be $null
        ($script:securityProtocolDuringRequest -band [System.Net.SecurityProtocolType]::Tls12) |
            Should -Be ([System.Net.SecurityProtocolType]::Tls12)
        [System.Net.ServicePointManager]::SecurityProtocol | Should -Be ([System.Net.SecurityProtocolType]::Tls)
    }
}
