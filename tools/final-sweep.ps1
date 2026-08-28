$ErrorActionPreference = 'Stop'
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
    $r = New-Object Native+RECT
    [Native]::GetWindowRect($h, [ref]$r) | Out-Null
    return $r
}
# 召出顶边中间（AppHelper 区域，x=1280 物理 = 853 DIP），等待稳定
[Native]::SetCursorPos(1280, 3) | Out-Null
Start-Sleep -Milliseconds 1500
$h = Get-PanelHwnd
$r = Get-Rect $h
"SUMMON-MID rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom)) W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top)"
# 平滑移到顶边左侧（Taskbar 横条区域，x=500 物理 = 333 DIP）
for ($x = 1280; $x -ge 500; $x -= 60) { [Native]::SetCursorPos($x, 3) | Out-Null; Start-Sleep -Milliseconds 15 }
Start-Sleep -Milliseconds 800
$r = Get-Rect $h
"AFTER-LEFT rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom)) W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top)"
# 再快速来回扫 3 个来回（Taskbar <-> AppHelper）
$samples = [System.Collections.Generic.List[string]]::new()
for ($round = 1; $round -le 3; $round++) {
    for ($x = 500; $x -le 1280; $x += 70) { [Native]::SetCursorPos($x, 3) | Out-Null; Start-Sleep -Milliseconds 10 }
    for ($x = 1280; $x -ge 500; $x -= 70) { [Native]::SetCursorPos($x, 3) | Out-Null; Start-Sleep -Milliseconds 10 }
    $r = Get-Rect $h
    $samples.Add("round $round T=$($r.Top) L=$($r.Left) W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top)")
}
'SWEEP SAMPLES'
$samples -join "`n"
# 停稳 1.5s（最终加载）
Start-Sleep -Milliseconds 1500
$r = Get-Rect $h
"SETTLED rect=($($r.Left),$($r.Top))-($($r.Right),$($r.Bottom)) W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top)"