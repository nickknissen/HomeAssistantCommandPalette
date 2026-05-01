# Home Assistant Command Palette

A PowerToys Command Palette extension that lets you search and control your
Home Assistant entities from CmdPal.

> Inspired by the [Raycast Home Assistant extension](https://github.com/raycast/extensions/tree/main/extensions/homeassistant)
> by tonka3000.

## Status

**Phase 1 — minimum viable.** See [`ha-plan.md`](ha-plan.md) for the roadmap.

## Phase 1 features

- Settings: Home Assistant URL + Long-Lived Access Token (+ ignore-cert toggle)
- One top-level command per domain, modeled on the Raycast extension:
  All Entities, Lights, Switches, Covers, Fans, Media Players, Scenes,
  Scripts, Automations, Sensors, Binary Sensors, Climate, Buttons, Persons,
  Zones, Cameras, Vacuums, Helpers, Updates, Weather, plus an
  **Open Dashboard** quick action
- Each domain page lists matching entities from `GET /api/states`,
  searchable via CmdPal
- Default action toggles / activates / runs the entity (per-domain)
- Context menu: Turn on, Turn off, Open in dashboard, Copy entity ID
- Demo mode (`DEMO_MODE` define) for Microsoft Store screenshots

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
- The token is stored in plain text in `settings.json` (CmdPal's toolkit has
  no password setting today). The file is in `%LOCALAPPDATA%`, which is
  per-user, but if your threat model requires more, pull the token from
  Windows Credential Manager instead.

## Roadmap

See [`ha-plan.md`](ha-plan.md) for Phase 2 (per-domain pages) and Phase 3
(WebSocket live state, Assist, calendar/weather, etc.).

## License

MIT — see [LICENSE](LICENSE).

## Disclaimer

Independent extension for Microsoft PowerToys; not affiliated with or endorsed
by Microsoft, Nabu Casa, or the Home Assistant project.
