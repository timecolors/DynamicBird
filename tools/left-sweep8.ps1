$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class P {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
[P]::SetProcessDPIAware() | Out-Null
$pt = New-Object P+POINT
[P]::GetCursorPos([ref]$pt) | Out-Null
if ($pt.X -gt 20) { 'CURSOR MOVED: (' + $pt.X + ',' + $pt.Y + ')'; exit 1 }
'CURSOR OK'
$targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
$found = [IntPtr]::Zero
$cb = [P+EnumProc]{ param($h, $l) $pid2 = 0; [P]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null; if ($pid2 -eq $targetPid -and [P]::IsWindowVisible($h)) { $sb = New-Object System.Text.StringBuilder 256; [P]::GetWindowText($h, $sb, 256) | Out-Null; $t = $sb.ToString(); $tt = [char]0x7075 + [char]0x52A8 + [char]0x9E1F; if ($t -eq $tt) { $script:found = $h } }; return $true }
[P]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($found -eq [IntPtr]::Zero) { 'WINDOW NOT FOUND'; exit 1 }
Start-Sleep -Milliseconds 2000
$r = New-Object P+RECT
[P]::GetWindowRect($found, [ref]$r) | Out-Null
'SUMMON rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ') W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)
$out = [System.Collections.Generic.List[string]]::new()
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($round = 1; $round -le 4; $round++) {
    for ($y = 200; $y -le 1400; $y += 80) { [P]::SetCursorPos(3, $y) | Out-Null; $r2 = New-Object P+RECT; [P]::GetWindowRect($found, [ref]$r2) | Out-Null; $out.Add('' + $sw.ElapsedMilliseconds + ',' + $r2.L + ',' + $r2.T + ',' + $r2.R + ',' + $r2.B); Start-Sleep -Milliseconds 10 }
    for ($y = 1400; $y -ge 200; $y -= 80) { [P]::SetCursorPos(3, $y) | Out-Null; $r2 = New-Object P+RECT; [P]::GetWindowRect($found, [ref]$r2) | Out-Null; $out.Add('' + $sw.ElapsedMilliseconds + ',' + $r2.L + ',' + $r2.T + ',' + $r2.R + ',' + $r2.B); Start-Sleep -Milliseconds 10 }
}
$wf = 'D:\' + [char]0x7075 + [char]0x52A8 + [char]0x9E1F + '\tools\flick\left-sweep-raw4.csv'
[System.IO.File]::WriteAllLines($wf, $out)
'SWEEP DONE samples=' + $out.Count