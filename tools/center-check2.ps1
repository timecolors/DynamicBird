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
# 先确认进程和鼠标
$targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
'PID=' + $targetPid
$pt = New-Object P+POINT
[P]::GetCursorPos([ref]$pt) | Out-Null
'CURSOR_BEFORE=(' + $pt.X + ',' + $pt.Y + ')'
# 触发顶边任务栏 x=500
[P]::SetCursorPos(500, 3) | Out-Null
Start-Sleep -Milliseconds 2500
$h = Get-Hwnd
'HWND=' + $h
if ($h -ne [IntPtr]::Zero) {
  $r = Get-Rect $h
  $cx = [math]::Round(($r.L + $r.R) / 2)
  'TOP-LEFT mouseX=500 panelW=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T) + ' centerX=' + $cx + ' diff=' + ($cx-500)
  # 平滑移到 x=2000
  for ($x = 500; $x -le 2000; $x += 100) { [P]::SetCursorPos($x, 3) | Out-Null; Start-Sleep -Milliseconds 20 }
  Start-Sleep -Milliseconds 800
  $r = Get-Rect $h
  $cx = [math]::Round(($r.L + $r.R) / 2)
  'TOP-RIGHT mouseX=2000 panelW=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T) + ' centerX=' + $cx + ' diff=' + ($cx-2000)
} else { 'NO HWND' }