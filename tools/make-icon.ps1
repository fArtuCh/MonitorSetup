param([string]$OutPath)
Add-Type -AssemblyName System.Drawing

function RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $u = $s / 32.0

    # Stand
    $standBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 70, 80, 96))
    $g.FillRectangle($standBrush, 14 * $u, 22 * $u, 4 * $u, 5 * $u)
    $base = RoundRect (9 * $u) (26 * $u) (14 * $u) (3 * $u) (1.5 * $u)
    $g.FillPath($standBrush, $base)

    # Bezel
    $bezel = RoundRect (1 * $u) (3 * $u) (30 * $u) (21 * $u) (3 * $u)
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 40, 46, 56))), $bezel)

    # Screen
    $sx = 3 * $u; $sy = 5 * $u; $sw = 26 * $u; $sh = 17 * $u
    $screen = RoundRect $sx $sy $sw $sh (1.5 * $u)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new($sx, $sy)), ([System.Drawing.PointF]::new($sx + $sw, $sy + $sh)), ([System.Drawing.Color]::FromArgb(255, 0, 120, 215)), ([System.Drawing.Color]::FromArgb(255, 0, 188, 242))
    $g.FillPath($grad, $screen)

    # Two "displays" on the screen, like the arrangement view
    if ($s -ge 24) {
        $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
        $soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 255, 255))
        $g.FillPath($white, (RoundRect (6 * $u) (8.5 * $u) (11 * $u) (8 * $u) (1 * $u)))
        $g.FillPath($soft, (RoundRect (18 * $u) (10.5 * $u) (8 * $u) (6 * $u) (1 * $u)))
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = Draw $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$fs = [System.IO.File]::Create($OutPath)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$len); $w.Write([UInt32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()

# Preview for checking the look
(Draw 256).Save([System.IO.Path]::ChangeExtension($OutPath, '.preview.png'))
