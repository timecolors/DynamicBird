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
$targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
'PID=' + $targetPid
$pt = New-Object P+POINT
[P]::GetCursorPos([ref]$pt) | Out-Null
'CURSOR_BEFORE=(' + $pt.X + ',' + $pt.Y + ')'
$h = Get-Hwnd
'HWND=' + $h
if ($h -eq [IntPtr]::Zero) { 'NO HWND'; exit 1 }
# 场景1：多次触发左上角（先移开，再触发）
for ($i = 1; $i -le 3; $i++) {
  [P]::SetCursorPos(1280, 800) | Out-Null
  Start-Sleep -Milliseconds 1500
  [P]::SetCursorPos(3, 3) | Out-Null
  Start-Sleep -Milliseconds 2000
  $r = Get-Rect $h
  'TRIGGER-TOPLEFT #' + $i + ': rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ') W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)
}
# 场景2：从左中划到左上
[P]::SetCursorPos(1280, 800) | Out-Null
Start-Sleep -Milliseconds 1500
[P]::SetCursorPos(3, 800) | Out-Null
Start-Sleep -Milliseconds 2000
$r = Get-Rect $h
'LEFT-CENTER: rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ') W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)
# 平滑划到左上
for ($y = 800; $y -ge 3; $y -= 60) { [P]::SetCursorPos(3, $y) | Out-Null; Start-Sleep -Milliseconds 15 }
Start-Sleep -Milliseconds 1000
$r = Get-Rect $h
'LEFT-CENTER-TO-TOPLEFT: rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ') W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)