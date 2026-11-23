<#
.SYNOPSIS
Publishes the Windows build to itch.io with butler and announces it in the playtest Discord's #builds (Docs/PlaytestDiscord.md).
.DESCRIPTION
1. butler push Build/Windows to the itch project's "windows" channel. Only what changed is uploaded, and testers using the itch app
   update by themselves. The "BackUpThisFolder" debug folder stays out.
2. Posts in #builds through the webhook: the version, the download page, and what changed since the last published build (the commits'
   first sentences).

Reads AgentScripts/discord.local.json (kept out of git):
    "builds"    the #builds webhook (made by Tools/Discord/setup_server.py)
    "itch"      the butler target, "user/project" (for example "ander/safe-haven")
    "itch_page" the page testers download from (for example "https://ander.itch.io/safe-haven")
Remembers the last published commit there too ("published").
.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/PublishBuild.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/PublishBuild.ps1 -Note "Test the new horse quest"
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/PublishBuild.ps1 -NoPost
#>
param(
    [string]$Note = '',
    [switch]$NoPost,
    [string]$Project = 'D:/Safe Haven/Safe Haven'
)

$ErrorActionPreference = 'Stop'
$configPath = Join-Path $Project 'AgentScripts/discord.local.json'
$config = Get-Content $configPath -Raw | ConvertFrom-Json
foreach ($key in 'itch', 'itch_page') {
    if (-not $config.$key) { throw "Put `"$key`" in $configPath (see this script's notes)." }
}

$butler = (Get-Command butler -ErrorAction SilentlyContinue).Source
if (-not $butler) { $butler = Join-Path $env:LOCALAPPDATA 'butler/butler.exe' }
if (-not (Test-Path $butler)) { throw 'butler is not installed (see Docs/PlaytestDiscord.md).' }

$build = Join-Path $Project 'Build/Windows'
if (-not (Test-Path (Join-Path $build 'Safe Haven.exe'))) { throw "No build in ${build}: run BuildWindows.ps1 first." }

Push-Location $Project
$commit = (git rev-parse --short HEAD).Trim()
$since = if ($config.published) { "$($config.published)..HEAD" } else { 'HEAD~10..HEAD' }
$subjects = @(git log --format=%s $since)
Pop-Location
$version = "$(Get-Date -Format 'yyyy-MM-dd')-$commit"

# The game's "Send my reports" posts straight to #playtest-logs with this (PlaytestReportWindow.UploadAddress). It goes only into the
# build that is shipped, never into the project.
if ($config.logs) {
    $streaming = Join-Path $build 'Safe Haven_Data/StreamingAssets'
    New-Item -ItemType Directory -Force $streaming | Out-Null
    Set-Content -Path (Join-Path $streaming 'playtest-upload.txt') -Value $config.logs -NoNewline -Encoding ASCII
    Write-Output '[Publish] The build sends reports to #playtest-logs.'
}

Write-Output "[Publish] butler push $build -> $($config.itch):windows ($version)"
& $butler push $build "$($config.itch):windows" --userversion $version --ignore 'Safe Haven_BackUpThisFolder_ButDontShipItWithYourGame'
if ($LASTEXITCODE -ne 0) { throw "butler push failed ($LASTEXITCODE)." }

# What changed: each commit's first sentence, newest first, as much as a Discord message holds.
$lines = foreach ($subject in $subjects) {
    $first = ($subject -split '(?<=[.:;])\s')[0]
    if ($first.Length -gt 160) { $first = $first.Substring(0, 157) + '...' }
    "- $first"
}
$changes = ($lines -join "`n")
if ($changes.Length -gt 1300) { $changes = $changes.Substring(0, 1300) + "`n- ...and more" }
$message = "**New build: $version**`nDownload: $($config.itch_page) (the itch app updates it by itself)`n"
if ($Note) { $message += "`n**What to test:** $Note`n" }
if ($changes) { $message += "`n**What changed**`n$changes" }

if (-not $NoPost -and $config.builds) {
    $body = @{ content = $message; allowed_mentions = @{ parse = @() } } | ConvertTo-Json -Depth 4
    Invoke-RestMethod -Uri $config.builds -Method Post -ContentType 'application/json; charset=utf-8' -Body ([System.Text.Encoding]::UTF8.GetBytes($body)) | Out-Null
    Write-Output '[Publish] Posted in #builds.'
}

$config | Add-Member -NotePropertyName published -NotePropertyValue $commit -Force
$config | ConvertTo-Json | Set-Content $configPath -Encoding UTF8
Write-Output "[Publish] Done: $version"
