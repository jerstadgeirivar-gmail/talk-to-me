# Usage: ./invoke-talk-to-me.ps1 -Scenario RecordOnly -AudioPath <real-audio> -EvidenceDirectory <new-directory>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('RecordOnly', 'Provider', 'Profiles', 'TokenLimit', 'AzureRepair', 'Vocabulary', 'Long')][string]$Scenario,
    [string]$ExecutablePath = "$env:LOCALAPPDATA\Programs\TalkToMe\TalkToMe.App.exe",
    [string]$AudioPath,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$TaskAllowsPaidRequests,
    [switch]$StopRunningApplication
)
$ErrorActionPreference = 'Stop'
if ($Scenario -ne 'RecordOnly' -and -not $TaskAllowsPaidRequests) {
    throw 'This journey can submit paid transcription requests. Use TaskAllowsPaidRequests only when the current task already authorizes them.'
}
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..\..'))
$evidence = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path $evidence) { throw 'Use a new evidence directory.' }
$executable = (Resolve-Path $ExecutablePath).Path
if ($Scenario -ne 'Provider') {
    if (-not $AudioPath) { throw 'This scenario requires a real AudioPath.' }
    $AudioPath = (Resolve-Path $AudioPath).Path
}
$running = @(Get-Process TalkToMe.App -ErrorAction SilentlyContinue)
if ($running.Count -gt 0 -and -not $StopRunningApplication) {
    throw 'TalkToMe is running. Prefer read-only PID inspection; stop it only when explicitly authorized and no unsaved work is at risk.'
}
foreach ($process in $running) { $process.Kill(); $process.WaitForExit() }
if (@(Get-Process TalkToMe.App -ErrorAction SilentlyContinue).Count -ne 0) { throw 'TalkToMe did not stop.' }
$project = Join-Path $root 'tools\TalkToMe.UiDriver\TalkToMe.UiDriver.csproj'
& dotnet build $project -c Debug --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'TalkToMe UI driver build failed.' }
$driver = Join-Path $root 'tools\TalkToMe.UiDriver\bin\Debug\net10.0-windows\TalkToMe.UiDriver.dll'
if ($Scenario -eq 'Provider') { $arguments = @($executable, '--live-provider', $evidence) }
else {
    $arguments = @($executable, $AudioPath, $evidence)
    $mode = @{ Profiles = '--segmentation'; TokenLimit = '--segmentation-token-limit'; AzureRepair = '--azure-error-retry'; Vocabulary = '--vocabulary-retry'; Long = '--segmentation-long' }
    if ($mode.ContainsKey($Scenario)) { $arguments += $mode[$Scenario] }
}
& dotnet $driver @arguments
if ($LASTEXITCODE -ne 0) { throw 'TalkToMe journey failed or was blocked. Inspect retained evidence; do not report PASS.' }