# Hide-delay fix test: leave edge -> panel must hide within ~200ms
Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct RECT { public int Left, Top, Right, Bottom; }
public struct POINT { public int X, Y; }
public static class W4 {
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
[W4]::SetProcessDPIAware() | Out-Null
$TITLE = [string]::Concat([char]0x7075, [char]0x52A8, [char]0x9E1F)
$HWND = [W4]::FindMain([uint32]$args[0], $TITLE)
Write-Output ("hwnd=" + $HWND)

function Get-BirdRect {
  if ($HWND -eq [IntPtr]::Zero) { return "NO-WINDOW" }
  $r = New-Object RECT
  [W4]::GetWindowRect($HWND, [ref]$r) | Out-Null
  return ("{0},{1} {2}x{3}" -f $r.Left, $r.Top, ($r.Right-$r.Left), ($r.Bottom-$r.Top))
}

function Move-Sample($x, $y, $label, $ms) {
  [W4]::SetCursorPos($x, $y) | Out-Null
  Start-Sleep -Milliseconds $ms
  Write-Output ("{0}: rect={1}" -f $label, (Get-BirdRect))
}

# trigger widget panel
Move-Sample 3 300  "W1-trigger"        2500
# leave edge, wait 1.5s > hide delay -> should be hidden (offscreen)
Move-Sample 1000 800 "LEAVE-wait1500"  1500
# re-trigger
Move-Sample 3 300  "W2-retrigger"      2500
# leave again, sample quickly at 300ms (hide delay is 200ms -> should already be hidden)
Move-Sample 1000 800 "LEAVE-300ms"     300
# re-trigger one more time to confirm
Move-Sample 3 300  "W3-retrigger"      2500
# leave and confirm hidden
Move-Sample 1000 800 "LEAVE-final"     1200
