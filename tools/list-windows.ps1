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
# 列所有可见窗口
$targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
$list = [System.Collections.Generic.List[string]]::new()
$cb = [P+EnumProc]{ param($h, $l) $pid2 = 0; [P]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null; if ($pid2 -eq $targetPid -and [P]::IsWindowVisible($h)) { $sb = New-Object System.Text.StringBuilder 256; [P]::GetWindowText($h, $sb, 256) | Out-Null; $r = New-Object P+RECT; [P]::GetWindowRect($h, [ref]$r) | Out-Null; $list.Add('hwnd=' + $h + ' title=' + $sb.ToString() + ' rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ')') }; return $true }
[P]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
if ($list.Count -eq 0) { 'NO VISIBLE WINDOWS' } else { $list -join "`n" }