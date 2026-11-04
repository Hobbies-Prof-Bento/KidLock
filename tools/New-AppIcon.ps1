<#
.SYNOPSIS
    Gera src/KidLock.App/Assets/kidlock.ico (cadeado, vários tamanhos, PNG dentro do ICO).
.DESCRIPTION
    Usa a mesma geometria de IconFactory.DrawPadlock (grade 32x32). Rode só quando quiser mudar o ícone
    do executável; o arquivo gerado fica versionado.
#>
[CmdletBinding()]
param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
# No Windows PowerShell 5.1, $PSScriptRoot ainda não existe nos valores padrão de param().
if (-not $OutputPath) { $OutputPath = Join-Path $PSScriptRoot '..\src\KidLock.App\Assets\kidlock.ico' }
Add-Type -AssemblyName System.Drawing

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$color = [System.Drawing.Color]::FromArgb(255, 15, 108, 189)

function New-RoundedRectangle([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $d = $r * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-PadlockPng([int]$size) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.ScaleTransform($size / 32.0, $size / 32.0)

        $pen = New-Object System.Drawing.Pen $color, ([single]3.6)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $shackle = New-Object System.Drawing.Drawing2D.GraphicsPath
        $shackle.AddLine([single]10, [single]15, [single]10, [single]10)
        $shackle.AddArc([single]10, [single]4, [single]12, [single]12, [single]180, [single]180)
        $shackle.AddLine([single]22, [single]10, [single]22, [single]15)
        $graphics.DrawPath($pen, $shackle)

        $body = New-RoundedRectangle 5 14 22 16 3.5
        $brush = New-Object System.Drawing.SolidBrush $color
        $graphics.FillPath($brush, $body)
        $graphics.FillEllipse([System.Drawing.Brushes]::White, [single]14, [single]18.5, [single]4, [single]4)
        $graphics.FillRectangle([System.Drawing.Brushes]::White, [single]15.1, [single]21, [single]1.8, [single]4.5)

        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return , $stream.ToArray()
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$images = @()
foreach ($size in $sizes) { $images += , (New-PadlockPng $size) }

$OutputPath = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath) | Out-Null
$file = [System.IO.File]::Create($OutputPath)
$writer = New-Object System.IO.BinaryWriter $file
try {
    # ICONDIR
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    # ICONDIRENTRY para cada tamanho (0 significa 256)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length)
        $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }

    foreach ($image in $images) { $writer.Write([byte[]]$image) }
}
finally {
    $writer.Dispose()
}

Write-Host "Ícone gerado: $OutputPath"
