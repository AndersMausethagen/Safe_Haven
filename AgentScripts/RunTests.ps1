<#
.SYNOPSIS
Runs Unity tests through the unity CLI without the "Scene(s) Have Been Modified" dialog stalling the Editor.

.DESCRIPTION
The World Builder dirties SafeHaven after the Editor starts and after feature-field changes. A test run then waits on
Unity's native save dialog and every CLI command times out until someone answers it. This script:
  1. answers that dialog with Save if it is already up (a stuck earlier run);
  2. saves open scenes that are dirty (refuses in Play mode: stop Play first);
  3. runs the tests, clicking Save on the dialog if it appears anyway, and reports any other Editor dialog it sees;
  4. prints the CLI's output and the totals from TestResults.xml.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/RunTests.ps1 -Filter CoopTests
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/RunTests.ps1 -Filter "WeatherTests;CoopTests" -Timeout 600
powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/RunTests.ps1 -All -Timeout 3600
  (-All: the whole suite of the mode in one run, every class, with only the totals and what failed reported. Run it before
  a build: AgentScripts/BuildWindows.ps1 does.)
#>
param(
    [string]$Filter = '',
    [switch]$All,
    [int]$Timeout = 300,
    [ValidateSet('EditMode', 'PlayMode')][string]$Mode = 'EditMode',
    [string]$Project = 'D:/Safe Haven/Safe Haven'
)

if (-not $All -and [string]::IsNullOrWhiteSpace($Filter)) { Write-Output '[RunTests] Name a -Filter (a class, or "A;B"), or -All.'; exit 2 }

# Continue: the CLI writes hints to stderr, which Windows PowerShell would otherwise turn into errors.
$ErrorActionPreference = 'Continue'
$unity = (Get-Command unity).Source
$results = Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\Safe Haven\TestResults.xml'

if (-not ('SafeHavenDialogs' -as [type])) {
    Add-Type @'
using System; using System.Text; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class SafeHavenDialogs
{
    private delegate bool Visit(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Visit visit, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, Visit visit, IntPtr data);
    [DllImport("user32.dll")] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern int GetClassName(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
    private const uint ButtonClick = 0x00F5;

    private static string Text(IntPtr window) { var text = new StringBuilder(256); GetWindowText(window, text, 256); return text.ToString(); }
    private static string Class(IntPtr window) { var text = new StringBuilder(64); GetClassName(window, text, 64); return text.ToString(); }

    // Native dialogs (#32770) of the given processes: clicks Save on the modified-scenes one, returns what it saw.
    public static List<string> Answer(HashSet<uint> processes)
    {
        var seen = new List<string>();
        EnumWindows((window, data) =>
        {
            uint process;
            GetWindowThreadProcessId(window, out process);
            if (!processes.Contains(process) || !IsWindowVisible(window) || Class(window) != "#32770") return true;
            string title = Text(window);
            if (title == "Scene(s) Have Been Modified")
            {
                IntPtr save = IntPtr.Zero;
                EnumChildWindows(window, (child, d) => { if (Text(child) == "Save") { save = child; return false; } return true; }, IntPtr.Zero);
                if (save != IntPtr.Zero) { SendMessage(save, ButtonClick, IntPtr.Zero, IntPtr.Zero); seen.Add("clicked Save on: " + title); }
                else seen.Add("no Save button on: " + title);
            }
            else
            {
                seen.Add("other Editor dialog open: " + title);
            }
            return true;
        }, IntPtr.Zero);
        return seen;
    }
}
'@
}

# Unity's own progress windows come and go by themselves; anything else is worth a look.
$passing = 'Hold on|Reloading Domain|Running managed callbacks|Compiling|Importing|Refresh|Unity Version Control|Loading|Saving|Building|Progress'

function Answer-Dialogs {
    $ids = New-Object 'System.Collections.Generic.HashSet[uint32]'
    foreach ($p in Get-Process Unity -ErrorAction SilentlyContinue) { [void]$ids.Add([uint32]$p.Id) }
    foreach ($line in [SafeHavenDialogs]::Answer($ids)) {
        if ($line -notmatch "other Editor dialog open: .*($passing)") { Write-Output "[RunTests] $line" }
    }
}

# 1. A dialog left by an earlier run blocks the CLI: answer it first. Then wait out a recompile or domain reload.
Answer-Dialogs
$deadline = (Get-Date).AddSeconds(180)
while ((Get-Date) -lt $deadline) {
    $status = & $unity status 2>&1 | Out-String
    if ($status -match '\bready\b') { break }
    Answer-Dialogs
    Start-Sleep -Seconds 2
}
Start-Sleep -Milliseconds 500

# 2. Save dirty scenes so the runner has nothing to ask about.
$probe = Join-Path $env:TEMP 'SafeHavenRunTests_SaveScenes.cs'
@'
if (UnityEditor.EditorApplication.isPlaying) return "playing";
var dirty = new System.Collections.Generic.List<string>();
for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
{
    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
    if (scene.isDirty) dirty.Add(scene.name);
}
if (dirty.Count > 0) UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
return dirty.Count == 0 ? "clean" : "saved " + string.Join(", ", dirty);
'@ | Set-Content -Path $probe -Encoding utf8
$saved = & $unity command script eval_file --no-banner -- --file $probe 2>&1 | Out-String
if ($saved -match '"result":"playing"') { Write-Output '[RunTests] The Editor is in Play mode: stop Play first.'; exit 2 }
if ($saved -match '"result":"([^"]*)"') { Write-Output "[RunTests] scenes: $($Matches[1])" } else { Write-Output "[RunTests] scene check failed: $saved" }

# 3. Run the tests, watching for the dialog while they run. The CLI's filter takes one name (a class, or any part of a
# test's full name), so "A;B" runs each in turn and the script exits non-zero if any run failed or did not finish.
$out = Join-Path $env:TEMP 'SafeHavenRunTests_out.txt'
$err = Join-Path $env:TEMP 'SafeHavenRunTests_err.txt'
$failedRuns = 0
$names = if ($All) { @('') } else { @($Filter -split ';' | ForEach-Object { $_.Trim() } | Where-Object { $_ }) }
foreach ($name in $names) {
    $before = if (Test-Path $results) { (Get-Item $results).LastWriteTime } else { [datetime]::MinValue }
    $arguments = if ($All) { @('command', 'test', 'run', '--mode', $Mode, '--no-banner', '--timeout', "$Timeout") }
        else { @('command', 'test', 'run', '--mode', $Mode, '--filter', "`"$name`"", '--no-banner', '--timeout', "$Timeout") }
    $run = Start-Process -FilePath $unity -ArgumentList $arguments -NoNewWindow -PassThru -RedirectStandardOutput $out -RedirectStandardError $err
    $reported = @{}
    while (-not $run.HasExited) {
        foreach ($line in Answer-Dialogs) { if (-not $reported.ContainsKey($line) -or $line -like '*clicked*') { $reported[$line] = $true; Write-Output $line } }
        Start-Sleep -Milliseconds 1500
    }

    # 4. What the CLI said, and the totals of this run.
    Get-Content $out, $err -ErrorAction SilentlyContinue | Where-Object { $_ -match '\S' } | Select-Object -Last $(if ($All) { 5 } else { 40 })
    if ($All -and (Test-Path $results) -and (Get-Item $results).LastWriteTime -gt $before) {
        # The whole suite: the totals, each class that failed, and each failure.
        [xml]$xml = Get-Content $results
        $r = $xml.'test-run'
        $fixtures = @($xml.SelectNodes("//test-suite[@type='TestFixture']"))
        Write-Output "[RunTests] all $Mode tests: $($r.passed)/$($r.total) passed, $($r.failed) failed, $($r.skipped) skipped, $($fixtures.Count) classes ($([math]::Round([double]$r.duration / 60, 1)) min)"
        foreach ($f in ($fixtures | Where-Object { [int]$_.failed -gt 0 })) { Write-Output "  class $($f.name): $($f.failed) failed of $($f.total)" }
        foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
            Write-Output "  FAILED $($case.fullname): $($case.failure.message.'#cdata-section')"
        }
        if ([int]$r.failed -gt 0) { $failedRuns++ }
        continue
    }

    if ((Test-Path $results) -and (Get-Item $results).LastWriteTime -gt $before) {
        [xml]$xml = Get-Content $results
        $r = $xml.'test-run'
        # The CLI's filter matches any part of a test's name, so "FishingTests" also runs CoopFishingTests: the totals
        # are for the class named, and any other class the filter caught is listed apart.
        $fixtures = @($xml.SelectNodes("//test-suite[@type='TestFixture']"))
        $named = @($fixtures | Where-Object { $_.name -eq $name })
        if ($named.Count -eq 1) {
            $f = $named[0]
            Write-Output "[RunTests] $name : $($f.result) - $($f.passed)/$($f.total) passed, $($f.failed) failed, $($f.skipped) skipped ($([math]::Round([double]$f.duration, 1)) s)"
        }
        else {
            Write-Output "[RunTests] $name : no class of that name ran; the filter matched: $(($fixtures | ForEach-Object { $_.name }) -join ', ')"
            $failedRuns++
        }
        foreach ($other in ($fixtures | Where-Object { $_.name -ne $name })) {
            Write-Output "  also matched $($other.name): $($other.passed)/$($other.total) passed, $($other.failed) failed"
        }
        foreach ($case in $xml.SelectNodes("//test-case[@result='Failed']")) {
            Write-Output "  FAILED $($case.fullname): $($case.failure.message.'#cdata-section')"
        }
        if ([int]$r.failed -gt 0) { $failedRuns++ }
    }
    else {
        Write-Output "[RunTests] $name : no new TestResults.xml, the run did not finish or matched no tests (see the output above)."
        $failedRuns++
    }
}
exit $(if ($failedRuns -gt 0) { 1 } else { 0 })
