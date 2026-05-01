# Vendored Command Palette SDK

This directory holds a copy of `Microsoft.CommandPalette.Extensions.dll` /
`.winmd` extracted from a specific Microsoft Command Palette install.

## Why it's vendored

Microsoft ships a newer SDK build inside the Command Palette MSIX than the
latest version published to NuGet (`Microsoft.CommandPalette.Extensions`).
The WinRT IIDs differ between versions, so an extension built against the
NuGet package fails CmdPal's `(ICommandProvider)result` cast at activation —
silently — and never appears in the palette. See PowerToys#47076 / #38273.

The build script (`TablePlusCommandPalette/build-msix.ps1`) overrides the
NuGet-shipped DLL/winmd with the files from this directory before packing
the MSIX, so the runtime IIDs match what CmdPal expects.

## Source version

Extracted from:

- Package: `Microsoft.CommandPalette_0.9.10852.0_x64__8wekyb3d8bbwe`
- File version: `0.9.2603.26002`

## Updating

When CmdPal updates and our extension stops loading after a CmdPal upgrade,
re-extract the DLL from the new install:

```powershell
$src = (Get-AppxPackage Microsoft.CommandPalette).InstallLocation
Copy-Item "$src\Microsoft.CommandPalette.Extensions.dll" tools\cmdpal-sdk\ -Force
Copy-Item "$src\Microsoft.CommandPalette.Extensions.winmd" tools\cmdpal-sdk\ -Force
```

Update the version note above. Commit. Bump the extension version and ship.

This whole song-and-dance can be removed once Microsoft publishes the matching
SDK version to NuGet.
