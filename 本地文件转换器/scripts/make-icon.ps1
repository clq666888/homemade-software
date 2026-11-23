$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$project = Split-Path -Parent $PSScriptRoot
$sizes = @(16,32,48,64,128,256)
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size,$size)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform(($size/256.0),($size/256.0))
    $g.Clear([System.Drawing.Color]::Transparent)
    $blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(35,96,215))
    $white = [System.Drawing.Brushes]::White
    $g.FillRectangle($blue,12,12,232,232)
    $g.FillRectangle($white,66,43,105,146)
    $line = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(174,201,250),10)
    $g.DrawLine($line,87,78,148,78); $g.DrawLine($line,87,104,148,104)
    $arrow = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(35,96,215),16)
    $g.DrawLine($arrow,105,154,207,154); $g.DrawLine($arrow,180,128,207,154); $g.DrawLine($arrow,180,180,207,154)
    $mem = New-Object System.IO.MemoryStream
    $bitmap.Save($mem,[System.Drawing.Imaging.ImageFormat]::Png)
    $images += ,$mem.ToArray()
    if ($size -eq 256) { $bitmap.Save((Join-Path $project 'assets\app.png'),[System.Drawing.Imaging.ImageFormat]::Png) }
    $mem.Dispose(); $arrow.Dispose(); $line.Dispose(); $blue.Dispose(); $g.Dispose(); $bitmap.Dispose()
}
$stream = [System.IO.File]::Create((Join-Path $project 'assets\app.ico'))
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16*$sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($data in $images) { $writer.Write([byte[]]$data) }
$writer.Dispose(); $stream.Dispose()
