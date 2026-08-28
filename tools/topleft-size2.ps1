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
function Get-Hwnd {
  $targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
  $found = [IntPtr]::Zero
  $cb = [P+EnumProc]{ param($h, $l) $pid2 = 0; [P]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null; if ($pid2 -eq $targetPid -and [P]::IsWindowVisible($h)) { $sb2 = New-Object System.Text.StringBuilder 256; [P]::GetWindowText($h, $sb2, 256) | Out-Null; $t2 = $sb2.ToString(); $tt2 = [char]0x7075 + [char]0x52A8 + [char]0x9E1F; if ($t2 -eq $tt2) { $script:found = $h } }; return $true }
  [P]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
  return $found
}
function Get-Rect($h) { $r = New-Object P+RECT; [P]::GetWindowRect($h, [ref]$r) | Out-Null; return $r }
$h = Get-Hwnd
if ($h -eq [IntPtr]::Zero) { 'NO HWND'; exit 1 }
'HWND=' + $h
# 触发1：左上角
[P]::SetCursorPos(1280, 800) | Out-Null
Start-Sleep -Milliseconds 1500
[P]::SetCursorPos(3, 3) | Out-Null
Start-Sleep -Milliseconds 2000
$r = Get-Rect $h
'TL-TRIGGER-1: W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T) + ' rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ')'
# 触发2：左上角（重新触发）
[P]::SetCursorPos(1280, 800) | Out-Null
Start-Sleep -Milliseconds 1500
[P]::SetCursorPos(3, 3) | Out-Null
Start-Sleep -Milliseconds 2000
$r = Get-Rect $h
'TL-TRIGGER-2: W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T) + ' rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ')'
# 触发3：左上角
[P]::SetCursorPos(1280, 800) | Out-Null
Start-Sleep -Milliseconds 1500
[P]::SetCursorPos(3, 3) | Out-Null
Start-Sleep -Milliseconds 2000
$r = Get-Rect $h
'TL-TRIGGER-3: W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T) + ' rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ')'
# 左中 -> 左上
[P]::SetCursorPos(3, 800) | Out-Null
Start-Sleep -Milliseconds 2000
$r = Get-Rect $h
'LC: W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)
for ($y = 760; $y -ge 3; $y -= 60) { [P]::SetCursorPos(3, $y) | Out-Null; Start-Sleep -Milliseconds 15 }
Start-Sleep -Milliseconds 1000
$r = Get-Rect $h
'LC-TO-TL: W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T) + ' rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ')'