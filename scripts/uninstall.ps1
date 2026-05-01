<#
.SYNOPSIS
  Remove all installed copies of the Home Assistant Command Palette extension.

.DESCRIPTION
  Finds every AppX package whose Name matches the extension's Identity Name
  and removes them. Covers sideloaded MSIX, Store installs, and dev-registered
  packages.

.EXAMPLE
  .\scripts\uninstall.ps1

.EXAMPLE
  .\scripts\uninstall.ps1 -AllUsers
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [switch]$AllUsers,

    [string]$PackageName = 'NickNissen.HomeAssistantforCommandPalette'
)

$ErrorActionPreference = 'Stop'

$getArgs = @{ Name = $PackageName }
if ($AllUsers) { $getArgs['AllUsers'] = $true }

$packages = @(Get-AppxPackage @getArgs)

if (-not $packages) {
    Write-Host "No installed packages found matching '$PackageName'." -ForegroundColor Yellow
    return
}

Write-Host "Found $($packages.Count) package(s):" -ForegroundColor Cyan
$packages | ForEach-Object {
    Write-Host ("  - {0}  ({1})" -f $_.PackageFullName, $_.SignatureKind)
}

$failed = 0
foreach ($pkg in $packages) {
    if (-not $PSCmdlet.ShouldProcess($pkg.PackageFullName, 'Remove-AppxPackage')) { continue }
    Write-Host "Removing $($pkg.PackageFullName)..." -ForegroundColor Yellow
    try {
        if ($AllUsers) {
            Remove-AppxPackage -Package $pkg.PackageFullName -AllUsers -ErrorAction Stop
        } else {
            Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction Stop
        }
    } catch {
        $failed++
        Write-Warning "Failed to remove $($pkg.PackageFullName): $($_.Exception.Message)"
    }
}

if ($AllUsers) {
    $provisioned = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -eq $PackageName }
    foreach ($prov in $provisioned) {
        if (-not $PSCmdlet.ShouldProcess($prov.PackageName, 'Remove-AppxProvisionedPackage')) { continue }
        Write-Host "Removing provisioned $($prov.PackageName)..." -ForegroundColor Yellow
        try {
            Remove-AppxProvisionedPackage -Online -PackageName $prov.PackageName -ErrorAction Stop | Out-Null
        } catch {
            $failed++
            Write-Warning "Failed to remove provisioned $($prov.PackageName): $($_.Exception.Message)"
        }
    }
}

if ($failed -gt 0) {
    throw "$failed package(s) failed to uninstall."
}

Write-Host "Done." -ForegroundColor Green
