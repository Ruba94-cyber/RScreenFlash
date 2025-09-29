[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$OutputDir = (Join-Path $PSScriptRoot '..\ScreenshotFlash.Package\Images'),
    [switch]$Force
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

function New-Logo {
    param(
        [int]$Width,
        [int]$Height,
        [string]$FileName,
        [System.Drawing.Color]$BackColor,
        [System.Drawing.Color]$AccentColor,
        [string]$Label
    )

    $filePath = Join-Path $OutputDir $FileName
    if ((Test-Path -LiteralPath $filePath) -and -not $Force) {
        Write-Verbose "Skipping existing asset: $FileName"
        return
    }

    $bitmap = New-Object System.Drawing.Bitmap $Width, $Height
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.Clear($BackColor)

    $padding = [Math]::Round([Math]::Min($Width, $Height) * 0.1)
    $rect = New-Object System.Drawing.Rectangle $padding, $padding, ($Width - 2 * $padding), ($Height - 2 * $padding)
    $brush = New-Object System.Drawing.SolidBrush $AccentColor
    $graphics.FillRectangle($brush, $rect)
    $brush.Dispose()

    if ($Label) {
        $fontSize = [Math]::Round([Math]::Min($rect.Width, $rect.Height) * 0.45)
        if ($fontSize -lt 8) { $fontSize = 8 }
        $font = New-Object System.Drawing.Font 'Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel
        $textBrush = New-Object System.Drawing.SolidBrush [System.Drawing.Color]::White
        $format = New-Object System.Drawing.StringFormat
        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $graphics.DrawString($Label, $font, $textBrush, $rect, $format)
        $textBrush.Dispose()
        $font.Dispose()
        $format.Dispose()
    }

    $bitmap.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()

    Write-Verbose "Generated $FileName"
}

$primary = [System.Drawing.Color]::FromArgb(0x18,0x4E,0xA8)
$accent = [System.Drawing.Color]::FromArgb(0x25,0x7B,0xE6)

New-Logo -Width 44  -Height 44  -FileName 'Square44x44Logo.png'  -BackColor $primary -AccentColor $accent -Label 'SF'
New-Logo -Width 71  -Height 71  -FileName 'Square71x71Logo.png'  -BackColor $primary -AccentColor $accent -Label 'SF'
New-Logo -Width 150 -Height 150 -FileName 'Square150x150Logo.png' -BackColor $primary -AccentColor $accent -Label 'SF'
New-Logo -Width 310 -Height 310 -FileName 'Square310x310Logo.png' -BackColor $primary -AccentColor $accent -Label 'SF'
New-Logo -Width 310 -Height 150 -FileName 'Wide310x150Logo.png' -BackColor $primary -AccentColor $accent -Label 'SF'
New-Logo -Width 50  -Height 50  -FileName 'StoreLogo.png'       -BackColor $primary -AccentColor $accent -Label 'SF'
New-Logo -Width 620 -Height 300 -FileName 'SplashScreen.png'    -BackColor $primary -AccentColor $accent -Label ''

Write-Verbose "Store assets generated in $OutputDir"
