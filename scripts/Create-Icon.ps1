# Reproducible vector drawing; no external graphics or libraries.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$output = Join-Path $PSScriptRoot '../assets'
New-Item -ItemType Directory -Path $output -Force | Out-Null
function RoundBox($graphics, $brush, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x,$y,$d,$d,180,90); $path.AddArc(($x+$w-$d),$y,$d,$d,270,90)
    $path.AddArc(($x+$w-$d),($y+$h-$d),$d,$d,0,90); $path.AddArc($x,($y+$h-$d),$d,$d,90,90)
    $path.CloseFigure(); $graphics.FillPath($brush,$path); $path.Dispose()
}
$frames = @()
foreach ($size in @(16,20,24,32,48,64,128,256)) {
    $bitmap = New-Object Drawing.Bitmap($size,$size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.ScaleTransform(($size/256.0),($size/256.0))
    $blue = New-Object Drawing.Drawing2D.LinearGradientBrush([Drawing.Point]::new(0,8),[Drawing.Point]::new(0,248),[Drawing.ColorTranslator]::FromHtml('#24AFE3'),[Drawing.ColorTranslator]::FromHtml('#075E99'))
    RoundBox $g $blue 8 8 240 240 44
    $dark = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#182C43'))
    RoundBox $g $dark 25 43 99 132 13
    RoundBox $g $dark 132 43 99 132 13
    $font = New-Object Drawing.Font('Arial',87,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
    $format = New-Object Drawing.StringFormat
    $format.Alignment = 'Center'; $format.LineAlignment = 'Center'
    $g.DrawString('1',$font,[Drawing.Brushes]::White,[Drawing.RectangleF]::new(22,40,102,134),$format)
    $g.DrawString('2',$font,[Drawing.Brushes]::White,[Drawing.RectangleF]::new(130,40,102,134),$format)
    $line = New-Object Drawing.Pen([Drawing.Color]::FromArgb(160,0,0,0),3)
    $g.DrawLine($line,26,109,123,109); $g.DrawLine($line,133,109,230,109)
    # Cloud and sun indicate weather without introducing tiny text.
    $sun = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#FFCF59'))
    $g.FillEllipse($sun,130,186,35,35)
    $g.FillEllipse([Drawing.Brushes]::White,94,198,37,30)
    $g.FillEllipse([Drawing.Brushes]::White,115,188,35,39)
    RoundBox $g ([Drawing.Brushes]::White) 96 211 68 19 9
    $stream = New-Object IO.MemoryStream
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $output 'Zegar.png'),$stream.ToArray()) }
    $frames += ,@($size,$stream.ToArray())
    $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $blue.Dispose(); $dark.Dispose(); $font.Dispose(); $format.Dispose(); $line.Dispose(); $sun.Dispose()
}
$file = [IO.File]::Create((Join-Path $output 'Zegar.ico'))
$writer = New-Object IO.BinaryWriter($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame[0] -eq 256) { 0 } else { $frame[0] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frame[1].Length); $writer.Write([uint32]$offset)
    $offset += $frame[1].Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame[1]) }
$writer.Dispose(); $file.Dispose()
