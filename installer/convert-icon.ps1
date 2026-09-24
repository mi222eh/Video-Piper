<#
.SYNOPSIS
    Converts a PNG image into a multi-resolution Windows .ico file (16, 32, 48, 64, 128, 256 px).
#>
param(
    [string]$InputPng = "$PSScriptRoot\..\app-icon.png",
    [string]$OutputIco = "$PSScriptRoot\app.ico"
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $InputPng)) {
    Write-Error "Source image '$InputPng' not found."
    exit 1
}

$bmp = [System.Drawing.Bitmap]::FromFile((Resolve-Path $InputPng).Path)
$sizes = @(16, 32, 48, 64, 128, 256)
$images = @()

foreach ($size in $sizes) {
    $resized = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($resized)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($bmp, 0, 0, $size, $size)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()

    $images += @{
        Size = $size
        Bytes = $ms.ToArray()
    }
    $ms.Dispose()
}
$bmp.Dispose()

$fs = [System.IO.File]::Create((Resolve-Path (Split-Path $OutputIco -Parent) -ErrorAction SilentlyContinue).Path + "\" + (Split-Path $OutputIco -Leaf))
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONHEADER
$bw.Write([UInt16]0)            # Reserved (must be 0)
$bw.Write([UInt16]1)            # Type 1 = ICO
$bw.Write([UInt16]$images.Count) # Number of images

# Calculate offset to image data: header is 6 bytes + 16 bytes per entry
$offset = 6 + ($images.Count * 16)

# ICONDIRENTRY for each image
foreach ($img in $images) {
    $w = if ($img.Size -ge 256) { 0 } else { [byte]$img.Size }
    $h = if ($img.Size -ge 256) { 0 } else { [byte]$img.Size }
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)          # Color count (0 = >= 8bpp)
    $bw.Write([byte]0)          # Reserved
    $bw.Write([UInt16]1)        # Color planes
    $bw.Write([UInt16]32)       # Bits per pixel
    $bw.Write([UInt32]$img.Bytes.Length) # Image data size
    $bw.Write([UInt32]$offset)  # Offset to image data
    $offset += $img.Bytes.Length
}

# Image data (PNG format payload)
foreach ($img in $images) {
    $bw.Write($img.Bytes)
}

$bw.Flush()
$fs.Close()

Write-Host "Created $OutputIco ($($images.Count) resolutions)"
