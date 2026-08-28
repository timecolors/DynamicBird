Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class DpiAware {
  [DllImport("shcore.dll")] public static extern int GetProcessDpiAwareness(IntPtr hProcess, out int value);
  [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
  [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetDpiForSystem();
}
"@
$h = [DpiAware]::OpenProcess(0x1000, $false, [uint32]$args[0])
$v = -1
$rc = [DpiAware]::GetProcessDpiAwareness($h, [ref]$v)
[DpiAware]::CloseHandle($h) | Out-Null
"rc=$rc value=$v (0=unaware 1=systemAware 2=perMonitor)"
"toolProcessSystemDpi=" + [DpiAware]::GetDpiForSystem()
