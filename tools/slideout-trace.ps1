# Monitor panel Left during slide-out (every 40ms)
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public struct POINT { public int X, Y; }
public static class WM {
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
[WM]::SetProcessDPIAware() | Out-Null
$TITLE = [string]::Concat([char]0x7075, [char]0x52A8, [char]0x9E1F)
$HWND = [WM]::FindMain([uint32]$args[0], $TITLE)
# trigger panel on left edge
[WM]::SetCursorPos(3, 300) | Out-Null
Start-Sleep -Milliseconds 2200
# leave
[WM]::SetCursorPos(1200, 800) | Out-Null
$sb = New-Object System.Text.StringBuilder
for ($i = 0; $i -lt 40; $i++) {
  $r = New-Object RECT
  [WM]::GetWindowRect($HWND, [ref]$r) | Out-Null
  $null = $sb.Append($r.Left).Append(" ")
  Start-Sleep -Milliseconds 40
}
"slideOutLeftTrace: " + $sb.ToString()
