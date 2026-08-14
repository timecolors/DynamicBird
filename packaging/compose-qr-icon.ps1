param(
    [string]$QrSource = (Join-Path $PSScriptRoot "Assets\qr-source.png"),
    [string]$OutFile = (Join-Path $PSScriptRoot "Assets\qr-icon-source.png")
)

# 把二维码合成应用图标风格：白底圆角卡片 + 主题蓝描边，二维码居中。
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$size = 1024
$canvas = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# 圆角矩形路径（先画白色底，再描蓝色边框）
$radius = 224.0
$d = $radius * 2
$bg = New-Object System.Drawing.Drawing2D.GraphicsPath
$bg.AddArc([float]0, [float]0, [float]$d, [float]$d, [float]180, [float]90)
$bg.AddArc([float]($size - $d), [float]0, [float]$d, [float]$d, [float]270, [float]90)
$bg.AddArc([float]($size - $d), [float]($size - $d), [float]$d, [float]$d, [float]0, [float]90)
$bg.AddArc([float]0, [float]($size - $d), [float]$d, [float]$d, [float]90, [float]90)
$bg.CloseFigure()

$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$g.FillPath($white, $bg)

$borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0, 120, 212), 52.0)
$borderPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawPath($borderPen, $bg)

# 二维码居中绘制（保持锐利：最近邻缩放）
$qr = New-Object System.Drawing.Bitmap($QrSource)
$qrSize = 700
$qrX = [int](($size - $qrSize) / 2)
$qrY = $qrX
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($qr, $qrX, $qrY, $qrSize, $qrSize)

$canvas.Save($OutFile, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "已生成: $OutFile"

$qr.Dispose(); $borderPen.Dispose(); $white.Dispose(); $bg.Dispose(); $g.Dispose(); $canvas.Dispose()
