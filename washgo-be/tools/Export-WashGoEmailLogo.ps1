# Rasterize the supplied 100x100 vector geometry into a 4x PNG for email clients.
# System.Drawing is used only by this Windows asset-generation tool, never by the server.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $PSScriptRoot '../src/WashGo.Application/Features/AuthApplication/Templates/Assets/washgo-logo.png'
New-Item -ItemType Directory -Force -Path (Split-Path $destination) | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(400,400)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$background = New-Object Drawing.Drawing2D.GraphicsPath
$drop = New-Object Drawing.Drawing2D.GraphicsPath
$highlight = New-Object Drawing.Drawing2D.GraphicsPath
$blue = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#2563EB'))
$pen = New-Object Drawing.Pen($blue,5)
try {
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([Drawing.Color]::Transparent)
    $graphics.ScaleTransform(4,4)
    $background.AddArc(0,0,70,70,180,90)
    $background.AddArc(30,0,70,70,270,90)
    $background.AddArc(30,30,70,70,0,90)
    $background.AddArc(0,30,70,70,90,90)
    $background.CloseFigure()
    $graphics.FillPath($blue,$background)
    $drop.AddBezier(50,20,50,20,75,50,75,65)
    $drop.AddArc(25,40,50,50,0,180)
    $drop.AddBezier(25,65,25,50,50,20,50,20)
    $drop.CloseFigure()
    $graphics.FillPath([Drawing.Brushes]::White,$drop)
    $pen.StartCap = [Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $highlight.AddArc(35,50,30,30,180,-90)
    $graphics.DrawPath($pen,$highlight)
    $bitmap.Save([IO.Path]::GetFullPath($destination),[Drawing.Imaging.ImageFormat]::Png)
} finally {
    $pen.Dispose(); $blue.Dispose(); $highlight.Dispose(); $drop.Dispose()
    $background.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
