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
[P]::SetCursorPos(3, 800) | Out-Null
Start-Sleep -Milliseconds 4000
$pt = New-Object P+POINT
[P]::GetCursorPos([ref]$pt) | Out-Null
'CURSOR=(' + $pt.X + ',' + $pt.Y + ')'
$targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
$found = [IntPtr]::Zero
$cb = [P+EnumProc]{ param($h, $l) $pid2 = 0; [P]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null; if ($pid2 -eq $targetPid -and [P]::IsWindowVisible($h)) { $sb = New-Object System.Text.StringBuilder 256; [P]::GetWindowText($h, $sb, 256) | Out-Null; $t = $sb.ToString(); $tt = [char]0x7075 + [char]0x52A8 + [char]0x9E1F; if ($t -eq $tt) { $script:found = $h } }; return $true }
[P]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($found -eq [IntPtr]::Zero) { 'WINDOW NOT FOUND' } else {
  $r = New-Object P+RECT
  [P]::GetWindowRect($found, [ref]$r) | Out-Null
  'WINDOW rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ') W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)
}