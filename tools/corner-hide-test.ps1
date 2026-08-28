# Verify corner hide -> fully offscreen
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public struct POINT { public int X, Y; }
public static class WA {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
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
[WA]::SetProcessDPIAware() | Out-Null
$TITLE = [string]::Concat([char]0x7075, [char]0x52A8, [char]0x9E1F)
$HWND = [WA]::FindMain([uint32]$args[0], $TITLE)
function Get-Rect {
  $r = New-Object RECT
  [WA]::GetWindowRect($HWND, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
  $out = ($r.Right -le 0 -or $r.Bottom -le 0 -or $r.Left -ge 2560 -or $r.Top -ge 1600)
  return ("(" + $r.Left + "," + $r.Top + ") " + $w + "x" + $h + " fullyOut=" + $out)
}
# trigger bottom-left corner (3,1585 phys)
[WA]::SetCursorPos(3, 1585) | Out-Null
Start-Sleep -Milliseconds 2500
"corner-shown: " + (Get-Rect)
# leave far away
[WA]::SetCursorPos(1200, 800) | Out-Null
Start-Sleep -Milliseconds 1200
"corner-after-leave: " + (Get-Rect)
