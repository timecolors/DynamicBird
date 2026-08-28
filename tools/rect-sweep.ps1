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
    if (-not $targetPid) { throw 'DynamicBird not running' }
    $found = [IntPtr]::Zero
    $cb = [Native+EnumWindowsProc]{
        param($h, $l)
        $pid2 = 0
        [Native]::GetWindowThreadProcessId($h, [ref]$pid2) | Out-Null
        if ($pid2 -eq $targetPid -and [Native]::IsWindowVisible($h)) {
            $r = New-Object Native+RECT
            [Native]::GetWindowRect($h, [ref]$r) | Out-Null
            $w = $r.Right - $r.Left; $ht = $r.Bottom - $r.Top
            if ($w -gt 100 -and $ht -gt 100) { $script:found = $h }
        }
        return $true
    }
    [Native]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
    if ($script:found -eq [IntPtr]::Zero) { throw 'panel window not found' }
    return $script:found
}

function Get-Rect($h) {
    $r = New-Object Native+RECT
    [Native]::GetWindowRect($h, [ref]$r) | Out-Null
    return $r
}

$out = 'tools/flick/sweep-top.csv'
$rows = [System.Collections.Generic.List[string]]::new()
$rows.Add('ms,round,dir,mouseX,L,T,R,B,W,H,cx,cy')

# 召出面板
[Native]::SetCursorPos(854, 3) | Out-Null
Start-Sleep -Milliseconds 1000
$h = Get-PanelHwnd
$r0 = Get-Rect $h
$baseline = '{0},{1},{2},{3},{4},{5},{6},{7},{8},{9}' -f 0,0,'base',854,$r0.Left,$r0.Top,$r0.Right,$r0.Bottom,($r0.Right-$r0.Left),($r0.Bottom-$r0.Top)
$rows.Add($baseline)
"BASELINE $baseline"

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$xFrom = 171; $xTo = 1536; $steps = 27; $stepPx = ($xTo - $xFrom) / $steps
for ($round = 1; $round -le 3; $round++) {
    # 去程 171 -> 1536
    for ($i = 0; $i -le $steps; $i++) {
        $x = [int]($xFrom + $i * $stepPx)
        [Native]::SetCursorPos($x, 3) | Out-Null
        Start-Sleep -Milliseconds 12
        $r = Get-Rect $h
        $rows.Add(('{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}' -f $sw.ElapsedMilliseconds,$round,'F',$x,$r.Left,$r.Top,$r.Right,$r.Bottom,($r.Right-$r.Left),($r.Bottom-$r.Top),(($r.Left+$r.Right)/2),(($r.Top+$r.Bottom)/2)))
    }
    # 回程 1536 -> 171
    for ($i = 0; $i -le $steps; $i++) {
        $x = [int]($xTo - $i * $stepPx)
        [Native]::SetCursorPos($x, 3) | Out-Null
        Start-Sleep -Milliseconds 12
        $r = Get-Rect $h
        $rows.Add(('{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}' -f $sw.ElapsedMilliseconds,$round,'B',$x,$r.Left,$r.Top,$r.Right,$r.Bottom,($r.Right-$r.Left),($r.Bottom-$r.Top),(($r.Left+$r.Right)/2),(($r.Top+$r.Bottom)/2)))
    }
}
[System.IO.File]::WriteAllLines($out, $rows)
"DONE rows=$($rows.Count) -> $out"
