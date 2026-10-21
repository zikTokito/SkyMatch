$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$assets = Join-Path $root 'assets'
if (!(Test-Path $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }
$pngPath = Join-Path $assets 'app.png'
$icoPath = Join-Path $assets 'app.ico'

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

$rect = New-Object System.Drawing.RectangleF 8, 8, 240, 240
$radius = 56
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$d = $radius * 2
$path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
$path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
$path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
$path.CloseFigure()
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 211, 122)), ([System.Drawing.Color]::FromArgb(239, 139, 64)), 55
$g.FillPath($brush, $path)
$brush.Dispose()

$dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 22, 28, 38))
$sunRect = New-Object System.Drawing.RectangleF 66, 58, 88, 88
$g.FillEllipse($dark, $sunRect)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 22, 28, 38)), 11
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
for ($i = 0; $i -lt 8; $i++) {
  $angle = $i * [Math]::PI / 4
  $cx = 110.0; $cy = 102.0
  $x1 = $cx + [Math]::Cos($angle) * 55
  $y1 = $cy + [Math]::Sin($angle) * 55
  $x2 = $cx + [Math]::Cos($angle) * 73
  $y2 = $cy + [Math]::Sin($angle) * 73
  $g.DrawLine($pen, [float]$x1, [float]$y1, [float]$x2, [float]$y2)
}

$cloud = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 255, 248, 235))
$g.FillEllipse($cloud, 76, 142, 64, 54)
$g.FillEllipse($cloud, 112, 126, 76, 70)
$g.FillEllipse($cloud, 158, 150, 52, 46)
$g.FillRectangle($cloud, 94, 166, 100, 30)
$cloud.Dispose()
$dark.Dispose()
$pen.Dispose()

$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()

$png = [System.IO.File]::ReadAllBytes($pngPath)
$fs = New-Object System.IO.FileStream $icoPath, ([System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]1)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]32)
$bw.Write([UInt32]$png.Length)
$bw.Write([UInt32]22)
$bw.Write($png)
$bw.Close()
$fs.Close()

Write-Host "Created $icoPath"
