Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct PP { public int X, Y; }
public static class CC {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out PP p);
}
"@
[CC]::SetProcessDPIAware() | Out-Null
[CC]::SetCursorPos(300, 1595) | Out-Null
Start-Sleep -Milliseconds 200
$p = New-Object PP
[CC]::GetCursorPos([ref]$p) | Out-Null
"cursor=($($p.X),$($p.Y))"
Start-Sleep -Milliseconds 2500
[CC]::SetCursorPos(1200, 800) | Out-Null
Start-Sleep -Milliseconds 800
