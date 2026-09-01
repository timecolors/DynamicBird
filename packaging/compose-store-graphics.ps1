
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$name = "ShoreHue " + [char]0x6D77 + [char]0x5CB8 + [char]0x7EBF
$sub  = "Windows " + [char]0x684C + [char]0x9762 + [char]0x8FB9 + [char]0x7F18 + [char]0x9762 + [char]0x677F + [char]0x5DE5 + [char]0x5177
$tag  = [char]0x8FB9 + [char]0x7F18 + [char]0x5373 + [char]0x6D77 + [char]0x5CB8 + " - " + [char]0x684C + [char]0x9762 + [char]0x5916 + [char]0x9AA8 + [char]0x9ABC + " - AI " + [char]0x53EF + [char]0x7F16 + [char]0x7A0B

$root = Split-Path -Parent $PSScriptRoot
$iconPath = Join-Path $root "packaging\Assets\IconMaster.png"
$outDir  = Join-Path $root "packaging\StoreScreenshots"
$icon = New-Object System.Drawing.Bitmap($iconPath)

# StoreBox 2160x2160
$size = 2160
$box = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($box)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$rect = New-Object System.Drawing.RectangleF([float]0, [float]0, [float]$size, [float]$size)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 30, 141, 224), [System.Drawing.Color]::FromArgb(255, 0, 103, 192), 90.0)
$g.FillRectangle($brush, $rect)
$iconSize = [int]($size * 0.42)
$ix = [int](($size - $iconSize) / 2)
$iy = [int]($size * 0.14)
$g.DrawImage($icon, $ix, $iy, $iconSize, $iconSize)
$font = New-Object System.Drawing.Font("Microsoft YaHei UI", [float]92, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$textRect = New-Object System.Drawing.RectangleF([float]0, [float]($size * 0.60), [float]$size, [float]160)
$g.DrawString($name, $font, $textBrush, $textRect, $sf)
$subFont = New-Object System.Drawing.Font("Microsoft YaHei UI", [float]40, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$subBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 255, 255, 255))
$subRect = New-Object System.Drawing.RectangleF([float]0, [float]($size * 0.60 + 180), [float]$size, [float]80)
$g.DrawString($sub, $subFont, $subBrush, $subRect, $sf)
$box.Save((Join-Path $outDir "StoreBox2160x2160.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $box.Dispose()
Write-Output "saved StoreBox2160x2160.png"

# StorePoster 1440x2160
$w = 1440; $h = 2160
$poster = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($poster)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$rect = New-Object System.Drawing.RectangleF([float]0, [float]0, [float]$w, [float]$h)
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, [System.Drawing.Color]::FromArgb(255, 30, 141, 224), [System.Drawing.Color]::FromArgb(255, 0, 103, 192), 90.0)
$g.FillRectangle($brush, $rect)
$iconSize = [int]($w * 0.30)
$ix = [int](($w - $iconSize) / 2)
$iy = [int]($h * 0.06)
$g.DrawImage($icon, $ix, $iy, $iconSize, $iconSize)
$font = New-Object System.Drawing.Font("Microsoft YaHei UI", [float]64, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$nameRect = New-Object System.Drawing.RectangleF([float]0, [float]($h * 0.20), [float]$w, [float]100)
$g.DrawString($name, $font, $textBrush, $nameRect, $sf)
$shot = New-Object System.Drawing.Bitmap((Join-Path $outDir "01-Taskbar.png"))
$shotW = [int]($w * 0.86)
$shotH = [int]($shotW * $shot.Height / $shot.Width)
$sx = [int](($w - $shotW) / 2)
$sy = [int]($h * 0.32)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($shot, $sx, $sy, $shotW, $shotH)
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160, 255, 255, 255), [float]6)
$g.DrawRectangle($pen, $sx, $sy, $shotW, $shotH)
$tagFont = New-Object System.Drawing.Font("Microsoft YaHei UI", [float]36, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$tagRect = New-Object System.Drawing.RectangleF([float]0, [float]($h * 0.90), [float]$w, [float]80)
$g.DrawString($tag, $tagFont, $textBrush, $tagRect, $sf)
$poster.Save((Join-Path $outDir "StorePoster1440x2160.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $poster.Dispose()
Write-Output "saved StorePoster1440x2160.png"
