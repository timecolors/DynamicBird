param(
    [string]$OutDir = (Join-Path $PSScriptRoot "Assets"),
    [string]$IcoPath = (Join-Path $PSScriptRoot "..\assets\icon.ico"),
    [string]$SourceImage = ""
)

# 灵动鸟应用图标生成脚本
# 依据 AppIcons.xaml 中 IconLogo 几何（Feather "layout"）矢量重绘：
#   M3,3 L21,3 L21,21 L3,21 Z  窗口外框
#   M3,9 L21,9                 标题栏分栏
#   M9,9 L9,21                 左侧边栏
# 主色 #0078D4（应用 AccentColor），白色描边，Win11 风格圆角方形底。

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$srcBmp = $null
if ($SourceImage -and (Test-Path $SourceImage)) {
    $srcBmp = New-Object System.Drawing.Bitmap((Resolve-Path $SourceImage).Path)
    Write-Output "使用源图: $SourceImage"
}

# 把源图按“contain”等比缩放居中绘制到画布（四周留白）
function Draw-SourceContained([System.Drawing.Graphics]$g, [double]$canvasW, [double]$canvasH, [double]$iconSize, [double]$padRatio) {
    if (-not $srcBmp) { return }
    $pad = $iconSize * $padRatio
    $box = $iconSize - 2 * $pad
    $ratio = [Math]::Min($box / $srcBmp.Width, $box / $srcBmp.Height)
    $w = [Math]::Max(1, [Math]::Round($srcBmp.Width * $ratio))
    $h = [Math]::Max(1, [Math]::Round($srcBmp.Height * $ratio))
    $x = [Math]::Round(($canvasW - $w) / 2)
    $y = [Math]::Round(($canvasH - $h) / 2)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($srcBmp, [int]$x, [int]$y, [int]$w, [int]$h)
}

function New-LayoutPath([double]$size) {
    # 画布留 6% 边距，glyph 占 88% 面积
    $pad = $size * 0.06
    $scale = ($size - 2 * $pad) / 24.0
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    function Add-Line($x1, $y1, $x2, $y2) {
        $path.AddLine(
            [float]($pad + $x1 * $scale), [float]($pad + $y1 * $scale),
            [float]($pad + $x2 * $scale), [float]($pad + $y2 * $scale))
    }
    # 外框（闭合矩形）
    $path.AddLines(@(
        [System.Drawing.PointF]::new([float]($pad + 3 * $scale), [float]($pad + 3 * $scale)),
        [System.Drawing.PointF]::new([float]($pad + 21 * $scale), [float]($pad + 3 * $scale)),
        [System.Drawing.PointF]::new([float]($pad + 21 * $scale), [float]($pad + 21 * $scale)),
        [System.Drawing.PointF]::new([float]($pad + 3 * $scale), [float]($pad + 21 * $scale))
    ))
    $path.CloseFigure()
    Add-Line 3 9 21 9
    Add-Line 9 9 9 21
    return $path
}

function New-IconBitmap([int]$size, [bool]$tile = $false) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 用户源图优先：直接等比缩放绘制，不做矢量重绘
    if ($srcBmp) {
        # JPEG 等无透明通道的满幅图片几乎不留边距；透明 PNG 自动留 4%
        $srcPad = if ($srcBmp.PixelFormat -like "*Alpha*") { 0.04 } else { 0.02 }
        Draw-SourceContained $g $size $size $size $srcPad
        $g.Dispose()
        return $bmp
    }

    # 圆角方形底色（顶部略亮，Win11 质感）
    $radius = $size * 0.22
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $bg.AddArc([float]0, [float]0, [float]$d, [float]$d, [float]180, [float]90)
    $bg.AddArc([float]($size - $d), [float]0, [float]$d, [float]$d, [float]270, [float]90)
    $bg.AddArc([float]($size - $d), [float]($size - $d), [float]$d, [float]$d, [float]0, [float]90)
    $bg.AddArc([float]0, [float]($size - $d), [float]$d, [float]$d, [float]90, [float]90)
    $bg.CloseFigure()

    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 30, 141, 224),   # #1E8DE0
        [System.Drawing.Color]::FromArgb(255, 0, 103, 192),    # #0067C0
        90.0)
    $g.FillPath($brush, $bg)

    # 白色 layout 描边
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [float]($size * 0.075))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $glyph = New-LayoutPath ($size * 0.82)   # glyph 区域略小于底色
    $g.DrawPath($pen, $glyph)

    $pen.Dispose(); $glyph.Dispose(); $brush.Dispose(); $bg.Dispose(); $g.Dispose()
    return $bmp
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

# ---- 1) ico：多尺寸 PNG 条目（修正旧 ico 的 tRNS+alpha 畸形问题） ----
$icoSizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBlobs = @()
$entries = New-Object System.Collections.Generic.List[byte[]]

foreach ($s in $icoSizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $blob = $ms.ToArray()
    $ms.Dispose(); $bmp.Dispose()
    $pngBlobs += , $blob
}

$count = $icoSizes.Count
$header = New-Object System.Collections.Generic.List[byte]
$header.AddRange([System.Text.Encoding]::ASCII.GetBytes([char]0 + [char]0 + [char]1 + [char]0))
$header.AddRange([System.BitConverter]::GetBytes([uint16]$count))

$offset = 6 + 16 * $count
$dirEntries = New-Object System.Collections.Generic.List[byte]
for ($i = 0; $i -lt $count; $i++) {
    $s = $icoSizes[$i]
    $blob = $pngBlobs[$i]
    $b = New-Object System.Collections.Generic.List[byte]
    $b.Add([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $b.Add([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $b.Add(0)                 # 调色板
    $b.Add(0)                 # 保留
    $b.AddRange([System.BitConverter]::GetBytes([uint16]1))    # planes
    $b.AddRange([System.BitConverter]::GetBytes([uint16]32))   # bitcount
    $b.AddRange([System.BitConverter]::GetBytes([uint32]$blob.Length))
    $b.AddRange([System.BitConverter]::GetBytes([uint32]$offset))
    $dirEntries.AddRange($b)
    $offset += $blob.Length
}

$fs = [System.IO.File]::Create($IcoPath)
$fs.Write($header.ToArray(), 0, $header.Count)
$fs.Write($dirEntries.ToArray(), 0, $dirEntries.Count)
foreach ($blob in $pngBlobs) { $fs.Write($blob, 0, $blob.Length) }
$fs.Close()
Write-Output "ICO: $IcoPath ($($icoSizes.Count) sizes)"

# ---- 2) 商店 MSIX 资产 ----
$squareSizes = @{
    "Square44x44Logo.png"   = 44
    "Square71x71Logo.png"   = 71
    "Square150x150Logo.png" = 150
    "Square310x310Logo.png" = 310
    "StoreLogo.png"         = 50
}
foreach ($kv in $squareSizes.GetEnumerator()) {
    $bmp = New-IconBitmap $kv.Value
    $bmp.Save((Join-Path $OutDir $kv.Key), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# 宽幅磁贴 310x150：左侧图标 + 右侧留白
$wide = New-Object System.Drawing.Bitmap(310, 150, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gw = [System.Drawing.Graphics]::FromImage($wide)
$gw.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gw.Clear([System.Drawing.Color]::Transparent)
$wrect = New-Object System.Drawing.RectangleF(0, 0, 310, 150)
$wbg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $wrect,
    [System.Drawing.Color]::FromArgb(255, 30, 141, 224),
    [System.Drawing.Color]::FromArgb(255, 0, 103, 192),
    90.0)
$wpath = New-Object System.Drawing.Drawing2D.GraphicsPath
$wr = 30.0
$wd = $wr * 2
$wpath.AddArc([float]0, [float]0, [float]$wd, [float]$wd, [float]180, [float]90)
$wpath.AddArc([float](310 - $wd), [float]0, [float]$wd, [float]$wd, [float]270, [float]90)
$wpath.AddArc([float](310 - $wd), [float](150 - $wd), [float]$wd, [float]$wd, [float]0, [float]90)
$wpath.AddArc([float]0, [float](150 - $wd), [float]$wd, [float]$wd, [float]90, [float]90)
$wpath.CloseFigure()
$gw.FillPath($wbg, $wpath)
if ($srcBmp) {
    Draw-SourceContained $gw 310 150 310 0.0
} else {
    $mini = New-IconBitmap 110
    $gw.DrawImage($mini, 20, 20, 110, 110)
    $mini.Dispose()
}
$wide.Save((Join-Path $OutDir "Wide310x150Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$wpath.Dispose(); $wbg.Dispose(); $gw.Dispose(); $wide.Dispose()

# 启动屏 620x300：底色 + 居中图标
$splash = New-Object System.Drawing.Bitmap(620, 300, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gs = [System.Drawing.Graphics]::FromImage($splash)
$gs.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$gs.Clear([System.Drawing.Color]::FromArgb(255, 45, 45, 45))   # 与面板底色一致 #2D2D2D
if ($srcBmp) {
    Draw-SourceContained $gs 620 300 620 0.0
} else {
    $logo = New-IconBitmap 96
    $gs.DrawImage($logo, (620 - 96) / 2, (300 - 96) / 2, 96, 96)
    $logo.Dispose()
}
$splash.Save((Join-Path $OutDir "SplashScreen.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$gs.Dispose(); $splash.Dispose()

# 主图（后续可复用）
$master = New-IconBitmap 512
$master.Save((Join-Path $OutDir "IconMaster.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$master.Dispose()

Write-Output "Assets: $OutDir"
