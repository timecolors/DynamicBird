param(
    [string]$QrSource = (Join-Path $PSScriptRoot "Assets\qr-source.png"),
    [string]$OutFile = (Join-Path $PSScriptRoot "Assets\qr-icon-source.png")
)

# 把二维码合成应用图标：45° 渐变（海蓝→沙滩）背景 + 透明二维码（白模块透出背景）
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

# 1) 加载二维码源，把白色模块变透明
$src = New-Object System.Drawing.Bitmap($QrSource)
$w = $src.Width; $h = $src.Height
$qr = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $h; $y++) {
  for ($x = 0; $x -lt $w; $x++) {
    $c = $src.GetPixel($x, $y)
    if ($c.R -gt 235 -and $c.G -gt 235 -and $c.B -gt 235) { $qr.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 255, 255, 255)) }
    else { $qr.SetPixel($x, $y, $c) }
  }
}
$src.Dispose()

# 2) 画布 1024x1024：45° 渐变背景
$size = 1024
$canvas = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 30, 141, 224),   # 海水蓝 #1E8DE0
    [System.Drawing.Color]::FromArgb(255, 244, 214, 168),  # 沙滩 #F4D6A8
    -45.0)
$g.FillRectangle($grad, $rect)

# 3) 透明二维码缩小居中
$qrSize = 680
$qrX = [int](($size - $qrSize) / 2)
$qrY = $qrX
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($qr, $qrX, $qrY, $qrSize, $qrSize)

$canvas.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "已生成: $OutFile"

$qr.Dispose(); $grad.Dispose(); $g.Dispose(); $canvas.Dispose()
