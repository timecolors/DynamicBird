$ErrorActionPreference = 'Continue'
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class P {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
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
'CURSOR=(' + $pt.X + ',' + $pt.Y + ')'
$targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
'PID=' + $targetPid
$cb = [P+EnumProc]{ param($h, $l) $pid2 = 0; [P]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null; if ($pid2 -eq $targetPid) { $sb2 = New-Object System.Text.StringBuilder 256; [P]::GetWindowText($h, $sb2, 256) | Out-Null; $r = New-Object P+RECT; [P]::GetWindowRect($h, [ref]$r) | Out-Null; $vis = [P]::IsWindowVisible($h); $list.Add('h=' + $h + ' vis=' + $vis + ' t=' + $sb2.ToString() + ' rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ')') }; return $true }
$list = [System.Collections.Generic.List[string]]::new()
[P]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($list.Count -eq 0) { 'NO WINDOWS' } else { $list -join "`n" }