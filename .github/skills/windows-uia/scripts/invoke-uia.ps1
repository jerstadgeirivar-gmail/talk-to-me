# Usage: ./invoke-uia.ps1 -Action Inspect -ProcessId <pid> -EvidenceDirectory <new-directory> -Screenshots
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Discover', 'Inspect', 'Run')][string]$Action,
    [int]$ProcessId,
    [string]$WindowAutomationId,
    [string]$ScenarioPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$IncludeText,
    [switch]$Screenshots,
    [switch]$TaskAllowsPaidRequests
)
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'WindowsUia\WindowsUia.csproj'
$output = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path $output) { throw 'Use a new evidence directory to preserve previous evidence.' }
if ($Action -eq 'Run') {
    if (-not $ScenarioPath) { throw 'Run requires ScenarioPath.' }
    $ScenarioPath = (Resolve-Path $ScenarioPath).Path
    $scenario = Get-Content $ScenarioPath -Raw | ConvertFrom-Json
    if ($scenario.requiresPaidService -and -not $TaskAllowsPaidRequests) {
        throw 'This scenario requires paid requests. Supply TaskAllowsPaidRequests only when the current task already authorizes them.'
    }
}
& dotnet build $project -c Release --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Windows UIA tool build failed.' }
$tool = Join-Path $PSScriptRoot 'WindowsUia\bin\Release\net10.0-windows\WindowsUia.dll'
if ($Action -eq 'Run') {
    $arguments = @('run', $ScenarioPath, $output, $IncludeText.IsPresent.ToString(), $Screenshots.IsPresent.ToString())
    if ($ProcessId -gt 0) { $arguments += $ProcessId.ToString() }
} elseif ($Action -eq 'Discover') {
    $arguments = @('discover', $output, $IncludeText.IsPresent.ToString())
} else {
    if ($ProcessId -le 0) { throw 'Inspect requires an explicit ProcessId.' }
    $arguments = @('inspect', $ProcessId.ToString(), $output, $IncludeText.IsPresent.ToString(), $Screenshots.IsPresent.ToString())
    if ($WindowAutomationId) { $arguments += $WindowAutomationId }
}
& dotnet $tool @arguments
if ($LASTEXITCODE -ne 0) { throw 'Windows UIA operation failed; review terminal output and retained evidence.' }