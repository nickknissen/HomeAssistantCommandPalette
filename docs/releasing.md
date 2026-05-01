# Releasing

A new release is cut by triggering the `Release Extension` GitHub Actions
workflow. It builds signed x64 + ARM64 MSIX via `build-msix.ps1`, combines
them into a single `.msixbundle`, and creates a GitHub Release with the
bundle and individual MSIX files attached.

```powershell
gh workflow run release.yml --repo nickknissen/HomeAssistantCommandPalette `
  -f version=1.0.0 `
  -f release_notes="One-line summary of what changed in this release."
```

When the run finishes:

1. The release appears at
   `https://github.com/nickknissen/HomeAssistantCommandPalette/releases/tag/<version>`.
2. To push the same artifact to the Microsoft Store: download
   `HomeAssistantCommandPalette_<version>.0.msixbundle` from the release page
   (or `gh release download <version> --pattern *.msixbundle`), then upload
   it in [Partner Center](https://partner.microsoft.com/dashboard/home)
   under your app's **Packages** section. The Store re-signs the package
   during ingestion regardless of the build-time signature.

## Required secrets

The release workflow expects two GitHub repository secrets:

- `SIGNING_PFX_BASE64` — base64-encoded PFX containing the code-signing
  certificate. The cert subject must match the `Publisher` declared in
  `Package.appxmanifest` (`CN=8E44B6E4-A8D0-49BB-8093-C2544C9DB8A7`).
- `SIGNING_PFX_PASSWORD` — PFX password.
