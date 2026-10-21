# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.
#requires -Version 7.0

<#
.SYNOPSIS
    Reports CLI elapsed and process latency against an elapsed p95 target.
.DESCRIPTION
    Starts a fresh process for every sample, including process startup and cold JIT.
    A warm OS file cache is not a warm process. Elapsed timing includes the CLI query
    and stdout flush; process timing also includes earlier startup work.
    BudgetMs defaults to a reported 50ms target, not a hard gate. Above-target commands
    remain in the complete table and produce an explicit stderr warning with their count.
    Successful measurement returns 0 by default; StrictBudget returns 1 when any elapsed
    p95 exceeds the target. Build, runtime, or invalid timing failures return 1;
    an absent indexed store returns 2. The timing environment variable is always restored.
.PARAMETER StrictBudget
    Fail with exit code 1 when any command's elapsed p95 exceeds BudgetMs.
.EXAMPLE
    pwsh -NoProfile -File eng/scripts/Measure-CliLatency.ps1 -Version 9.0.0
#>
[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Version = '9.0.0',
    [ValidateRange(1, 2147483647)]
    [int]$Iterations = 20,
    [ValidateRange(0, [double]::MaxValue)]
    [double]$BudgetMs = 50,
    [string]$DataPath,
    [switch]$NoBuild,
    [switch]$StrictBudget
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-CapturedProcess {
    param([string]$FilePath, [string[]]$ArgumentList)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $ArgumentList) {
        $startInfo.ArgumentList.Add($argument)
    }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        [void]$process.Start()
        # Drain both pipes concurrently before waiting, even for large command output.
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return [pscustomobject]@{
            ExitCode = $process.ExitCode
            Stdout = $stdout.GetAwaiter().GetResult()
            Stderr = $stderr.GetAwaiter().GetResult()
        }
    }
    finally {
        $process.Dispose()
    }
}

function Get-NearestRank {
    param([double[]]$Values, [double]$Percentile)
    $sorted = @($Values | Sort-Object)
    return $sorted[[int][Math]::Ceiling($Percentile * $sorted.Count) - 1]
}

function Get-TimingValue {
    param([string]$Stderr, [string]$Name)
    $matches = [regex]::Matches($Stderr, "(?m)^$Name=([0-9]+(?:\.[0-9]+)?)\r?$")
    if ($matches.Count -ne 1) {
        throw "Expected exactly one invariant $Name timing on stderr."
    }
    return [double]::Parse($matches[0].Groups[1].Value, [System.Globalization.CultureInfo]::InvariantCulture)
}

function Invoke-CliLatency {
    [CmdletBinding()]
    param(
        [string]$Version = '9.0.0',
        [int]$Iterations = 20,
        [double]$BudgetMs = 50,
        [string]$DataPath,
        [switch]$NoBuild,
        [switch]$StrictBudget
    )

    $root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $project = Join-Path $root 'src' 'MudBlazor.Mcp' 'MudBlazor.Mcp.csproj'
    $dll = Join-Path $root 'src' 'MudBlazor.Mcp' 'bin' 'Release' 'net10.0' 'MudBlazor.Mcp.dll'
    $globalArguments = @('--version', $Version)
    if ($DataPath) { $globalArguments += @('--data-path', $DataPath) }
    $previousTiming = [Environment]::GetEnvironmentVariable('MUDMCP_TIMING')
    try {
        if (-not $NoBuild) {
            $build = Invoke-CapturedProcess -FilePath 'dotnet' -ArgumentList @('build', $project, '-c', 'Release')
            Write-Output $build.Stdout
            if ($build.Stderr) { [Console]::Error.WriteLine($build.Stderr) }
            if ($build.ExitCode -ne 0) { throw "Release build failed (exit $($build.ExitCode))." }
        }
        $status = Invoke-CapturedProcess -FilePath 'dotnet' -ArgumentList (@($dll, 'index-status') + $globalArguments)
        if ($status.ExitCode -eq 3) {
            [Console]::Error.WriteLine("Version $Version is not indexed; run: mudmcp index --version $Version")
            return 2
        }
        if ($status.ExitCode -ne 0) { throw "index-status failed (exit $($status.ExitCode)): $($status.Stderr)" }
        $env:MUDMCP_TIMING = '1'
        $commands = @(
            ,@('component', 'MudButton')
            ,@('props', 'MudButton', '--filter', 'Color,Variant,Size')
            ,@('props', 'MudDataGrid')
            ,@('events', 'MudButton')
            ,@('methods', 'MudButton')
            ,@('api', 'MudButton')
            ,@('enum', 'Color')
            ,@('example', 'MudButton', 'Filled')
            ,@('examples', 'MudButton', '--names-only')
            ,@('search', 'button')
            ,@('list')
            ,@('categories')
            ,@('related', 'MudButton')
        )
        $overBudgetCount = 0
        Write-Output 'Command                                         p50 elapsed  p95 elapsed  p50 process  p95 process (ms)'
        Write-Output ('-' * 109)
        foreach ($command in $commands) {
            $elapsed = [System.Collections.Generic.List[double]]::new()
            $processTimes = [System.Collections.Generic.List[double]]::new()
            for ($i = 0; $i -lt $Iterations; $i++) {
                $result = Invoke-CapturedProcess -FilePath 'dotnet' -ArgumentList (@($dll) + $command + $globalArguments)
                if ($result.ExitCode -ne 0) {
                    throw "$($command -join ' ') failed (exit $($result.ExitCode)): $($result.Stderr)"
                }
                $elapsed.Add((Get-TimingValue -Stderr $result.Stderr -Name 'elapsed-ms'))
                $processTimes.Add((Get-TimingValue -Stderr $result.Stderr -Name 'process-ms'))
            }
            $p95 = Get-NearestRank -Values $elapsed.ToArray() -Percentile 0.95
            if ($p95 -gt $BudgetMs) { $overBudgetCount++ }
            Write-Output ([string]::Format(
                [System.Globalization.CultureInfo]::InvariantCulture,
                '{0,-46} {1,12:F1} {2,12:F1} {3,12:F0} {4,12:F0}',
                ($command -join ' '),
                (Get-NearestRank -Values $elapsed.ToArray() -Percentile 0.50),
                $p95,
                (Get-NearestRank -Values $processTimes.ToArray() -Percentile 0.50),
                (Get-NearestRank -Values $processTimes.ToArray() -Percentile 0.95)))
        }
        if ($overBudgetCount -gt 0) {
            [Console]::Error.WriteLine("warning: $overBudgetCount commands above elapsed p95 target ($($BudgetMs.ToString([System.Globalization.CultureInfo]::InvariantCulture)) ms).")
            if ($StrictBudget) { return 1 }
        }
        return 0
    }
    catch {
        [Console]::Error.WriteLine("error: $($_.Exception.Message)")
        return 1
    }
    finally {
        if ($null -eq $previousTiming) {
            Remove-Item Env:MUDMCP_TIMING -ErrorAction SilentlyContinue
        }
        else {
            [Environment]::SetEnvironmentVariable('MUDMCP_TIMING', $previousTiming)
        }
    }
}

# Dot-sourcing exposes the helpers for dependency-free controlled-input self-checks.
if ($MyInvocation.InvocationName -ne '.') {
    $result = @(Invoke-CliLatency @PSBoundParameters)
    if ($result.Count -gt 1) { $result[0..($result.Count - 2)] | Write-Output }
    exit [int]$result[-1]
}
