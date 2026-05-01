#Requires -Version 7
<#
.SYNOPSIS
  Rebuild, re-sign, and reinstall the Home Assistant Command Palette
  extension on the local machine.

.DESCRIPTION
  One-shot dev loop:
    1. build-msix.ps1   — dotnet publish + makeappx pack
    2. sign-local.ps1   — signtool sign with the 1Password cert
    3. uninstall.ps1    — remove every previously installed copy
    4. Add-AppxPackage  — install the freshly signed MSIX

  Restart PowerToys / Command Palette after this script finishes so CmdPal
  re-enumerates the AppExtensionCatalog.

.PARAMETER Version
  Three-part version. The .0 revision is appended automatically.

.PARAMETER Platform
  Single architecture for dev iteration. Defaults to the current machine.

.PARAMETER SkipSign
  Skip the 1Password signing step. The resulting MSIX won't install on a
  machine that doesn't trust the unsigned package — only useful if you
  want to inspect the staging output.

.EXAMPLE
  .\scripts\dev-deploy.ps1
  .\scripts\dev-deploy.ps1 -Version 0.2.0
  .\scripts\dev-deploy.ps1 -Platform arm64
#>
[CmdletBinding()]
param(
    [string]$Version = '0.1.0',

    [ValidateSet('x64', 'arm64')]
    [string]$Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq 'ARM64') { 'arm64' } else { 'x64' }),

    [switch]$SkipSign
)

$ErrorActionPreference = 'Stop'

$ScriptDir   = $PSScriptRoot
$RepoRoot    = Split-Path -Parent $ScriptDir
$ProjectDir  = Join-Path $RepoRoot 'HomeAssistantCommandPalette'
$BuildScript = Join-Path $ProjectDir 'build-msix.ps1'
$SignScript  = Join-Path $ScriptDir 'sign-local.ps1'
$UninstallScript = Join-Path $ScriptDir 'uninstall.ps1'

$MsixPath = Join-Path $ProjectDir "bin\Release\msix\HomeAssistantCommandPalette_${Version}.0_${Platform}.msix"

Write-Host "=== dev-deploy: $Version ($Platform) ===" -ForegroundColor Green

# ---------- 1. Build ----------
Write-Host "`n[1/5] Building MSIX..." -ForegroundColor Cyan
& $BuildScript -Version $Version -Platforms $Platform
if ($LASTEXITCODE -ne 0) { throw "build-msix.ps1 failed (exit $LASTEXITCODE)" }
if (-not (Test-Path $MsixPath)) { throw "Expected MSIX not produced: $MsixPath" }

# ---------- 2. Sign ----------
if ($SkipSign) {
    Write-Host "`n[2/5] Skipping signing (-SkipSign)" -ForegroundColor DarkYellow
} else {
    Write-Host "`n[2/5] Signing..." -ForegroundColor Cyan
    & $SignScript -Path $MsixPath
    if ($LASTEXITCODE -ne 0) { throw "sign-local.ps1 failed (exit $LASTEXITCODE)" }
}

# ---------- 3. Stop CmdPal so install can replace files cleanly ----------
# CmdPal hosts the extension via COM; if it's running, it has the .dll
# loaded and Add-AppxPackage races with the lock. Kill the UI plus any
# extension COM servers before reinstalling.
Write-Host "`n[3/5] Stopping Command Palette..." -ForegroundColor Cyan
Get-Process |
    Where-Object { $_.ProcessName -match '^(Microsoft\.CmdPal\.UI|Microsoft\.CmdPal\.Ext\.|HomeAssistantCommandPalette)$' } |
    Stop-Process -Force -ErrorAction SilentlyContinue

# ---------- 4. Uninstall + install ----------
Write-Host "`n[4/5] Reinstalling..." -ForegroundColor Cyan
& $UninstallScript
Add-AppxPackage -Path $MsixPath
$installed = Get-AppxPackage NickNissen.HomeAssistantforCommandPalette
if (-not $installed) { throw "Add-AppxPackage didn't register the package." }

# ---------- 5. Launch Command Palette ----------
Write-Host "`n[5/5] Launching Command Palette..." -ForegroundColor Cyan
Start-Process 'explorer.exe' 'shell:AppsFolder\Microsoft.CommandPalette_8wekyb3d8bbwe!App'

Write-Host "`n=== Deployed ===" -ForegroundColor Green
Write-Host "  $($installed.PackageFullName)  ($($installed.SignatureKind))" -ForegroundColor Yellow
