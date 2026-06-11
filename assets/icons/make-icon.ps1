# ============================================================================
#  CleanWipe — generador de icono e imágenes de marca (obra original, vamp9)
#  v2: estilo RETRO AERO GLOSSY — tile aqua con gradiente multi-parada,
#  brillo de cristal (gloss), biseles, sombra y escoba con profundidad.
#  Exporta PNG en varios tamaños y empaqueta un .ico multi-resolución.
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

function Draw-Broom {
    param($g, [float]$S, $handleBrush, $brushBrush, $penColor, [float]$dx, [float]$dy)
    # Dibuja la escoba (mango + cepillo). dx/dy permiten dibujar una copia como sombra.
    $state = $g.Save()
    $g.TranslateTransform(($S*0.5 + $dx), ($S*0.54 + $dy))
    $g.RotateTransform(35)

    $handleW = $S * 0.085
    $handleH = $S * 0.46
    $handle = New-RoundedRectPath -x (-$handleW/2) -y (-$handleH*0.78) -w $handleW -h ($handleH*0.7) -r ($handleW/2)
    $g.FillPath($handleBrush, $handle)

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
    $g.FillPolygon($brushBrush, $brushPts)

    if ($penColor) {
        $pen = New-Object System.Drawing.Pen($penColor, ($S*0.016))
        for ($i = -2; $i -le 2; $i++) {
            $x0 = $i * ($S * 0.045)
            $x1 = $i * ($S * 0.07)
            $g.DrawLine($pen, (New-Object System.Drawing.PointF($x0, ($by + $brushH*0.35))), (New-Object System.Drawing.PointF($x1, ($by + $brushH))))
        }
        $pen.Dispose()
    }
    $g.Restore($state)
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
    $margin = $S * 0.05
    $tileW = $S - 2*$margin
    $r = $S * 0.23

    # --- Sombra exterior suave (look retro 3D) ---
    $shadowPath = New-RoundedRectPath -x ($margin + $S*0.01) -y ($margin + $S*0.03) -w $tileW -h $tileW -r $r
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(45, 6, 30, 70))
    $g.FillPath($shadowBrush, $shadowPath)

    # --- Tile base: gradiente aqua multi-parada (cian claro → azul profundo) ---
    $tile = New-RoundedRectPath -x $margin -y $margin -w $tileW -h $tileW -r $r
    $pTop = New-Object System.Drawing.PointF(0, $margin)
    $pBot = New-Object System.Drawing.PointF(0, ($margin + $tileW))
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $pTop, $pBot,
        [System.Drawing.Color]::FromArgb(255, 166, 235, 255),
        [System.Drawing.Color]::FromArgb(255, 8, 70, 184))
    $blend = New-Object System.Drawing.Drawing2D.ColorBlend(4)
    $blend.Colors = [System.Drawing.Color[]]@(
        [System.Drawing.Color]::FromArgb(255, 166, 235, 255),  # cian hielo
        [System.Drawing.Color]::FromArgb(255,  63, 180, 255),  # celeste
        [System.Drawing.Color]::FromArgb(255,  30, 140, 255),  # azul aero
        [System.Drawing.Color]::FromArgb(255,   8,  70, 184)   # azul profundo
    )
    $blend.Positions = [single[]]@(0.0, 0.35, 0.6, 1.0)
    $grad.InterpolationColors = $blend
    $g.FillPath($grad, $tile)

    # --- Vetas "liquid": dos blobs suaves de color dentro del tile ---
    $g.SetClip($tile)
    $blob1 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(50, 0, 220, 170))
    $g.FillEllipse($blob1, ($S*0.55), ($S*0.55), ($S*0.55), ($S*0.45))
    $blob2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 255, 255, 255))
    $g.FillEllipse($blob2, (-$S*0.1), ($S*0.45), ($S*0.5), ($S*0.5))
    $g.ResetClip()

    # --- Escoba: primero sombra, luego cuerpo blanco con cerdas azules ---
    $broomShadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 4, 24, 70))
    Draw-Broom $g $S $broomShadow $broomShadow $null ($S*0.014) ($S*0.02)

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $pHandleTop = New-Object System.Drawing.PointF(0, ($S*0.12))
    $pHandleBot = New-Object System.Drawing.PointF(0, ($S*0.86))
    $brushGrad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $pHandleTop, $pHandleBot,
        [System.Drawing.Color]::FromArgb(255, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(255, 205, 228, 250))
    $bristlePen = [System.Drawing.Color]::FromArgb(255, 30, 120, 235)
    Draw-Broom $g $S $white $brushGrad $bristlePen 0 0

    # --- Destellos ---
    Draw-Sparkle $g ($S*0.72) ($S*0.27) ($S*0.10) (New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(90, 255, 255, 255)))
    Draw-Sparkle $g ($S*0.72) ($S*0.27) ($S*0.075) $white
    Draw-Sparkle $g ($S*0.80) ($S*0.48) ($S*0.045) $white
    Draw-Sparkle $g ($S*0.27) ($S*0.36) ($S*0.04) $white

    # --- GLOSS aero: lámina de cristal en la mitad superior (recortada al tile) ---
    $g.SetClip($tile)
    $glossRectY = $margin
    $glossH = $tileW * 0.52
    $pgTop = New-Object System.Drawing.PointF(0, $glossRectY)
    $pgBot = New-Object System.Drawing.PointF(0, ($glossRectY + $glossH))
    $glossBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $pgTop, $pgBot,
        [System.Drawing.Color]::FromArgb(165, 255, 255, 255),
        [System.Drawing.Color]::FromArgb(12, 255, 255, 255))
    # Elipse ancha cuya curva inferior forma el borde curvo del cristal.
    $g.FillEllipse($glossBrush, (-$S*0.25), ($glossRectY - $tileW*0.45), ($S*1.5), ($glossH + $tileW*0.45))
    $g.ResetClip()

    # --- Biseles: borde interior claro arriba, borde exterior oscuro ---
    $penLight = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 255, 255, 255), [Math]::Max(1.0, $S*0.012))
    $innerTile = New-RoundedRectPath -x ($margin + $S*0.012) -y ($margin + $S*0.012) -w ($tileW - $S*0.024) -h ($tileW - $S*0.024) -r ($r - $S*0.01)
    $g.DrawPath($penLight, $innerTile)
    $penDark = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 6, 45, 120), [Math]::Max(1.0, $S*0.01))
    $g.DrawPath($penDark, $tile)

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

$frames = $sizes
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
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
    $bw.Write([Byte]$wb)
    $bw.Write([Byte]$wb)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$len)
    $bw.Write([UInt32]$offset)
    $offset += $len
}
foreach ($d in $datas) { $bw.Write($d) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output "Icono generado: $icoPath"
Write-Output ("Tamaños incluidos: " + ($frames -join ", "))
