#Requires -Version 7
<#
.SYNOPSIS
  Bake state-tinted variants of icon SVGs for the Home Assistant CmdPal extension.

.DESCRIPTION
  CmdPal's IconInfo has no runtime tint, so each icon ships pre-baked in
  the three Raycast palette variants:
    -on            yellow  #FECF02   (active state)
    -off           blue    #178CFB   (idle / inactive state)
    -unavailable   grey    #888888

  Source SVGs use fill="currentColor". This script string-replaces that
  fill and writes the three variants alongside the base file.

.PARAMETER Bakes
  An array of hashtables, each describing one bake operation:
    Out      Stem of the output filenames (e.g. "switch" → switch-on.svg, switch-off.svg, switch-unavailable.svg)
    OnSrc    Optional. Path of the base SVG to use for the -on variant.
             Defaults to "<Out>.svg". When yellow/blue use different
             geometries (e.g. door-open vs door-closed) supply both OnSrc
             and OffSrc.
    OffSrc   Optional. Path of the base SVG for -off and -unavailable.
             Defaults to OnSrc / "<Out>.svg".
    Variants Optional. Subset of @('on','off','unavailable'). Defaults to all three.

.PARAMETER IconsDir
  Target directory. Defaults to ../HomeAssistantCommandPalette/Assets/Icons.
#>
param(
    [Parameter(Mandatory)]
    [hashtable[]]$Bakes,

    [string]$IconsDir = (Join-Path $PSScriptRoot '..\HomeAssistantCommandPalette\Assets\Icons')
)

$ErrorActionPreference = 'Stop'

$colors = @{
    'low'          = '#FF4D4F'
    'on'           = '#FECF02'
    'off'          = '#178CFB'
    'unavailable'  = '#888888'
}

$IconsDir = (Resolve-Path $IconsDir).Path

function Get-SourcePath([string]$name) {
    if ([System.IO.Path]::IsPathRooted($name)) { return $name }
    return Join-Path $IconsDir $name
}

function Bake-Variant {
    param(
        [string]$SourcePath,
        [string]$Color,
        [string]$OutPath
    )
    if (-not (Test-Path $SourcePath)) { throw "Source not found: $SourcePath" }
    $svg = Get-Content -LiteralPath $SourcePath -Raw
    $tinted = $svg -replace 'fill="currentColor"', ('fill="{0}"' -f $Color)
    if ($tinted -eq $svg) {
        # Source SVGs without an explicit fill="currentColor" can't be baked.
        throw "Source $SourcePath has no fill=`"currentColor`" — can't bake."
    }
    Set-Content -LiteralPath $OutPath -Value $tinted -NoNewline -Encoding utf8NoBOM
    Write-Host ("  {0,-44}  ← {1}" -f (Split-Path -Leaf $OutPath), (Split-Path -Leaf $SourcePath))
}

foreach ($b in $Bakes) {
    $stem    = $b.Out
    $variants = if ($b.ContainsKey('Variants')) { $b.Variants } else { @('on','off','unavailable') }
    $onSrc   = if ($b.ContainsKey('OnSrc'))  { Get-SourcePath $b.OnSrc }  else { Join-Path $IconsDir "$stem.svg" }
    $offSrc  = if ($b.ContainsKey('OffSrc')) { Get-SourcePath $b.OffSrc } else { $onSrc }

    Write-Host ("[{0}]" -f $stem)
    foreach ($v in $variants) {
        $src = if ($v -eq 'on') { $onSrc } else { $offSrc }
        $out = Join-Path $IconsDir ("{0}-{1}.svg" -f $stem, $v)
        Bake-Variant -SourcePath $src -Color $colors[$v] -OutPath $out
    }
}

Write-Host "`nDone."
