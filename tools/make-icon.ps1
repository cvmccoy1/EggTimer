# Generates src\EggTimer\Assets\EggTimer.ico: a cream/amber egg with clock hands.
# Run with Windows PowerShell:  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
param(
    [string]$OutFile = (Join-Path $PSScriptRoot "..\src\EggTimer\Assets\EggTimer.ico"),
    [string]$PreviewFile
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-EggBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [double]$size
    $h = $s * 0.94; $y0 = ($s - $h) / 2
    $rx = $s * 0.38; $cx = $s / 2
    $pt = { param($x, $y) New-Object System.Drawing.PointF ([single]$x), ([single]$y) }

    # Egg outline: narrower at the top, widest below the middle.
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $top = & $pt $cx $y0
    $right = & $pt ($cx + $rx) ($y0 + 0.60 * $h)
    $bottom = & $pt $cx ($y0 + $h)
    $left = & $pt ($cx - $rx) ($y0 + 0.60 * $h)
    $path.AddBezier($top, (& $pt ($cx + 0.60 * $rx) $y0), (& $pt ($cx + $rx) ($y0 + 0.28 * $h)), $right)
    $path.AddBezier($right, (& $pt ($cx + $rx) ($y0 + 0.88 * $h)), (& $pt ($cx + 0.58 * $rx) ($y0 + $h)), $bottom)
    $path.AddBezier($bottom, (& $pt ($cx - 0.58 * $rx) ($y0 + $h)), (& $pt ($cx - $rx) ($y0 + 0.88 * $h)), $left)
    $path.AddBezier($left, (& $pt ($cx - $rx) ($y0 + 0.28 * $h)), (& $pt ($cx - 0.60 * $rx) $y0), $top)
    $path.CloseFigure()

    $rect = New-Object System.Drawing.RectangleF ([single]($cx - $rx)), ([single]$y0), ([single](2 * $rx)), ([single]$h)
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.Color]::FromArgb(255, 255, 246, 225)),
        ([System.Drawing.Color]::FromArgb(255, 255, 181, 71)), 60.0
    $g.FillPath($fill, $path)

    $outlineWidth = [Math]::Max(1.0, $s / 28)
    $outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 138, 90, 16)), ([single]$outlineWidth)
    $g.DrawPath($outline, $path)

    # Clock face centred in the lower, wider part of the egg.
    $ccx = $cx; $ccy = $y0 + 0.60 * $h
    $dark = [System.Drawing.Color]::FromArgb(255, 27, 30, 36)
    if ($size -ge 32) {
        $tickPen = New-Object System.Drawing.Pen $dark, ([single]([Math]::Max(1.0, $s / 40)))
        $r1 = $rx * 0.72; $r2 = $rx * 0.58
        foreach ($i in 0..11) {
            if ($size -lt 64 -and ($i % 3) -ne 0) { continue }
            $a = $i * [Math]::PI / 6
            $g.DrawLine($tickPen, [single]($ccx + $r2 * [Math]::Sin($a)), [single]($ccy - $r2 * [Math]::Cos($a)),
                                  [single]($ccx + $r1 * [Math]::Sin($a)), [single]($ccy - $r1 * [Math]::Cos($a)))
        }
    }
    $handPen = New-Object System.Drawing.Pen $dark, ([single]([Math]::Max(1.5, $s / 14)))
    $handPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($handPen, [single]$ccx, [single]$ccy, [single]$ccx, [single]($ccy - $rx * 0.55))            # 12 o'clock
    $a = 2 * [Math]::PI / 6 * 2                                                                               # 4 o'clock
    $g.DrawLine($handPen, [single]$ccx, [single]$ccy, [single]($ccx + $rx * 0.40 * [Math]::Sin($a)),
                                                     [single]($ccy - $rx * 0.40 * [Math]::Cos($a)))
    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($sz in $sizes) {
    $bmp = New-EggBitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($PreviewFile -and $sz -eq 256) { $bmp.Save($PreviewFile, [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    , $ms.ToArray()
}

# ICO container with PNG-compressed entries (supported since Windows Vista).
New-Item -ItemType Directory -Force (Split-Path $OutFile) | Out-Null
$fs = [System.IO.File]::Create($OutFile)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$pngs[$i].Length); $w.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $w.Write($p) }
$w.Close()
"Wrote $OutFile"
