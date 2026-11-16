<#
.SYNOPSIS
Two built copies of the game play co-op on this machine over 127.0.0.1 (Docs/BugPatterns.md, second round, fix 8).
.DESCRIPTION
Starts Build/Windows/Safe Haven.exe twice, small and windowed, as "-coopSmoke host" and "-coopSmoke guest" (CoopSmokeTest):
the host opens a new Haven with a lit stone, the guest joins, checks the host's settlement came as a copy beside its own, lays a
Pact Rune that must reach the host, saves while visiting (its own Haven must be what is saved) and leaves for home. Each copy
writes PASS or FAIL with what it saw, and quits. Saves, logs and reports go to a fresh temp folder; the player's saves, Steam
and the playtest logs are not touched. Build first (BuildWindows.ps1) so the copies have today's code. The two copies took about
8 GB together (2026-11-16): beside the Editor (7 GB and its import workers) that ran the machine out of memory, so close the Editor
first; the script refuses to start under 9 GB free. Passed in 40 s on 2026-11-16.
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/CoopTwoCopies.ps1
#>
param(
    [string]$Exe = 'D:/Safe Haven/Safe Haven/Build/Windows/Safe Haven.exe',
    [int]$TimeoutSeconds = 480,
    [double]$MinimumFreeGB = 9
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) {
    Write-Output "[CoopTwoCopies] No build at $Exe."
    exit 2
}

$free = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1MB, 1)
if ($free -lt $MinimumFreeGB) {
    Write-Output "[CoopTwoCopies] Only $free GB of RAM free; two copies need about 8. Close the Unity Editor (and other big programs) first."
    exit 3
}

$folder = Join-Path $env:TEMP ("SafeHavenCoopTwoCopies_" + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Force $folder | Out-Null
$copies = @{}
foreach ($role in 'host', 'guest') {
    $arguments = @('-coopSmoke', $role, '-coopSmokeDir', "`"$folder`"", '-logFile', "`"$(Join-Path $folder "$role.log")`"",
        '-screen-fullscreen', '0', '-screen-width', '960', '-screen-height', '540')
    $copies[$role] = Start-Process -FilePath $Exe -ArgumentList $arguments -PassThru
    Write-Output "[CoopTwoCopies] $role started (pid $($copies[$role].Id))"
    Start-Sleep -Seconds 5
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline -and -not ((Test-Path "$folder/host.txt") -and (Test-Path "$folder/guest.txt"))) {
    if (($copies.Values | Where-Object { -not $_.HasExited }).Count -eq 0) { break }
    Start-Sleep -Seconds 3
}

Start-Sleep -Seconds 5
$passed = $true
foreach ($role in 'host', 'guest') {
    $report = Join-Path $folder "$role.txt"
    if (Test-Path $report) {
        $lines = Get-Content $report
        Write-Output "[CoopTwoCopies] $role : $($lines -join ' / ')"
        if ($lines[0] -ne 'PASS') { $passed = $false }
    }
    else {
        $passed = $false
        Write-Output "[CoopTwoCopies] $role : no report (crashed or still running). Its log: $(Join-Path $folder "$role.log")"
    }

    if (-not $copies[$role].HasExited) {
        Stop-Process -Id $copies[$role].Id -Force
    }
}

Write-Output "[CoopTwoCopies] $(if ($passed) { 'PASSED' } else { 'FAILED' }) (logs and saves in $folder)"
if ($passed) { exit 0 } else { exit 1 }
