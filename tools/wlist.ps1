Add-Type @"
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class WList {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
  public static string ListFor(uint targetPid) {
    StringBuilder sb = new StringBuilder();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == targetPid) {
        StringBuilder t = new StringBuilder(512);
        GetWindowText(h, t, 512);
        sb.Append("hwnd=").Append(h).Append(" vis=").Append(IsWindowVisible(h))
          .Append(" title=[").Append(t).Append("]\n");
      }
      return true;
    }, IntPtr.Zero);
    return sb.ToString();
  }
}
"@
[WList]::ListFor([uint32]$args[0])
