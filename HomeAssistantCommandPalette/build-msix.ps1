#Requires -Version 7
<#
.SYNOPSIS
  Build (and optionally sign) signed MSIX packages for the Home Assistant
  Command Palette extension.

.DESCRIPTION
  Mirrors the SSMSCommandPalette / TablePlusCommandPalette pipeline.
  CmdPal only enumerates extensions via AppExtensionCatalog, which requires
  a real MSIX install (sideloaded or Store).

  Steps per platform:
    1. dotnet publish with WindowsPackageType=MSIX
    2. Stage publish output, patch AppxManifest (Version, ProcessorArchitecture, Publisher)
    3. Override Microsoft.CommandPalette.Extensions.dll/winmd with the
       vendored copy from tools/cmdpal-sdk (NuGet WinRT IIDs don't match
       what CmdPal expects)
    4. makeappx pack -> .msix
    5. (optional) signtool sign with PFX cert
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet("x64", "arm64")]
    [string[]]$Platforms = @("x64", "arm64"),

    [string]$Publisher = "CN=8E44B6E4-A8D0-49BB-8093-C2544C9DB8A7",

    [string]$CertBase64 = $env:SIGNING_PFX_BASE64,

    [string]$CertPassword = $env:SIGNING_PFX_PASSWORD,

    [string]$CertPath,

    [string]$OutputDir,

    [string]$SourceRevisionId = $env:GITHUB_SHA,

    [switch]$Bundle,

    # Build with DEMO_MODE define so the extension surfaces hard-coded
    # demo entities instead of hitting a real Home Assistant instance.
    # Used to take Microsoft Store screenshots without leaking real
    # entity IDs.
    [switch]$Demo
)

$ErrorActionPreference = "Stop"

$ExtensionName = "HomeAssistantCommandPalette"
$ProjectDir    = $PSScriptRoot
$RepoRoot      = Split-Path -Parent $ProjectDir
$ProjectFile   = Join-Path $ProjectDir "$ExtensionName.csproj"
$VendorSdkDir  = Join-Path $RepoRoot "tools\cmdpal-sdk"

if (-not $OutputDir) { $OutputDir = Join-Path $ProjectDir "bin\Release\msix" }
New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null

Write-Host "=== Building $ExtensionName MSIX ===" -ForegroundColor Green
Write-Host "Version:    $Version" -ForegroundColor Yellow
Write-Host "Platforms:  $($Platforms -join ', ')" -ForegroundColor Yellow
Write-Host "Publisher:  $Publisher" -ForegroundColor Yellow
Write-Host "Output dir: $OutputDir" -ForegroundColor Yellow

# ---------- Resolve Windows SDK tools ----------
$sdkBin = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "^10\." } | Sort-Object Name -Descending | Select-Object -First 1
if (-not $sdkBin) { throw "Windows 10/11 SDK not found under Program Files (x86)\Windows Kits\10\bin" }
$makeappx = Join-Path $sdkBin.FullName "x64\makeappx.exe"
$signtool = Join-Path $sdkBin.FullName "x64\signtool.exe"
foreach ($t in @($makeappx, $signtool)) {
    if (-not (Test-Path $t)) { throw "Required SDK tool not found: $t" }
}
Write-Host "SDK bin:    $($sdkBin.FullName)" -ForegroundColor Yellow

# ---------- Validate vendored SDK ----------
foreach ($f in @("Microsoft.CommandPalette.Extensions.dll", "Microsoft.CommandPalette.Extensions.winmd")) {
    if (-not (Test-Path (Join-Path $VendorSdkDir $f))) {
        throw "Missing vendored SDK file: $f. See tools/cmdpal-sdk/README.md."
    }
}

# ---------- Resolve PFX once if provided ----------
$signingPfx = $null
if ($CertBase64) {
    $signingPfx = Join-Path $env:TEMP "hacp-signing-$([Guid]::NewGuid().ToString('N')).pfx"
    [System.IO.File]::WriteAllBytes($signingPfx, [Convert]::FromBase64String($CertBase64))
} elseif ($CertPath) {
    if (-not (Test-Path $CertPath)) { throw "CertPath not found: $CertPath" }
    $signingPfx = (Resolve-Path $CertPath).Path
}

try {
    foreach ($Platform in $Platforms) {
        Write-Host "`n=== Building $Platform ===" -ForegroundColor Cyan

        # ---------- 1. dotnet publish ----------
        $stagingDir = Join-Path $ProjectDir "bin\Release\stage-$Platform"
        if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
        New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

        $publishArgs = @(
            'publish', $ProjectFile,
            '--configuration', 'Release',
            "-p:RuntimeIdentifier=win-$Platform",
            "-p:PublishDir=$stagingDir\",
            "-p:Version=$Version",
            "-p:AssemblyVersion=$Version.0",
            "-p:FileVersion=$Version.0",
            '-p:WindowsPackageType=MSIX',
            '-p:EnableMsixTooling=true',
            '-p:GenerateAppxPackageOnBuild=false'
        )
        if (-not [string]::IsNullOrWhiteSpace($SourceRevisionId)) {
            $publishArgs += "-p:SourceRevisionId=$SourceRevisionId"
        }
        if ($Demo) {
            $publishArgs += '-p:ExtraDefineConstants=DEMO_MODE'
        }
        & dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $Platform" }

        # ---------- 2. Patch and write AppxManifest.xml into the staging dir ----------
        $manifestSrc = Join-Path $ProjectDir "Package.appxmanifest"
        $manifestDst = Join-Path $stagingDir "AppxManifest.xml"
        $content = Get-Content $manifestSrc -Raw

        $content = $content -replace '(<Identity[^>]*?)Version="[^"]*"', "`$1Version=`"$Version.0`""
        if ($content -match 'ProcessorArchitecture="') {
            $content = $content -replace '(ProcessorArchitecture=")[^"]*"', "`$1$Platform`""
        } else {
            $content = $content -replace '(<Identity\b)', "`$1 ProcessorArchitecture=`"$Platform`""
        }
        $content = $content -replace '(Publisher=")[^"]*"', "`$1$Publisher`""
        $content = $content -replace 'Language="x-generate"', 'Language="en-us"'

        # AppxManifest can only point at concrete asset paths; the scale-200
        # variants are what we actually publish.
        $content = $content -replace 'Square150x150Logo="Assets\\[^"]+"', 'Square150x150Logo="Assets\Square150x150Logo.scale-200.png"'
        $content = $content -replace 'Square44x44Logo="Assets\\[^"]+"', 'Square44x44Logo="Assets\Square44x44Logo.scale-200.png"'
        $content = $content -replace 'Wide310x150Logo="Assets\\[^"]+"', 'Wide310x150Logo="Assets\Wide310x150Logo.scale-200.png"'
        $content = $content -replace 'Square71x71Logo="Assets\\[^"]+"', 'Square71x71Logo="Assets\SmallTile.scale-200.png"'
        $content = $content -replace 'Square310x310Logo="Assets\\[^"]+"', 'Square310x310Logo="Assets\LargeTile.scale-200.png"'
        $content = $content -replace 'SplashScreen Image="Assets\\[^"]+"', 'SplashScreen Image="Assets\SplashScreen.scale-200.png"'
        $content = $content -replace '<Logo>Assets\\[^<]+</Logo>', '<Logo>Assets\StoreLogo.scale-200.png</Logo>'

        [System.IO.File]::WriteAllText($manifestDst, $content, [System.Text.UTF8Encoding]::new($true))

        Remove-Item (Join-Path $stagingDir "Package.appxmanifest") -ErrorAction SilentlyContinue

        # ---------- 3. Override SDK DLL/winmd with vendored copy ----------
        # NuGet 0.9.260303001 IIDs don't match CmdPal's internal 0.9.260326002 —
        # cast to (ICommandProvider) fails silently without this override.
        Copy-Item (Join-Path $VendorSdkDir "Microsoft.CommandPalette.Extensions.dll") `
                  (Join-Path $stagingDir "Microsoft.CommandPalette.Extensions.dll") -Force
        Copy-Item (Join-Path $VendorSdkDir "Microsoft.CommandPalette.Extensions.winmd") `
                  (Join-Path $stagingDir "Microsoft.CommandPalette.Extensions.winmd") -Force

        New-Item -ItemType Directory -Path (Join-Path $stagingDir "Public") -Force | Out-Null

        foreach ($strip in @("build-exe.ps1", "setup-template.iss", "build-msix.ps1", "*.pdb", "app.manifest")) {
            Get-ChildItem $stagingDir -Filter $strip -Recurse -ErrorAction SilentlyContinue |
                Remove-Item -Force -ErrorAction SilentlyContinue
        }
        $appPackagesNested = Join-Path $stagingDir "AppPackages"
        if (Test-Path $appPackagesNested) { Remove-Item $appPackagesNested -Recurse -Force }

        # ---------- 4. makeappx pack ----------
        $msixName = "${ExtensionName}_${Version}.0_${Platform}.msix"
        $msixPath = Join-Path $OutputDir $msixName
        Remove-Item $msixPath -Force -ErrorAction SilentlyContinue

        & $makeappx pack /d $stagingDir /p $msixPath /nv /o
        if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed for $Platform (exit $LASTEXITCODE)" }

        # ---------- 5. Optional signing ----------
        if ($signingPfx) {
            Write-Host "Signing $msixName" -ForegroundColor Yellow
            $signArgs = @('sign', '/fd', 'SHA256', '/f', $signingPfx)
            if ($CertPassword) { $signArgs += @('/p', $CertPassword) }
            $signArgs += $msixPath
            & $signtool @signArgs
            if ($LASTEXITCODE -ne 0) { throw "signtool sign failed for $msixName" }
        } else {
            Write-Host "Skipping signing (no SIGNING_PFX_BASE64 / -CertPath)" -ForegroundColor DarkYellow
        }

        $sizeMB = [math]::Round((Get-Item $msixPath).Length / 1MB, 2)
        Write-Host "Built: $msixName ($sizeMB MB)" -ForegroundColor Green

        Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($Bundle -and $Platforms.Count -ge 2) {
        Write-Host "`n=== Bundling $($Platforms.Count) MSIX into .msixbundle ===" -ForegroundColor Cyan
        $bundleName = "${ExtensionName}_${Version}.0.msixbundle"
        $bundlePath = Join-Path $OutputDir $bundleName
        Remove-Item $bundlePath -Force -ErrorAction SilentlyContinue

        $mappingPath = Join-Path $env:TEMP "bundle-mapping-$([Guid]::NewGuid().ToString('N')).txt"
        $lines = @("[Files]")
        foreach ($Platform in $Platforms) {
            $msixName = "${ExtensionName}_${Version}.0_${Platform}.msix"
            $msixPath = Join-Path $OutputDir $msixName
            if (-not (Test-Path $msixPath)) { throw "Bundle source missing: $msixPath" }
            $lines += "`"$msixPath`" `"$msixName`""
        }
        $lines -join "`r`n" | Set-Content $mappingPath -Encoding utf8 -NoNewline

        try {
            & $makeappx bundle /f $mappingPath /p $bundlePath /bv "$Version.0" /o
            if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed (exit $LASTEXITCODE)" }
        } finally {
            Remove-Item $mappingPath -Force -ErrorAction SilentlyContinue
        }

        if ($signingPfx) {
            Write-Host "Signing $bundleName" -ForegroundColor Yellow
            $signArgs = @('sign', '/fd', 'SHA256', '/f', $signingPfx)
            if ($CertPassword) { $signArgs += @('/p', $CertPassword) }
            $signArgs += $bundlePath
            & $signtool @signArgs
            if ($LASTEXITCODE -ne 0) { throw "signtool sign failed for $bundleName" }
        }

        $sizeMB = [math]::Round((Get-Item $bundlePath).Length / 1MB, 2)
        Write-Host "Bundled: $bundleName ($sizeMB MB)" -ForegroundColor Green
    }
} finally {
    if ($CertBase64 -and $signingPfx -and (Test-Path $signingPfx)) {
        Remove-Item $signingPfx -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`n=== Done ===" -ForegroundColor Green
Get-ChildItem $OutputDir | Where-Object { $_.Name -match '\.msix(bundle)?$' } | Select-Object Name, Length, LastWriteTime
