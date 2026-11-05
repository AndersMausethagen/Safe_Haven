param([int]$ProcessId, [string]$Csv, [int]$IntervalSeconds = 30)
# Logs the Unity Editor's handle counts by type to a CSV until the process exits. Touches nothing in Unity.
$counter = Join-Path $PSScriptRoot 'HandleTypes.ps1'
if (-not (Test-Path $Csv)) { 'time,total,user,gdi,threads,Token,Thread,Event,File,Semaphore,Timer' | Out-File $Csv -Encoding utf8 }
Add-Type 'using System;using System.Runtime.InteropServices;public class GR{[DllImport("user32.dll")]public static extern int GetGuiResources(IntPtr h,int f);}'
while ($true) {
    $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $p) { break }
    $c = & $counter $ProcessId
    '{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}' -f (Get-Date -Format 'HH:mm:ss'), $p.HandleCount,
        [GR]::GetGuiResources($p.Handle, 1), [GR]::GetGuiResources($p.Handle, 0), $p.Threads.Count,
        $c.Token, $c.Thread, $c.Event, $c.File, $c.Semaphore, $c.Timer | Out-File $Csv -Append -Encoding utf8
    Start-Sleep $IntervalSeconds
}
