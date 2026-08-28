# Rapid top-sweep then stop at Top_Right: panel right edge must not exceed screen
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public struct POINT { public int X, Y; }
public static class W7 {
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
[W7]::SetProcessDPIAware() | Out-Null
$TITLE = [string]::Concat([char]0x7075, [char]0x52A8, [char]0x9E1F)
$HWND = [W7]::FindMain([uint32]$args[0], $TITLE)
Write-Output ("hwnd=" + $HWND)
function Get-RectInfo {
  $r = New-Object RECT
  [W7]::GetWindowRect($HWND, [ref]$r) | Out-Null
  $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
  return ("L=$($r.Left) R=$($r.Right) W=$w H=$h rightEdgeInScreen=$($r.Right -le 2560)")
}
# rapid sweep on top edge: Left(400) -> Center(1300) -> Right(2300) -> Center -> Right, ~350ms apart (icon mode)
[W7]::SetCursorPos(400, 3) | Out-Null; Start-Sleep -Milliseconds 350
[W7]::SetCursorPos(1300, 3) | Out-Null; Start-Sleep -Milliseconds 350
[W7]::SetCursorPos(2300, 3) | Out-Null; Start-Sleep -Milliseconds 350
[W7]::SetCursorPos(1300, 3) | Out-Null; Start-Sleep -Milliseconds 350
[W7]::SetCursorPos(2300, 3) | Out-Null
Write-Output ("sweep done, staying at Top_Right")
Start-Sleep -Milliseconds 3000
Write-Output ("after-stop: " + (Get-RectInfo))
Start-Sleep -Milliseconds 2000
Write-Output ("after-2s: " + (Get-RectInfo))
# leave
[W7]::SetCursorPos(1200, 800) | Out-Null
Start-Sleep -Milliseconds 1500
Write-Output ("after-leave: " + (Get-RectInfo))
