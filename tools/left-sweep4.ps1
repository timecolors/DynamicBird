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
# 1) 移开鼠标，面板隐藏
[P]::SetCursorPos(1280, 800) | Out-Null
Start-Sleep -Milliseconds 1500
$h = Get-Hwnd
if ($h -eq [IntPtr]::Zero) { 'WINDOW NOT FOUND'; exit 1 }
# 2) 呼出左边缘中间，停留 2.5s 过触发延时
[P]::SetCursorPos(3, 800) | Out-Null
Start-Sleep -Milliseconds 2500
$r = Get-Rect $h
'SUMMON rect=(' + $r.L + ',' + $r.T + ')-(' + $r.R + ',' + $r.B + ') W=' + ($r.R-$r.L) + ' H=' + ($r.B-$r.T)
# 3) 快速滑动 4 轮，每步记录窗口矩形
$out = [System.Collections.Generic.List[string]]::new()
$sw = [System.Diagnostics.Stopwatch]::StartNew()
for ($round = 1; $round -le 4; $round++) {
    for ($y = 200; $y -le 1400; $y += 80) { [P]::SetCursorPos(3, $y) | Out-Null; $r = Get-Rect $h; $out.Add('' + $sw.ElapsedMilliseconds + ',' + $r.L + ',' + $r.T + ',' + $r.R + ',' + $r.B); Start-Sleep -Milliseconds 10 }
    for ($y = 1400; $y -ge 200; $y -= 80) { [P]::SetCursorPos(3, $y) | Out-Null; $r = Get-Rect $h; $out.Add('' + $sw.ElapsedMilliseconds + ',' + $r.L + ',' + $r.T + ',' + $r.R + ',' + $r.B); Start-Sleep -Milliseconds 10 }
}
[System.IO.File]::WriteAllLines('tools/flick/left-sweep-raw.csv', $out)
'SWEEP DONE samples=' + $out.Count