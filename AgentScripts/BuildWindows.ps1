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
#>
param(
    [switch]$SkipTests,
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
$data = Join-Path $Project 'Build/Windows/Safe Haven_Data/globalgamemanagers'
$start = Get-Date
$answer = & $unity command script eval --no-banner --timeout 7200 --code 'SafeHavenBuild.BuildWindows(); return "built";' 2>&1 | Out-String
$written = (Test-Path $data) -and ((Get-Item $data).LastWriteTime -gt $start)
if ($answer -match '"result":"built"' -or ($written -and $answer -notmatch 'Exception|stopped|failed')) {
    Write-Output "[Build] Windows build done in $([math]::Round(((Get-Date) - $start).TotalMinutes, 1)) min: $Project/Build/Windows/Safe Haven.exe"
    exit 0
}

Write-Output '[Build] The build failed (or the audits stopped it):'
Write-Output $answer.Substring(0, [math]::Min(3000, $answer.Length))
exit 1
