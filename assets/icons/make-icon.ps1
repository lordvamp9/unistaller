# ============================================================================
#  CleanWipe — generador de icono e imágenes de marca (obra original, vamp9)
#  Dibuja un mosaico con gradiente azul→teal y una escoba + destellos,
#  exporta PNG en varios tamaños y empaqueta un .ico multi-resolución.
# ============================================================================
Add-Type -AssemblyName System.Drawing

$OutDir = $PSScriptRoot
if (-not $OutDir) { $OutDir = (Get-Location).Path }

function New-RoundedRectPath {
    param([float]$x, [float]$y, [float]$w, [float]$h, [float]$r)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Draw-Sparkle {
    param($g, [float]$cx, [float]$cy, [float]$size, $brush)
    # Destello de 4 puntas (rombo cóncavo).
    $s = $size
    $k = $size * 0.18
    $pts = @(
        (New-Object System.Drawing.PointF($cx, ($cy - $s))),
        (New-Object System.Drawing.PointF(($cx + $k), ($cy - $k))),
        (New-Object System.Drawing.PointF(($cx + $s), $cy)),
        (New-Object System.Drawing.PointF(($cx + $k), ($cy + $k))),
        (New-Object System.Drawing.PointF($cx, ($cy + $s))),
        (New-Object System.Drawing.PointF(($cx - $k), ($cy + $k))),
        (New-Object System.Drawing.PointF(($cx - $s), $cy)),
        (New-Object System.Drawing.PointF(($cx - $k), ($cy - $k)))
    )
    $g.FillPolygon($brush, $pts)
}

function Render-Icon {
    param([int]$Size)
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $S = [float]$Size
    $margin = $S * 0.06
    $tile = New-RoundedRectPath -x $margin -y $margin -w ($S - 2*$margin) -h ($S - 2*$margin) -r ($S * 0.22)

    # Fondo con gradiente diagonal azul eléctrico → verde-teal.
    $c1 = [System.Drawing.Color]::FromArgb(255, 30, 111, 255)   # #1E6FFF
    $c2 = [System.Drawing.Color]::FromArgb(255, 0, 200, 150)    # #00C896
    $rect = New-Object System.Drawing.RectangleF(0, 0, $S, $S)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 45)
    $g.FillPath($grad, $tile)

    # Brillo superior sutil (glow).
    $glowPath = New-RoundedRectPath -x $margin -y $margin -w ($S - 2*$margin) -h (($S - 2*$margin) * 0.55) -r ($S * 0.22)
    $pTop = New-Object System.Drawing.PointF(0, $margin)
    $pBot = New-Object System.Drawing.PointF(0, ($S*0.55))
    $glowBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $pTop, $pBot,
        [System.Drawing.Color]::FromArgb(70, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(0, 255, 255, 255))
    $g.FillPath($glowBrush, $glowPath)

    # --- Escoba estilizada (blanca) ---
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(45, 6, 12, 30))

    $state = $g.Save()
    $g.TranslateTransform($S*0.5, $S*0.52)
    $g.RotateTransform(35)

    # Mango (rectángulo redondeado).
    $handleW = $S * 0.085
    $handleH = $S * 0.46
    $handle = New-RoundedRectPath -x (-$handleW/2) -y (-$handleH*0.78) -w $handleW -h ($handleH*0.7) -r ($handleW/2)
    $g.FillPath($shadow, (New-RoundedRectPath -x (-$handleW/2 + $S*0.012) -y (-$handleH*0.78 + $S*0.012) -w $handleW -h ($handleH*0.7) -r ($handleW/2)))
    $g.FillPath($white, $handle)

    # Cepillo (trapecio).
    $by = $handleH * -0.10
    $topW = $S * 0.14
    $botW = $S * 0.34
    $brushH = $S * 0.26
    $brushPts = @(
        (New-Object System.Drawing.PointF((-$topW/2), $by)),
        (New-Object System.Drawing.PointF(($topW/2), $by)),
        (New-Object System.Drawing.PointF(($botW/2), ($by + $brushH))),
        (New-Object System.Drawing.PointF((-$botW/2), ($by + $brushH)))
    )
    $g.FillPolygon($white, $brushPts)

    # Líneas de las cerdas (separadores con color de fondo).
    $penBlue = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 30, 111, 255), ($S*0.015))
    for ($i = -2; $i -le 2; $i++) {
        $x0 = $i * ($S * 0.045)
        $x1 = $i * ($S * 0.07)
        $g.DrawLine($penBlue, (New-Object System.Drawing.PointF($x0, ($by + $brushH*0.35))), (New-Object System.Drawing.PointF($x1, ($by + $brushH))))
    }
    $g.Restore($state)

    # Destellos (limpieza).
    Draw-Sparkle $g ($S*0.70) ($S*0.30) ($S*0.085) $white
    Draw-Sparkle $g ($S*0.78) ($S*0.50) ($S*0.05) $white
    Draw-Sparkle $g ($S*0.30) ($S*0.34) ($S*0.045) $white

    $g.Dispose()
    return $bmp
}

# --- Exportar PNGs ---
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngFiles = @{}
foreach ($sz in $sizes) {
    $bmp = Render-Icon -Size $sz
    $file = Join-Path $OutDir "icon-$sz.png"
    $bmp.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngFiles[$sz] = $file
    if ($sz -eq 256) { $bmp.Save((Join-Path $OutDir "logo.png"), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
}

# --- Empaquetar .ico (frames PNG, soportado en Windows Vista+) ---
$icoPath = Join-Path $OutDir "icon.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$frames = $sizes  # incluir todos los tamaños
# ICONDIR
$bw.Write([UInt16]0)          # reserved
$bw.Write([UInt16]1)          # type = icon
$bw.Write([UInt16]$frames.Count)

$offset = 6 + (16 * $frames.Count)
$datas = @()
foreach ($sz in $frames) {
    $bytes = [System.IO.File]::ReadAllBytes($pngFiles[$sz])
    $datas += ,$bytes
}
for ($i = 0; $i -lt $frames.Count; $i++) {
    $sz = $frames[$i]
    $len = $datas[$i].Length
    $wb = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([Byte]$wb)       # width  (0 = 256)
    $bw.Write([Byte]$wb)       # height (0 = 256)
    $bw.Write([Byte]0)         # color count
    $bw.Write([Byte]0)         # reserved
    $bw.Write([UInt16]1)       # planes
    $bw.Write([UInt16]32)      # bpp
    $bw.Write([UInt32]$len)    # data size
    $bw.Write([UInt32]$offset) # data offset
    $offset += $len
}
foreach ($d in $datas) { $bw.Write($d) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output "Icono generado: $icoPath"
Write-Output ("Tamaños incluidos: " + ($frames -join ", "))
