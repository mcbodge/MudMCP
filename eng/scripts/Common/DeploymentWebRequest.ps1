# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

if (-not ([System.Management.Automation.PSTypeName]'MudDocs.DeploymentHealthCertificateValidator').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace MudDocs
{
    public static class DeploymentHealthCertificateValidator
    {
        private static string targetScheme;
        private static string targetAuthority;
        private static RemoteCertificateValidationCallback previousCallback;

        public static void Configure(Uri targetUri, RemoteCertificateValidationCallback previous)
        {
            targetScheme = targetUri.Scheme;
            targetAuthority = targetUri.Authority;
            previousCallback = previous;
        }

        public static void Clear()
        {
            targetScheme = null;
            targetAuthority = null;
            previousCallback = null;
        }

        public static bool Validate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            var request = sender as HttpWebRequest;

            if (request != null &&
                request.RequestUri != null &&
                request.RequestUri.IsLoopback &&
                string.Equals(request.RequestUri.Scheme, targetScheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.RequestUri.Authority, targetAuthority, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (previousCallback != null)
            {
                return previousCallback(sender, certificate, chain, sslPolicyErrors);
            }

            return sslPolicyErrors == SslPolicyErrors.None;
        }
    }
}
'@
}

function Invoke-DeploymentHealthRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [ValidateNotNull()]
        [Uri]$Uri,

        [Parameter(Mandatory=$false)]
        [ValidateRange(1, 300)]
        [int]$TimeoutSec = 10,

        [Parameter(Mandatory=$false)]
        [switch]$SkipCertificateValidation
    )

    $useCertificateValidationBypass = $SkipCertificateValidation -and $Uri.Scheme -eq 'https' -and $Uri.IsLoopback
    $previousSecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol
    $previousCertificateValidationCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
    $targetUri = $Uri

    try {
        if ($Uri.Scheme -eq 'https') {
            [System.Net.ServicePointManager]::SecurityProtocol = $previousSecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
        }

        if ($useCertificateValidationBypass) {
            [MudDocs.DeploymentHealthCertificateValidator]::Configure($targetUri, $previousCertificateValidationCallback)
            $validatorMethod = [MudDocs.DeploymentHealthCertificateValidator].GetMethod('Validate', [System.Reflection.BindingFlags]'Public, Static')
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = [System.Delegate]::CreateDelegate(
                [System.Net.Security.RemoteCertificateValidationCallback],
                $validatorMethod)
        }

        return Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec $TimeoutSec
    } finally {
        if ($useCertificateValidationBypass) {
            [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $previousCertificateValidationCallback
            [MudDocs.DeploymentHealthCertificateValidator]::Clear()
        }

        if ($Uri.Scheme -eq 'https') {
            [System.Net.ServicePointManager]::SecurityProtocol = $previousSecurityProtocol
        }
    }
}