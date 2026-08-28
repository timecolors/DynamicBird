$ErrorActionPreference = 'Continue'
$src = @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class Native {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
Add-Type -TypeDefinition $src
[Native]::SetProcessDPIAware() | Out-Null
function Get-PanelHwnd {
    $targetPid = (Get-Process DynamicBird -ErrorAction SilentlyContinue).Id
    $found = [IntPtr]::Zero
    $cb = [Native+EnumWindowsProc]{
        param($h, $l)
        $pid2 = 0
        [Native]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $targetPid -and [Native]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256
            [Native]::GetWindowText($h, $sb, 256) | Out-Null
            $title = $sb.ToString()
            if ($title -eq ([char]0x7075 + [char]0x52A8 + [char]0x9E1F)) { $script:found = $h }
        }
        return $true
    }
    [Native]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    return $script:found
}
function Get-Rect($h) {
    try {
        $r = New-Object Native+RECT
        [Native]::GetWindowRect($h, [ref]$r) | Out-Null
        return $r
    } catch { return $null }
}
# 先移开鼠标让面板隐藏
[Native]::SetCursorPos(1280, 800) | Out-Null
Start-Sleep -Milliseconds 1500
# 召出顶边中间
[Native]::SetCursorPos(1280, 3) | Out-Null
Start-Sleep -Milliseconds 2000
$h = Get-PanelHwnd
$r = Get-Rect $h
if ($r) { "SUMMON-1 rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom)) W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top)" } else { 'NULL' }
# 平滑移到顶边左侧
for ($x = 1280; $x -ge 500; $x -= 60) { [Native]::SetCursorPos($x, 3) | Out-Null; Start-Sleep -Milliseconds 15 }
Start-Sleep -Milliseconds 800
$r = Get-Rect $h
if ($r) { "AFTER-LEFT rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom)) W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top)" } else { 'NULL' }