<#
.SYNOPSIS
Runs every EditMode test, then makes the Windows build when they all pass (Docs/BugPatterns.md, second round: the whole suite
before a build, not the classes someone thought of).
.DESCRIPTION
1. RunTests.ps1 -All: the whole suite in one run (it answers the "Scene(s) Have Been Modified" dialog and saves dirty scenes).
2. SafeHavenBuild.BuildWindows through the CLI: the audits (QuestAudit, ContentAudit), then Build/Windows/Safe Haven.exe.
The CLI's answer can be lost while the Editor is busy building, so success is read from the build's own output being written.
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/BuildWindows.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/BuildWindows.ps1 -SkipTests
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/BuildWindows.ps1 -FullTextures   (a release build: 4K textures)
A playtest build (the default) keeps textures at the Editor's 2K cap; both are packed with LZ4HC.
#>
param(
    [switch]$SkipTests,
    [switch]$FullTextures,
    [int]$TestTimeout = 3600,
    [string]$Project = 'D:/Safe Haven/Safe Haven'
)

$ErrorActionPreference = 'Continue'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $SkipTests) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $here 'RunTests.ps1') -All -Timeout $TestTimeout
    if ($LASTEXITCODE -ne 0) {
        Write-Output '[Build] Tests failed or didn''t finish: no build.'
        exit 1
    }
}

$unity = (Get-Command unity).Source
# boot.config: written by every build, LZ4HC or not (a compressed build packs globalgamemanagers into data.unity3d).
$data = Join-Path $Project 'Build/Windows/Safe Haven_Data/boot.config'
$start = Get-Date
# No double quotes in the code: Windows PowerShell 5.1 strips them from a native command's arguments.
$code = if ($FullTextures) { 'SafeHavenBuild.BuildWindows(true); return 7;' } else { 'SafeHavenBuild.BuildWindows(false); return 7;' }
$answer = & $unity command script eval --no-banner --timeout 7200 --code $code 2>&1 | Out-String
# A shader cache Unity threw away (a URP or Editor update) can keep the build compiling variants for hours, past the CLI's wait;
# the Editor goes on building, so wait for its output instead of calling that a failure.
$waited = $answer -match 'did not complete within'
if ($waited) {
    Write-Output '[Build] Still building (shader variants?): waiting up to 4 more hours for the output.'
    $until = (Get-Date).AddHours(4)
    $idle = 0
    while ((Get-Date) -lt $until -and $idle -lt 3 -and -not ((Test-Path $data) -and ((Get-Item $data).LastWriteTime -gt $start))) {
        Start-Sleep -Seconds 60
        # A busy Editor names its progress bar in its window title ("... (busy for 2:00:12)..."); three idle looks: it stopped.
        $busy = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -match 'busy' }
        $idle = if ($busy) { 0 } else { $idle + 1 }
    }
    Start-Sleep -Seconds 60
}
$written = (Test-Path $data) -and ((Get-Item $data).LastWriteTime -gt $start)
# Unity CLI 1.0 prints the eval's value itself (7); earlier versions a JSON object with "result":7.
if ($answer -match '"result":7' -or $answer -match '(?m)^\s*7\s*$' -or ($written -and ($waited -or $answer -notmatch 'Exception|stopped|failed'))) {
    Write-Output "[Build] Windows build done in $([math]::Round(((Get-Date) - $start).TotalMinutes, 1)) min: $Project/Build/Windows/Safe Haven.exe"
    exit 0
}

Write-Output '[Build] The build failed (or the audits stopped it):'
Write-Output $answer.Substring(0, [math]::Min(3000, $answer.Length))
exit 1
