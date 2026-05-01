<#
.SYNOPSIS
  Sign an MSIX / MSIXBUNDLE locally using the code-signing cert stored in 1Password.

.DESCRIPTION
  Pulls the PFX and password from a 1Password item, writes the PFX to a temp
  file, signs the given paths with signtool, and scrubs the temp file on exit.

  Expected 1Password item layout (override with -OpItem):
    op://Private/CmdPal Signing Cert/password           (concealed field)
    op://Private/CmdPal Signing Cert/signing/pfx        (file attachment named "signing.pfx")

.EXAMPLE
  .\scripts\sign-local.ps1 -Path .\HomeAssistantCommandPalette\bin\Release\msix\*.msix
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$Path,

    [string]$OpItem = 'op://Private/CmdPal Signing Cert',

    [string]$OpAccount = 'my.1password.com'
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command op -ErrorAction SilentlyContinue)) {
    throw "1Password CLI 'op' not found on PATH. Install via: winget install AgileBits.1Password.CLI"
}

$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $signtool) {
    throw "signtool.exe not found under Windows Kits 10. Install the Windows SDK."
}

$files = $Path | ForEach-Object { Get-Item $_ } | Sort-Object FullName -Unique
if (-not $files) { throw "No files matched: $($Path -join ', ')" }

$tempPfx = Join-Path $env:TEMP ("cmdpal-signing-{0}.pfx" -f ([guid]::NewGuid().ToString('N')))

try {
    Write-Host "Fetching cert from 1Password ($OpItem)..."
    & op read --account $OpAccount "$OpItem/signing/pfx" --out-file $tempPfx | Out-Null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $tempPfx)) {
        throw "Failed to read PFX attachment from 1Password."
    }

    $password = & op read --account $OpAccount "$OpItem/password"
    if ($LASTEXITCODE -ne 0 -or -not $password) {
        throw "Failed to read password field from 1Password."
    }

    foreach ($file in $files) {
        Write-Host "Signing $($file.FullName)..."
        & $signtool.FullName sign /fd SHA256 /f $tempPfx /p $password $file.FullName
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE for $($file.FullName)"
        }
    }

    Write-Host "Done. Signed $($files.Count) file(s)."
}
finally {
    if (Test-Path $tempPfx) {
        try {
            $len = (Get-Item $tempPfx).Length
            [System.IO.File]::WriteAllBytes($tempPfx, [byte[]]::new([int]$len))
        } catch { }
        Remove-Item $tempPfx -Force -ErrorAction SilentlyContinue
    }
}
