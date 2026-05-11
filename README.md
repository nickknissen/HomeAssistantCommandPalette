# Home Assistant Command Palette

A PowerToys Command Palette extension that lets you search and control your
Home Assistant entities from CmdPal.

> Inspired by the [Raycast Home Assistant extension](https://github.com/raycast/extensions/tree/main/extensions/homeassistant)
> by tonka3000.

## Setup

1. In Home Assistant, go to **Profile → Security → Long-Lived Access Tokens**
   and create a token. Copy it somewhere safe; HA only shows it once.
2. Open PowerToys Command Palette, type `Home Assistant`, and open the
   extension's Settings page.
3. Set:
   - **Home Assistant URL** — e.g. `http://homeassistant.local:8123`
   - **Long-Lived Access Token** — paste from step 1
   - **Ignore TLS certificate errors** — only for self-signed certs on a LAN

Settings are stored at:

```
%LOCALAPPDATA%\HomeAssistantCommandPalette\settings.json
```

### Token storage

The Long-Lived Access Token is stored in plain text in that local
`settings.json` file. This keeps setup simple and matches the current CmdPal
settings toolkit, which does not provide a password/secret setting. The file is
under `%LOCALAPPDATA%`, so it is scoped to the signed-in Windows user, but any
process already running as that user could read it.

If your environment requires stronger at-rest protection, create a dedicated HA
token with only the access you are comfortable exposing, rotate it regularly,
and delete the token from the extension settings when you stop using it. Moving
the token to Windows Credential Manager remains a possible future hardening
step, but the current project decision is to keep the token in local settings
and document the trade-off clearly.

## How it works

- `GET {url}/api/states` is called every time the list is opened (with a
  3-second cache to keep typing snappy).
- The default action calls `POST {url}/api/services/{domain}/{service}` with
  `{"entity_id": "..."}` based on the domain:

  | Domain                                               | Default action     |
  | ---------------------------------------------------- | ------------------ |
  | `light`, `switch`, `fan`, `input_boolean`, `automation`, `group`, `cover`, `media_player` | `toggle`           |
  | `scene`                                              | `turn_on`          |
  | `script`                                             | `turn_on`          |
  | `button`, `input_button`                             | `press`            |
  | other                                                | open in dashboard  |

## Requirements

- **Windows 10 2004+** or **Windows 11**
- Microsoft PowerToys with **Command Palette** support
- Home Assistant **2024.04+** (any modern version exposes the REST API used)

## Project structure

```text
HomeAssistantCommandPalette/
├─ Commands/              # CallServiceCommand, OpenDashboardCommand
├─ Models/                # HaEntity, HaQueryResult
├─ Pages/                 # EntityListPage (filters by domain set)
├─ Services/              # HaSettings (JsonSettingsManager), HaApiClient (REST)
└─ Assets/                # HA-branded MSIX icons
```

The provider in `HomeAssistantCommandPaletteCommandsProvider.cs` declares
the per-domain top-level commands as `DomainPage` records — add a new line
there to expose another domain.

## Notes

- The starter icons under `Assets/` are placeholders copied from the SSMS
  extension. Replace them with Home Assistant–themed art before publishing.
- `Package.appxmanifest` declares `internetClient` and `privateNetworkClientServer`
  capabilities — required for HTTP calls to the HA instance.
- See [Token storage](#token-storage) for the current settings.json vs.
  Windows Credential Manager decision and threat model.

## Roadmap

Open work is tracked in [GitHub Issues](https://github.com/nickknissen/HomeAssistantCommandPalette/issues).
Phase 1 (minimum viable) and most of Phase 2 (per-domain actions) are shipped;
remaining items cover Phase 3 (notifications, weather forecast, history charts,
multi-instance, etc.) and pre-publish polish.

## License

MIT — see [LICENSE](LICENSE).

## Disclaimer

Independent extension for Microsoft PowerToys; not affiliated with or endorsed
by Microsoft, Nabu Casa, or the Home Assistant project.
