# Strict boundary hide test
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public struct POINT { public int X, Y; }
public static class W5 {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
  public static IntPtr FindMain(uint targetPid, string title) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == targetPid && found == IntPtr.Zero) {
        StringBuilder t = new StringBuilder(256);
        GetWindowText(h, t, 256);
        if (t.ToString() == title) { found = h; return false; }
      }
      return true;
    }, IntPtr.Zero);
    return found;
  }
}
"@
[W5]::SetProcessDPIAware() | Out-Null
$TITLE = [string]::Concat([char]0x7075, [char]0x52A8, [char]0x9E1F)
$HWND = [W5]::FindMain([uint32]$args[0], $TITLE)
Write-Output ("hwnd=" + $HWND)
function Get-BirdRect {
  $r = New-Object RECT
  [W5]::GetWindowRect($HWND, [ref]$r) | Out-Null
  return ("{0},{1} {2}x{3}" -f $r.Left, $r.Top, ($r.Right-$r.Left), ($r.Bottom-$r.Top))
}
function Move-Sample($x, $y, $label, $ms) {
  [W5]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds $ms
  Write-Output ("{0}: rect={1}" -f $label, (Get-BirdRect))
}
# trigger panel
Move-Sample 3 300 "trigger" 2500
# panel rect shows (0,0) 1024x680 -> bottom edge at 680 phys. Stay 8px below (688)
Move-Sample 500 688 "below-8px" 800
# wait longer - must be hidden by now
Start-Sleep -Milliseconds 1200
Write-Output ("after-below: rect=" + (Get-BirdRect))
# re-trigger, then stay 8px right of panel right edge (1032)
Move-Sample 3 300 "retrigger" 2500
Move-Sample 1032 400 "right-8px" 800
Start-Sleep -Milliseconds 1200
Write-Output ("after-right: rect=" + (Get-BirdRect))
