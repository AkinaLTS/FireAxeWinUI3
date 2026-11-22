# Source: original P7 CI test runner for FireAxeWinUI3.
# Runs the FireAxe.Tests xUnit suite as a child process with a hard timeout, so a stuck
# test host fails the build in minutes instead of blocking until the job timeout.
# Requires PowerShell 7 on Windows. Never launches the application.
<#
.SYNOPSIS
Build (optionally) and run FireAxe.Tests with a bounded timeout.
.EXAMPLE
pwsh -File ./scripts/Test-WindowsTests.ps1
.EXAMPLE
pwsh -File ./scripts/Test-WindowsTests.ps1 -SkipBuild -TimeoutMinutes 10
.DESCRIPTION
Writes tests.trx under -ResultsDirectory (default artifacts/tests) and prints the captured
output after the run. Health checks under FIREAXE_INTEGRATION_HEALTH are opt-in and stay skipped.
#>
[CmdletBinding()]
param(
    [switch] $SkipBuild,
    [int] $TimeoutMinutes = 10,
    [string] $ResultsDirectory = 'artifacts/tests'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Running FireAxe tests requires Windows and PowerShell 7.' }
if ($TimeoutMinutes -lt 1) { throw "TimeoutMinutes must be positive, got $TimeoutMinutes." }

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$solution = Join-Path $repositoryRoot 'FireAxe.sln'
$testProject = Join-Path $repositoryRoot 'FireAxe.Tests/FireAxe.Tests.csproj'
$resultsPath = [IO.Path]::GetFullPath($ResultsDirectory, $repositoryRoot)
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
$logPath = Join-Path $resultsPath 'dotnet-test.log'
$errorPath = Join-Path $resultsPath 'dotnet-test.stderr.log'

Push-Location $repositoryRoot
try {
    if (-not $SkipBuild) {
        & dotnet build $solution -c Release -p:Platform=x64 --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)." }
    }

    # Start-Process with redirection hands us output that survives a kill; `& dotnet test | Tee`
    # would block on the pipe when the test host never exits.
    $arguments = @('test', $testProject, '-c', 'Release', '-p:Platform=x64', '--no-build', '--nologo',
        '--logger', 'trx;LogFileName=tests.trx', '--results-directory', $resultsPath)
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $process = Start-Process dotnet -ArgumentList $arguments -NoNewWindow -PassThru `
        -RedirectStandardOutput $logPath -RedirectStandardError $errorPath
    $finished = $process.WaitForExit($TimeoutMinutes * 60000)
    if (-not $finished) {
        $process.Kill($true)
        $process.WaitForExit()
        Get-Content $logPath -ErrorAction SilentlyContinue
        if (Test-Path $errorPath) {
            $errContent = Get-Content $errorPath -ErrorAction SilentlyContinue
            if ($errContent) {
                Write-Host '--- stderr ---'
                $errContent
            }
        }
        throw "dotnet test did not finish within $TimeoutMinutes minutes; see $logPath"
    }
    $process.WaitForExit()
    "dotnet test finished in $([int] $stopwatch.Elapsed.TotalSeconds)s with exit code $($process.ExitCode)."

    Get-Content $logPath -ErrorAction SilentlyContinue
    if ((Get-Item $errorPath).Length -gt 0) {
        Write-Host '--- stderr ---'
        Get-Content $errorPath
    }
    $trx = Join-Path $resultsPath 'tests.trx'
    if (-not (Test-Path $trx)) {
        Get-Content $errorPath -ErrorAction SilentlyContinue | Out-Host
        throw "No test results were produced at $trx."
    }
    [xml] $report = Get-Content $trx -Raw
    $summary = $report.TestRun.ResultSummary
    $counters = $summary.Counters
    $executed = [int] $counters.executed
    $failed = [int] $counters.failed
    # The TRX outcome reads "Completed" even when every test passed; the counters and the
    # dotnet exit code are the authoritative signals.
    "Tests: total $($counters.total) / executed $executed / passed $($counters.passed) / failed $failed / skipped $($counters.notExecuted)"
    if ($summary.outcome -ne 'Completed' -or $executed -le 0 -or $failed -gt 0 -or $process.ExitCode -ne 0) {
        throw "Test run failed: outcome $($summary.outcome), executed $executed, failed $failed, exit code $($process.ExitCode)."
    }
}
finally { Pop-Location }
