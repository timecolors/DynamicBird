$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class P {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
"@
[P]::SetProcessDPIAware() | Out-Null
function Get-Hwnd {
  $targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
  $found = [IntPtr]::Zero
  $cb = [P+EnumProc]{ param($h, $l) $pid2 = 0; [P]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null; if ($pid2 -eq $targetPid -and [P]::IsWindowVisible($h)) { $sb = New-Object System.Text.StringBuilder 256; [P]::GetWindowText($h, $sb, 256) | Out-Null; $t = $sb.ToString(); $tt = [char]0x7075 + [char]0x52A8 + [char]0x9E1F; if ($t -eq $tt) { $script:found = $h } }; return $true }
  [P]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
  return $found
}
function Get-Rect($h) { $r = New-Object P+RECT; [P]::GetWindowRect($h, [ref]$r) | Out-Null; return $r }
$h = Get-Hwnd
$r = Get-Rect $h
'START rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ')'
# 快速上下扫 4 轮（Left_Top Widget <-> Left_Center AI <-> Left_Bottom Widget）
$hidden = 0; $minT = 99999
for ($round = 1; $round -le 4; $round++) {
    for ($y = 200; $y -le 1400; $y += 80) { [P]::SetCursorPos(3, $y) | Out-Null; Start-Sleep -Milliseconds 10 }
    for ($y = 1400; $y -ge 200; $y -= 80) { [P]::SetCursorPos(3, $y) | Out-Null; Start-Sleep -Milliseconds 10 }
    $r = Get-Rect $h
    if ($r.T -lt $minT) { $minT = $r.T }
    $off = ($r.T -lt -100) -or ($r.B -gt 1700) -or ($r.L -lt -100) -or ($r.R -gt 2700)
    if ($off) { $hidden++ }
    'round ' + $round + ' T=' + $r.T + ' L=' + $r.L + ' W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T) + ' off=' + $off
}
'MIN-T=' + $minT + ' HIDDEN=' + $hidden
Start-Sleep -Milliseconds 1500
$r = Get-Rect $h
'SETTLED T=' + $r.T + ' L=' + $r.L + ' W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)