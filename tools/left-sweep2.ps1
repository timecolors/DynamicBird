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
# 0) 先移开鼠标，让面板处于隐藏态
[Native]::SetCursorPos(1280, 800) | Out-Null
Start-Sleep -Milliseconds 1500
# 1) 召出左边缘中间：停留 2500ms（> TriggerDelayMs 150ms 触发延时）
[Native]::SetCursorPos(3, 800) | Out-Null
Start-Sleep -Milliseconds 2500
$h = Get-PanelHwnd
$r = Get-Rect $h
if ($r) { "SUMMON W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top) T=$($r.Top) L=$($r.Left)" } else { 'NO RECT' }
# 2) 快速上下扫 4 轮（面板已显示，切换无触发延时）
$hidden = 0
for ($round = 1; $round -le 4; $round++) {
    for ($y = 200; $y -le 1400; $y += 80) { [Native]::SetCursorPos(3, $y) | Out-Null; Start-Sleep -Milliseconds 10 }
    for ($y = 1400; $y -ge 200; $y -= 80) { [Native]::SetCursorPos(3, $y) | Out-Null; Start-Sleep -Milliseconds 10 }
    $r = Get-Rect $h
    if ($r) {
        $off = ($r.Top -lt -100) -or ($r.Bottom -gt 1700) -or ($r.Left -lt -100) -or ($r.Right -gt 2700)
        if ($off) { $hidden++ }
        "round $round T=$($r.Top) L=$($r.Left) W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top) off=$off"
    } else { "round $round RECT NULL" }
}
"HIDDEN=$hidden"
Start-Sleep -Milliseconds 1500
$r = Get-Rect $h
if ($r) { "SETTLED W=$($r.Right-$r.Left) H=$($r.Bottom-$r.Top) T=$($r.Top) L=$($r.Left)" }