# Builds app.ico: blue rounded square with a white "MD" mark and a markdown-style down arrow. ASCII only.
# 16-128px stored as BMP DIB entries, 256px as PNG (rc/csc friendly, same as sample-list-wpf).
Add-Type -AssemblyName System.Drawing
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoPath = Join-Path $dir 'app.ico'
$prevPath = Join-Path $dir 'app-icon-preview.png'
$sizes = 16, 24, 32, 48, 64, 128, 256

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    $r = [single]($size * 0.18); $d = $r * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90); $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90); $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(255, 59, 130, 246)), ([System.Drawing.Color]::FromArgb(255, 29, 78, 216)), 90
    $g.FillPath($br, $path)
    $white = [System.Drawing.Brushes]::White
    $fs = [single]($size * 0.40)
    $font = New-Object System.Drawing.Font 'Consolas', $fs, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $tr = New-Object System.Drawing.RectangleF 0, ($size * 0.12), $size, ($size * 0.50)
    $g.DrawString('MD', $font, $white, $tr, $sf)
    # down arrow (markdown logo style)
    $cx = $size / 2.0; $top = $size * 0.66; $bot = $size * 0.88; $hw = $size * 0.16; $sw = $size * 0.055
    $pts = @(
        (New-Object System.Drawing.PointF ($cx - $sw), $top), (New-Object System.Drawing.PointF ($cx + $sw), $top),
        (New-Object System.Drawing.PointF ($cx + $sw), ($top + ($bot - $top) * 0.45)), (New-Object System.Drawing.PointF ($cx + $hw), ($top + ($bot - $top) * 0.45)),
        (New-Object System.Drawing.PointF $cx, $bot), (New-Object System.Drawing.PointF ($cx - $hw), ($top + ($bot - $top) * 0.45)),
        (New-Object System.Drawing.PointF ($cx - $sw), ($top + ($bot - $top) * 0.45)))
    $g.FillPolygon($white, [System.Drawing.PointF[]]$pts)
    $g.Dispose(); $path.Dispose(); $br.Dispose(); $font.Dispose()
    return $bmp
}

$entries = @()
foreach ($sz in $sizes) {
    $bmp = New-IconBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    if ($sz -ge 256) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Save($prevPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        $bw = New-Object System.IO.BinaryWriter $ms
        $bw.Write([int]40); $bw.Write([int]$sz); $bw.Write([int]($sz * 2))
        $bw.Write([int16]1); $bw.Write([int16]32); $bw.Write([int]0); $bw.Write([int]0)
        $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
        for ($y = $sz - 1; $y -ge 0; $y--) {          # bottom-up BGRA
            for ($x = 0; $x -lt $sz; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
            }
        }
        $maskRow = [int]([math]::Ceiling($sz / 32.0) * 4)   # 1bpp AND mask, all zero (alpha does the work)
        $bw.Write((New-Object byte[] ($maskRow * $sz)))
        $bw.Flush()
    }
    $entries += , @{ Size = $sz; Data = $ms.ToArray() }
    $bmp.Dispose(); $ms.Dispose()
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$entries.Count)
$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $dim = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32)
    $w.Write([int]$e.Data.Length); $w.Write([int]$offset)
    $offset += $e.Data.Length
}
foreach ($e in $entries) { $w.Write($e.Data) }
$w.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
Write-Output ("wrote " + $icoPath + " (" + (Get-Item $icoPath).Length + " bytes, " + $entries.Count + " sizes)")