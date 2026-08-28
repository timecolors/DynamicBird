# Widget size determinism test v3 - DPI-aware cursor + enum-based hwnd
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public struct POINT { public int X, Y; }
public static class W3 {
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
[W3]::SetProcessDPIAware() | Out-Null
$TITLE = [string]::Concat([char]0x7075, [char]0x52A8, [char]0x9E1F)
$HWND = [W3]::FindMain([uint32]$args[0], $TITLE)
Write-Output ("hwnd=" + $HWND)

function Get-BirdRect {
  if ($HWND -eq [IntPtr]::Zero) { return "NO-WINDOW" }
  $r = New-Object RECT
  [W3]::GetWindowRect($HWND, [ref]$r) | Out-Null
  return ("{0},{1} {2}x{3}" -f $r.Left, $r.Top, ($r.Right-$r.Left), ($r.Bottom-$r.Top))
}

function Move-And-Sample($x, $y, $label, $expect) {
  [W3]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds 2000
  $rect = Get-BirdRect
  $p = New-Object POINT
  [W3]::GetCursorPos([ref]$p) | Out-Null
  Write-Output ("{0}: expect={1} cursor=({2},{3}) rect={4}" -f $label, $expect, $p.X, $p.Y, $rect)
}

Move-And-Sample 3 300  "W1-LeftTop-first"    "Widget"
Move-And-Sample 3 700  "AI-LeftCenter"       "AI"
Move-And-Sample 3 300  "W2-LeftTop-again"    "Widget"
Move-And-Sample 3 1200 "W3-LeftBottom"       "Widget"
Move-And-Sample 3 300  "W4-LeftTop-recheck"  "Widget"
Move-And-Sample 1000 800 "CENTER-leave"      "hidden"
Move-And-Sample 3 300  "W5-LeftTop-afterhide" "Widget"
