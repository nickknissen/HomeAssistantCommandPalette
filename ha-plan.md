# Home Assistant CmdPal — Roadmap

This is the multi-phase plan. Phase 1 is the current scaffold; Phase 2 and
Phase 3 are tracked here so we can pick up additional features later
without re-deriving scope.

Inspiration: the
[Raycast Home Assistant extension](https://github.com/raycast/extensions/tree/main/extensions/homeassistant)
ships ~40 commands. We're recreating the high-value ones for CmdPal.

---

## Phase 1 — Minimum viable ✅ (shipped locally)

Goal: prove the bones — settings, REST, list, toggle, dashboard, plus the
Raycast-style per-domain top-level commands.

- [x] `JsonSettingsManager`-backed settings: HA URL + Long-Lived Access Token + ignore-cert toggle
- [x] Top-level command per domain (All Entities, Lights, Switches, Covers,
      Fans, Media Players, Scenes, Scripts, Automations, Sensors, Binary
      Sensors, Climate, Buttons, Persons, Zones, Cameras, Vacuums, Helpers,
      Updates, Weather) plus a no-view **Open Dashboard**
- [x] Single `EntityListPage` parameterized by a domain filter set
- [x] Per-domain default action (toggle / turn_on / press / open dashboard)
- [x] Context menu: Turn on, Turn off, Open in dashboard, Copy entity ID
- [x] State-aware tags (ON / OFF) and domain tag
- [x] 3-second list cache to keep search snappy
- [x] Toast notifications on success / failure
- [x] Demo mode (`DEMO_MODE` define) for Store screenshots
- [x] HA-branded MSIX assets generated from official logo files
- [ ] **TODO before publishing:** decide whether to keep token in
      `settings.json` or move to Windows Credential Manager

---

## Phase 2 — Domain-specific actions

Phase 1 ships per-domain *list pages*. Phase 2 layers on the rich,
domain-specific actions Raycast supports.

- [ ] **Lights** — brightness slider (call `light.turn_on` with
      `brightness_pct`), color picker for RGB lights
- [ ] **Covers** — open / close / stop / set position
- [ ] **Media players** — play / pause / next / previous, volume up/down
- [ ] **Scripts** — input fields for scripts that take parameters
- [ ] **Climate** — set HVAC mode (heat / cool / off), set target temp,
      fan mode
- [ ] **Automations** — trigger (in addition to toggle)
- [ ] **Sensors** — filter by `device_class`, sort by value
- [ ] **Batteries** page — sensors with `device_class=battery`, sorted by
      level ascending so low ones float to the top
- [ ] **Doors / Windows / Motions** — derived pages from `binary_sensor`
      filtered by `device_class` (door, window, motion, occupancy)
- [ ] **All Entities with Attributes** — full attribute drill-down view
      (Raycast `attributes`)
- [ ] **Custom Entities** — user-defined include / exclude glob (Raycast
      `customentities`); needs per-command settings if/when CmdPal supports
      them, or a single global filter list in our `JsonSettingsManager`

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
