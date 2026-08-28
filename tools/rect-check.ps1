Add-Type @"
using System;
using System.Runtime.InteropServices;
public struct RECT2 { public int Left, Top, Right, Bottom; }
public static class WR {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT2 rect);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT2 p);
}
public struct POINT2 { public int X, Y; }
"@
# move to left edge top (widget region)
[WR]::SetCursorPos(3, 300) | Out-Null
Start-Sleep -Milliseconds 2000
$h = [IntPtr]10027554
$r = New-Object RECT2
[WR]::GetWindowRect($h, [ref]$r) | Out-Null
"physRect=($($r.Left),$($r.Top)) $($r.Right-$r.Left)x$($r.Bottom-$r.Top)"
$p = New-Object POINT2
[WR]::GetCursorPos([ref]$p) | Out-Null
"cursorPhys=($($p.X),$($p.Y))"
