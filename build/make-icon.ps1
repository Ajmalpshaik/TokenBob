# Draws the TokenBob ring-ornament icon and writes src/TokenBob.App/Assets/TokenBob.ico
# (16–64 px as classic 32-bit bitmaps, 256 px as PNG). Run from anywhere:
#   powershell -ExecutionPolicy Bypass -File build/make-icon.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot '..\src\TokenBob.App\Assets\TokenBob.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 32.0
    $disc = New-Object System.Drawing.RectangleF (1 * $s), (1 * $s), (30 * $s), (30 * $s)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)), $disc)
    $g.DrawEllipse((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70, 0, 0, 0)), ([Math]::Max(1, 1 * $s))), $disc)

    $width = [Math]::Max(1.6, 3.6 * $s)
    $track = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 228, 226, 218)), $width
    $outer = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 63, 164, 91)), $width
    $inner = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 232, 162, 58)), $width
    foreach ($p in @($outer, $inner)) { $p.StartCap = 'Round'; $p.EndCap = 'Round' }

    $o = New-Object System.Drawing.RectangleF (6 * $s), (6 * $s), (20 * $s), (20 * $s)
    $i = New-Object System.Drawing.RectangleF (11 * $s), (11 * $s), (10 * $s), (10 * $s)
    $g.DrawEllipse($track, $o); $g.DrawArc($outer, $o, -90, 270)
    $g.DrawEllipse($track, $i); $g.DrawArc($inner, $i, -90, 180)

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    if ($size -ge 256) {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # BITMAPINFOHEADER + bottom-up BGRA pixels + empty AND mask (alpha does the masking).
        $bw = New-Object System.IO.BinaryWriter $ms
        $bw.Write([UInt32]40); $bw.Write([Int32]$size); $bw.Write([Int32]($size * 2))
        $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]0)
        $bw.Write([UInt32]0); $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
        $rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
        $data = $bmp.LockBits($rect, 'ReadOnly', ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
        $row = New-Object byte[] ($size * 4)
        for ($y = $size - 1; $y -ge 0; $y--) {
            [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]($data.Scan0.ToInt64() + $y * $data.Stride), $row, 0, $row.Length)
            $bw.Write($row)
        }
        $bmp.UnlockBits($data)
        $maskRow = [Math]::Ceiling($size / 32.0) * 4
        $bw.Write((New-Object byte[] ($maskRow * $size)))
        $bw.Flush()
    }
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 256
$frames = foreach ($size in $sizes) { , (New-Frame $size) }

$file = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $file
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($n = 0; $n -lt $sizes.Count; $n++) {
    $dim = if ($sizes[$n] -ge 256) { 0 } else { $sizes[$n] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$frames[$n].Length); $w.Write([UInt32]$offset)
    $offset += $frames[$n].Length
}
foreach ($frame in $frames) { $w.Write($frame) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $out)).Path + '\TokenBob.ico', $file.ToArray())
"Wrote $out ($($file.Length) bytes)"
