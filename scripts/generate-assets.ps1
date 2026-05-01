#Requires -Version 7
<#
.SYNOPSIS
  Generate the MSIX asset PNGs from the official Home Assistant logo files.

.DESCRIPTION
  Reads source PNGs from -SourceDir and produces every Assets/*.png the
  Microsoft Store / MSIX validator requires. Square tiles use the
  logomark; the wide tile and splash screen use the wordmark.

  Only the scale-200 variants are generated here; the PrepareAssets MSBuild
  target copies them to the unscaled filenames at build time.
#>
param(
    [string]$SourceDir = "$env:USERPROFILE\Downloads\home-assistant-logo",
    [string]$AssetsDir = (Join-Path $PSScriptRoot '..\HomeAssistantCommandPalette\Assets')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

# Use the no-margins logomark — the with-margins variant has so much
# padding baked in that the icon looks small in dense UIs (e.g. CmdPal's
# list-row icon slot). The Resize-Image -Padding parameter below adds
# back any padding the MSIX validator needs.
$logomark = Join-Path $SourceDir 'home-assistant-logomark-color-on-light.png'
$wordmark = Join-Path $SourceDir 'home-assistant-wordmark-color-on-light.png'

foreach ($f in @($logomark, $wordmark)) {
    if (-not (Test-Path $f)) { throw "Source not found: $f" }
}

$AssetsDir = (Resolve-Path $AssetsDir).Path
Write-Host "Source:  $SourceDir"
Write-Host "Output:  $AssetsDir"

function Resize-Image {
    param(
        [string]$SourcePath,
        [int]$CanvasWidth,
        [int]$CanvasHeight,
        [string]$OutputPath,
        [double]$Padding = 0.0
    )

    $src = [System.Drawing.Image]::FromFile($SourcePath)
    try {
        $bmp = [System.Drawing.Bitmap]::new($CanvasWidth, $CanvasHeight)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
            $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)

            # Fit the source inside (canvas - padding) preserving aspect ratio.
            $availW = $CanvasWidth  * (1.0 - $Padding)
            $availH = $CanvasHeight * (1.0 - $Padding)
            $scale  = [Math]::Min($availW / $src.Width, $availH / $src.Height)
            $w      = [int]([Math]::Round($src.Width  * $scale))
            $h      = [int]([Math]::Round($src.Height * $scale))
            $x      = [int](($CanvasWidth  - $w) / 2)
            $y      = [int](($CanvasHeight - $h) / 2)

            $g.DrawImage($src, $x, $y, $w, $h)
        } finally { $g.Dispose() }

        $bmp.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    } finally {
        $src.Dispose()
    }
    Write-Host ("  {0,-60}  {1}x{2}" -f (Split-Path -Leaf $OutputPath), $CanvasWidth, $CanvasHeight)
}

# Square tiles use the logomark (with built-in margins)
$square = @(
    @{ Name = 'Square150x150Logo.scale-200.png';                       W = 300;  H = 300 },
    @{ Name = 'Square44x44Logo.scale-200.png';                         W = 88;   H = 88  },
    @{ Name = 'Square44x44Logo.targetsize-24_altform-unplated.png';    W = 24;   H = 24  },
    @{ Name = 'LockScreenLogo.scale-200.png';                          W = 48;   H = 48  },
    @{ Name = 'StoreLogo.scale-200.png';                               W = 100;  H = 100 }
)
foreach ($t in $square) {
    Resize-Image -SourcePath $logomark -CanvasWidth $t.W -CanvasHeight $t.H `
        -OutputPath (Join-Path $AssetsDir $t.Name)
}

# Wide tile + splash use the wordmark on a transparent canvas
Resize-Image -SourcePath $wordmark -CanvasWidth 620  -CanvasHeight 300 `
    -OutputPath (Join-Path $AssetsDir 'Wide310x150Logo.scale-200.png') -Padding 0.20

Resize-Image -SourcePath $wordmark -CanvasWidth 1240 -CanvasHeight 600 `
    -OutputPath (Join-Path $AssetsDir 'SplashScreen.scale-200.png') -Padding 0.30

Write-Host "`nDone. Run 'pwsh -File .\scripts\dev-deploy.ps1' to rebuild + reinstall."
