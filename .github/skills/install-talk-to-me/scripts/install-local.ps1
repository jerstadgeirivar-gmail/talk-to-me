<#
.SYNOPSIS
Stops TalkToMe, installs a built package silently, and verifies the installed binary.

.EXAMPLE
.\install-local.ps1 -InstallerPath .\artifacts\installer\TalkToMe-Setup.exe -ExpectedVersion 1.0.13 -LogPath .\artifacts\validation\install-1.0.13.log
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $InstallerPath,

    [Parameter(Mandatory)]
    [string] $ExpectedVersion,

    [Parameter(Mandatory)]
    [string] $LogPath
)

$ErrorActionPreference = 'Stop'
$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$resolvedLog = [System.IO.Path]::GetFullPath($LogPath)
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\TalkToMe'
$installedExecutable = Join-Path $installDirectory 'TalkToMe.App.exe'

$running = Get-Process -Name 'TalkToMe.App' -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
}

$remaining = Get-Process -Name 'TalkToMe.App' -ErrorAction SilentlyContinue
if ($remaining) {
    $ids = ($remaining.Id -join ', ')
    throw "TalkToMe.App is still running after termination (PID: $ids)."
}

$logDirectory = Split-Path -Parent $resolvedLog
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$arguments = @(
    '/VERYSILENT'
    '/SUPPRESSMSGBOXES'
    '/NORESTART'
    '/SP-'
    '/CLOSEAPPLICATIONS'
    "/DIR=$installDirectory"
    "/LOG=$resolvedLog"
)
$installer = Start-Process -FilePath $resolvedInstaller -ArgumentList $arguments -Wait -PassThru
if ($installer.ExitCode -ne 0) {
    throw "TalkToMe installer exited with code $($installer.ExitCode). See $resolvedLog."
}

if (-not (Test-Path -LiteralPath $installedExecutable -PathType Leaf)) {
    throw "Installed executable was not found at $installedExecutable."
}

$actualVersion = (Get-Item -LiteralPath $installedExecutable).VersionInfo.ProductVersion
if (-not $actualVersion.StartsWith($ExpectedVersion, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Installed version '$actualVersion' does not match expected version '$ExpectedVersion'."
}

[pscustomobject]@{
    Executable = $installedExecutable
    Version = $actualVersion
    InstallLog = $resolvedLog
    PriorProcessesStopped = $true
}