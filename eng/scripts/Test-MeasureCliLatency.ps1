# Copyright (c) 2026 Mud MCP Contributors
# Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.
#requires -Version 7.0

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

$scriptPath = Join-Path $PSScriptRoot 'Measure-CliLatency.ps1'
Assert-True (Test-Path $scriptPath) 'The CLI latency benchmark implementation is missing.'
. $scriptPath

Assert-True ((Get-NearestRank -Values (1..20) -Percentile 0.50) -eq 10) 'p50 must use nearest rank.'
Assert-True ((Get-NearestRank -Values (20..1) -Percentile 0.95) -eq 19) 'p95 must sort and use nearest rank.'
Assert-True ((Get-NearestRank -Values @(7) -Percentile 0.95) -eq 7) 'One sample must work.'

# Exercise real argument boundaries and drain both pipes beyond their buffer capacity.
$result = Invoke-CapturedProcess -FilePath (Get-Process -Id $PID).Path -ArgumentList @(
    '-NoProfile', '-CommandWithArgs',
    '[Console]::Out.Write($args[0]); [Console]::Out.Write("x" * 100000); [Console]::Error.Write("y" * 100000)',
    'a path with spaces'
)
Assert-True ($result.ExitCode -eq 0) 'The captured process must succeed.'
Assert-True ($result.Stdout.StartsWith('a path with spaces')) 'ArgumentList must preserve spaces.'
Assert-True ($result.Stderr.Length -eq 100000) 'Stderr must remain separate and fully drained.'

$script:Calls = [System.Collections.Generic.List[string[]]]::new()
$script:StatusExit = 0
$script:CommandExit = 0
$script:Elapsed = '1.5'
$script:Malformed = $false
$script:BuildExit = 0
function Invoke-CapturedProcess {
    param([string]$FilePath, [string[]]$ArgumentList)
    $script:Calls.Add($ArgumentList)
    if ($ArgumentList[0] -eq 'build') {
        return [pscustomobject]@{ ExitCode = $script:BuildExit; Stdout = 'Release build'; Stderr = '' }
    }
    if ($ArgumentList[1] -eq 'index-status') {
        return [pscustomobject]@{ ExitCode = $script:StatusExit; Stdout = ''; Stderr = '' }
    }
    Assert-True ($env:MUDDOCS_TIMING -eq '1') 'Timing must be enabled for measured children.'
    # Stdout deliberately contains misleading timing; only stderr may be parsed.
    $stderr = if ($script:Malformed) { 'elapsed-ms=1,5' } else { "elapsed-ms=$script:Elapsed`nprocess-ms=999" }
    return [pscustomobject]@{ ExitCode = $script:CommandExit; Stdout = 'elapsed-ms=9999'; Stderr = $stderr }
}

$previousTiming = $env:MUDDOCS_TIMING
$previousCulture = [System.Globalization.CultureInfo]::CurrentCulture
try {
    $env:MUDDOCS_TIMING = 'previous'
    [System.Globalization.CultureInfo]::CurrentCulture = [System.Globalization.CultureInfo]::GetCultureInfo('de-DE')
    $output = @(Invoke-CliLatency -NoBuild -DataPath 'a path with spaces' -Iterations 20)
    Assert-True ($output[-1] -eq 0) 'Elapsed targets should pass regardless of process time.'
    Assert-True ($env:MUDDOCS_TIMING -eq 'previous') 'The previous timing value must be restored.'
    Assert-True ($script:Calls.Count -eq 261) 'Run status once and exactly 13 commands 20 times.'
    $expected = @(
        'component MudButton', 'props MudButton --filter Color,Variant,Size', 'props MudDataGrid',
        'events MudButton', 'methods MudButton', 'api MudButton', 'enum Color',
        'example MudButton Filled', 'examples MudButton --names-only', 'search button',
        'list', 'categories', 'related MudButton'
    )
    for ($i = 0; $i -lt 13; $i++) {
        $tokens = $script:Calls[1 + $i * 20]
        Assert-True (($tokens[1..($tokens.Length - 5)] -join ' ') -eq $expected[$i]) "Wrong command at row $i."
        Assert-True (($tokens[-4..-1] -join '|') -eq '--version|9.0.0|--data-path|a path with spaces') 'Global arguments must be forwarded intact.'
    }
    Assert-True (($output -join "`n") -match 'p95 elapsed') 'The script must print the required table.'
    $script:Elapsed = '50.0'
    $previousError = [Console]::Error
    $warning = [System.IO.StringWriter]::new()
    try {
        [Console]::SetError($warning)
        Assert-True (@(Invoke-CliLatency -NoBuild -Iterations 1)[-1] -eq 0) 'The budget boundary is inclusive.'
        Assert-True ($warning.ToString() -eq '') 'The inclusive boundary must not warn.'
        $script:Elapsed = '50.1'
        $output = @(Invoke-CliLatency -NoBuild -Iterations 1)
        Assert-True ($output[-1] -eq 0) 'Above-target measurements must succeed by default.'
        Assert-True ($output.Count -eq 16) 'Above-target measurements must retain the complete table.'
        Assert-True ($warning.ToString() -match 'warning:.*13 commands.*above.*target') 'Stderr must explicitly warn with the above-target command count.'
        Assert-True ($env:MUDDOCS_TIMING -eq 'previous') 'Restore timing after default warnings.'
        Assert-True (@(Invoke-CliLatency -NoBuild -Iterations 1 -StrictBudget)[-1] -eq 1) 'StrictBudget must fail above target.'
        Assert-True ($env:MUDDOCS_TIMING -eq 'previous') 'Restore timing after strict budget failure.'
        $script:Elapsed = '50.0'
        Assert-True (@(Invoke-CliLatency -NoBuild -Iterations 1 -StrictBudget)[-1] -eq 0) 'The strict budget boundary is inclusive.'
    }
    finally {
        [Console]::SetError($previousError)
        $warning.Dispose()
    }
    $script:StatusExit = 3
    $script:Calls.Clear()
    Assert-True (@(Invoke-CliLatency -NoBuild -Iterations 1)[-1] -eq 2) 'An unindexed version must return 2.'
    Assert-True ($script:Calls.Count -eq 1) 'Do not benchmark an unindexed version.'
    $script:StatusExit = 0
    $script:CommandExit = 4
    Assert-True (@(Invoke-CliLatency -NoBuild -Iterations 1)[-1] -eq 1) 'A failed command must fail the benchmark.'
    $script:CommandExit = 0
    $script:Malformed = $true
    Assert-True (@(Invoke-CliLatency -NoBuild -Iterations 1)[-1] -eq 1) 'Malformed or missing timing must fail.'
    Assert-True ($env:MUDDOCS_TIMING -eq 'previous') 'Restore timing even after failures.'
    $script:Malformed = $false
    $script:Elapsed = '1.5'
    $script:Calls.Clear()
    Assert-True (@(Invoke-CliLatency -Iterations 1)[-1] -eq 0) 'The default build path must succeed.'
    Assert-True (($script:Calls[0][0] -eq 'build') -and ($script:Calls[0][-1] -eq 'Release')) 'Build the main project in Release by default.'
    Assert-True ($script:Calls[0][1].EndsWith((Join-Path 'src' 'MudDocs' 'MudDocs.csproj'))) 'Build only the main project.'
    $script:BuildExit = 1
    $script:Calls.Clear()
    Assert-True (@(Invoke-CliLatency -Iterations 1)[-1] -eq 1) 'A build failure must stop the benchmark.'
    Assert-True ($script:Calls.Count -eq 1) 'Do not run commands after a failed build.'
    $script:BuildExit = 0
    $env:MUDDOCS_TIMING = $null
    Assert-True (@(Invoke-CliLatency -NoBuild -Iterations 1)[-1] -eq 0) 'An initially absent timing variable must work.'
    Assert-True ($null -eq [Environment]::GetEnvironmentVariable('MUDDOCS_TIMING')) 'Restore an absent timing variable.'
    Write-Output 'PASS: CLI latency self-checks.'
}
finally {
    [System.Globalization.CultureInfo]::CurrentCulture = $previousCulture
    $env:MUDDOCS_TIMING = $previousTiming
}
