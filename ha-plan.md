# Home Assistant CmdPal — Roadmap

This is the multi-phase plan. Phase 1 is the current scaffold; Phase 2 and
Phase 3 are tracked here so we can pick up additional features later
without re-deriving scope.

Inspiration: the
[Raycast Home Assistant extension](https://github.com/raycast/extensions/tree/main/extensions/homeassistant)
ships ~40 commands. We're recreating the high-value ones for CmdPal.

---

## Phase 1 — Minimum viable ✅ (scaffolded)

Goal: prove the bones — settings, REST, list, toggle, dashboard.

- [x] `JsonSettingsManager`-backed settings: HA URL + Long-Lived Access Token + ignore-cert toggle
- [x] Single top-level `Home Assistant` command (alias `ha` via Subtitle)
- [x] `HomeAssistantPage` listing all entities from `GET /api/states`
- [x] Per-domain default action (toggle / turn_on / press / open dashboard)
- [x] Context menu: Turn on, Turn off, Open in dashboard, Copy entity ID
- [x] State-aware tags (ON / OFF) and domain tag
- [x] 3-second list cache to keep search snappy
- [x] Toast notifications on success / failure
- [x] Demo mode (`DEMO_MODE` define) for Store screenshots
- [ ] **TODO before publishing:** swap placeholder icons in `Assets/` for
      Home Assistant–themed art
- [ ] **TODO before publishing:** decide whether to keep token in
      `settings.json` or move to Windows Credential Manager

---

## Phase 2 — Per-domain pages

Goal: surface dedicated commands so users can jump straight to a domain.
Each page is its own top-level `CommandItem` in `TopLevelCommands()`.

Pattern: each page filters `GET /api/states` to its domain, with
domain-specific actions in MoreCommands.

- [ ] **Lights** — list, toggle, **brightness slider** (call `light.turn_on`
      with `brightness_pct`), **color picker** for RGB lights
- [ ] **Covers** — open / close / stop / set position
- [ ] **Media players** — play / pause / next / previous, volume up/down
- [ ] **Scenes** — one-tap activate
- [ ] **Scripts** — one-tap run, with optional input fields for scripts that
      take parameters (later)
- [ ] **Switches** — toggle-only list (no brightness clutter)
- [ ] **Climate** — set HVAC mode (heat / cool / off), set target temp
- [ ] **Automations** — toggle, trigger
- [ ] **Sensors** — read-only list with values + units (filterable by device class)
- [ ] **Batteries** — sensors with `device_class=battery`, sorted by level
      ascending so low ones float to the top
- [ ] **Persons / Zones** — read-only "who's home" view
- [ ] **Custom Entities** — user-defined include / exclude glob (mirrors the
      Raycast `customentities` command)

Open question for Phase 2:
- Should these be **separate top-level commands** (like Raycast does) or
  sub-pages reachable from the main `Home Assistant` page? Top-level is more
  discoverable; sub-pages are tidier. **Recommendation:** top-level, with
  `disabledByDefault` for the niche ones (e.g. Persons, Zones) so the
  command list isn't overwhelming out of the box.

---

## Phase 3 — Live state + advanced features

Goal: real-time updates and richer features that need WebSocket or
specialized APIs.

- [ ] **WebSocket connection** to `ws(s)://{url}/api/websocket` — subscribe
      to `state_changed` and push updates instead of polling. Big UX win
      because list items reflect state changes within ~1s.
- [ ] **Assist conversation** — `POST /api/conversation/process`, render the
      response as a CmdPal page (mirrors Raycast `assist.tsx`)
- [ ] **Notifications** — pull persistent notifications via WS
      `persistent_notification/get` and surface them as a CmdPal list page;
      consider StatusBar-style command that shows a count
- [ ] **Weather** — render `weather.*` entity forecast (current + hourly +
      daily). Daily/hourly come from the WS `weather/subscribe_forecast`.
- [ ] **Calendar** — `GET /api/calendars` for the list, then
      `GET /api/calendars/{entity_id}?start=...&end=...` for events
- [ ] **History / sensor charts** — `GET /api/history/period` for sensor
      values; render as ASCII sparklines in the entity Details pane (or
      open a dialog with a chart image — needs more design)
- [ ] **Cameras** — show snapshot via `GET /api/camera_proxy/{entity_id}` in
      a Details image; consider auto-refresh every 3s like Raycast
- [ ] **Vacuums** — start / stop / return-to-base / locate
- [ ] **Run Service** — generic free-form service caller (pick domain →
      pick service → fill in JSON data → call). Useful for power users.
- [ ] **Multi-instance support** — one default instance plus alternates,
      switchable via a top-level command. Keep settings simple in Phase 1
      and add an "Instances" array in Phase 3.
- [ ] **Internal/external URL fallback** with home-network detection (Wi-Fi
      SSID match or ping check) — mirrors Raycast `instanceInternal` flow
- [ ] **Custom HTTP headers** for users behind Cloudflare Access etc.

---

## Out of scope (probably never)

- Mobile companion-app deep links (Raycast `preferredapp=companion`) — the
  Windows companion app doesn't exist
- macOS-specific Wi-Fi SSID detection (we'd use the Windows
  `WlanQueryInterface` API instead)
- The Raycast `runService` quicklink/deeplink machinery — CmdPal's
  invocation model is different; we'd want a CmdPal-native equivalent

---

## Notes for whoever picks this up

- `Microsoft.CommandPalette.Extensions.Toolkit` exposes `JsonSettingsManager`,
  `TextSetting`, `ToggleSetting`, `ChoiceSetSetting`, and `Setting<T>`. No
  password-style setting today — use `TextSetting` with a note in the UI
  description if it matters.
- `CommandResult` returns `Dismiss`, `KeepOpen`, `GoHome`, `GoBack`,
  `GoToPage`, `ShowToast`, `Confirm`. For service calls we want
  `ShowToast(KeepOpen)` so users can chain actions.
- Each `CommandItem` is searched by **Title + Subtitle**. There's no
  dedicated keyword/alias property today — bake aliases into the Subtitle
  string if you need them.
- For per-domain top-level commands, give each a stable `Id` so CmdPal can
  remember user-pinned/disabled state across reinstalls.
