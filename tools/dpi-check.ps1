Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class DpiInfo {
  public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder sb, int max);
  public static string Check(uint targetPid) {
    System.Text.StringBuilder sb = new System.Text.StringBuilder();
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid == targetPid) {
        System.Text.StringBuilder t = new System.Text.StringBuilder(256);
        GetWindowText(h, t, 256);
        sb.Append("hwnd=").Append(h).Append(" dpi=").Append(GetDpiForWindow(h))
          .Append(" title=[").Append(t).Append("]\n");
      }
      return true;
    }, IntPtr.Zero);
    return sb.ToString() + "systemDpi=" + GetDpiForSystem();
  }
}
"@
[DpiInfo]::Check([uint32]$args[0])
