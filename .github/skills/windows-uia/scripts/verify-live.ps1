# Usage: ./verify-live.ps1 -EditorExecutable <real-editor.exe> -EvidenceDirectory <new-directory> [-AttachedProcessId <pid> -AttachedScenarioPath <json> -SettingsFile <path>]
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EditorExecutable,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [int]$AttachedProcessId,
    [string]$AttachedScenarioPath,
    [string]$SettingsFile,
    [string]$TalkToMeAudioPath
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($EvidenceDirectory)
if (Test-Path $root) { throw 'Use a fresh verification directory.' }
$oldTarget = $env:WINDOWS_UIA_TARGET_EXE
$env:WINDOWS_UIA_TARGET_EXE = (Resolve-Path $EditorExecutable).Path
$assets = Join-Path $PSScriptRoot '..\assets'
$runner = Join-Path $PSScriptRoot 'invoke-uia.ps1'
$results = [ordered]@{}
try {
    $success = Join-Path $root 'editor-success'
    & $runner -Action Run -ScenarioPath (Join-Path $assets 'editor-journey.json') -EvidenceDirectory $success
    $owner = Get-Content (Join-Path $success 'process.json') -Raw | ConvertFrom-Json
    if (Get-Process -Id $owner.processId -ErrorAction SilentlyContinue) { throw 'Successful journey left its owned editor running.' }
    $tree = Get-Content (Join-Path $success 'after-tree.json') -Raw | ConvertFrom-Json
    if ($tree.nodes.Count -eq 0 -or @($tree.nodes | Where-Object { $null -ne $_.name }).Count -ne 0) {
        throw 'Default inspection exposed names or produced no tree.'
    }
    $results.EditorValueAssertion = 'PASS'
    $results.DefaultTextPrivacy = 'PASS'
    $failure = Join-Path $root 'editor-timeout'
    $failed = $false
    try { & $runner -Action Run -ScenarioPath (Join-Path $assets 'editor-timeout.json') -EvidenceDirectory $failure }
    catch { $failed = $true }
    if (-not $failed) { throw 'Absent-control journey unexpectedly passed.' }
    $outcome = Get-Content (Join-Path $failure 'result.json') -Raw | ConvertFrom-Json
    $owner = Get-Content (Join-Path $failure 'process.json') -Raw | ConvertFrom-Json
    if ($outcome.result -ne 'FAIL' -or $outcome.exceptionType -ne 'TimeoutException') { throw 'Failure evidence does not identify the real UIA timeout.' }
    if (Get-Process -Id $owner.processId -ErrorAction SilentlyContinue) { throw 'Failed journey left its owned editor running.' }
    $results.TimeoutAndOwnedCleanup = 'PASS'
    if ($AttachedProcessId -gt 0) {
        if (-not $AttachedScenarioPath) { throw 'Attach verification requires an explicit scenario.' }
        $before = if ($SettingsFile) { (Get-FileHash $SettingsFile).Hash } else { $null }
        & $runner -Action Run -ProcessId $AttachedProcessId -ScenarioPath $AttachedScenarioPath -EvidenceDirectory (Join-Path $root 'attached')
        if (-not (Get-Process -Id $AttachedProcessId -ErrorAction SilentlyContinue)) { throw 'Attached process did not remain alive.' }
        if ($SettingsFile -and (Get-FileHash $SettingsFile).Hash -ne $before) { throw 'Attached journey changed persisted settings.' }
        $results.AttachedProcessPreserved = 'PASS'
        if ($SettingsFile) { $results.PersistedSettingsPreserved = 'PASS' }
    }
    if ($TalkToMeAudioPath) {
        $adapter = Join-Path $PSScriptRoot 'invoke-talk-to-me.ps1'
        $blocked = $false
        try { & $adapter -Scenario Provider -EvidenceDirectory (Join-Path $root 'paid-blocked') }
        catch { if ($_.Exception.Message -like 'This journey can submit paid*') { $blocked = $true } else { throw } }
        if (-not $blocked -or (Test-Path (Join-Path $root 'paid-blocked'))) { throw 'Unapproved paid journey was not blocked before side effects.' }
        $results.PaidRequestAuthorizationGuard = 'PASS'
        if (@(Get-Process TalkToMe.App -ErrorAction SilentlyContinue).Count -gt 0) {
            $blocked = $false
            try { & $adapter -Scenario RecordOnly -AudioPath $TalkToMeAudioPath -EvidenceDirectory (Join-Path $root 'running-app-blocked') }
            catch { if ($_.Exception.Message -like 'TalkToMe is running.*') { $blocked = $true } else { throw } }
            if (-not $blocked) { throw 'Adapter did not preserve the existing app by default.' }
            $results.RunningTalkToMeGuard = 'PASS'
        }
    }
    $results | ConvertTo-Json | Set-Content (Join-Path $root 'verification.json') -Encoding UTF8
    'PASS real UIA success/failure, privacy and selected ownership/authorization safeguards'
} finally { $env:WINDOWS_UIA_TARGET_EXE = $oldTarget }