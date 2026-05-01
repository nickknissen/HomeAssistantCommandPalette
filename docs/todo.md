# TODO + Roadmap

Single source of truth — strategic phases at the top, granular working
list at the bottom. Drop items off the list once shipped; git log is
authoritative for what's done.

Inspiration: the
[Raycast Home Assistant extension](https://github.com/raycast/extensions/tree/main/extensions/homeassistant)
ships ~40 commands. We're recreating the high-value ones for CmdPal.

---

## Phase 1 — Minimum viable ✅ shipped

- [x] `JsonSettingsManager`-backed settings: HA URL + Long-Lived Access
      Token + ignore-cert toggle, with deeplink from "not configured" /
      "unauthorized" / "invalid URL" error rows
- [x] Top-level command per domain (All Entities, Lights, Switches,
      Covers, Fans, Media Players, Scenes, Scripts, Automations, Sensors,
      Binary Sensors, Climate, Buttons, Persons, Zones, Cameras, Vacuums,
      Helpers, Updates, Weather) plus a no-view **Open Dashboard**
- [x] Single `EntityListPage` parameterized by a domain filter set + page
      icon + stable `Id` (so CmdPal can persist user pinned/disabled state)
- [x] Per-domain default action (toggle / turn_on / press / open dashboard)
- [x] Context menu: Turn on, Turn off, Open in dashboard, Copy entity ID
- [x] State-aware tags (ON / OFF) and domain tag (hidden on single-domain
      pages, kept on multi-domain pages)
- [x] Subtitle shows area (room) name from `area_name(entity_id)`
      template, cached 5 min — matches Raycast's WebSocket area resolution
- [x] 3-second list cache; auto-refresh after service calls (250 ms delay
      to let HA propagate state, then `RaiseItemsChanged`)
- [x] Toast notifications on failure (silent on success — list refresh
      is the confirmation)
- [x] Demo mode (`DEMO_MODE` define) for Store screenshots
- [x] HA-branded MSIX assets generated from official logo files
- [x] Vendored CmdPal SDK in `tools/cmdpal-sdk/` to work around the
      NuGet/runtime WinRT IID mismatch

---

## Phase 2 — Domain-specific actions

Phase 1 ships per-domain *list pages*. Phase 2 layers on the rich,
domain-specific actions Raycast supports. **Status: mostly shipped**;
remaining domains live in the granular list below.

- [x] **Lights** — brightness presets (25 / 50 / 75 / 100 %) nested
      under "Set brightness…", state-tinted icon (yellow on / blue off /
      grey unavailable), `lightbulb-group.svg` for groups
- [x] **Covers** — Open / Close / Stop, position presets nested under
      "Set position…" (gated by `SET_POSITION` in supported_features),
      state icons window-open / window-closed / arrow-up / arrow-down
- [x] **Media players** — Play/Pause, Play, Pause, Stop, Next, Previous,
      Volume up/down, Mute (label flips with current `is_volume_muted`),
      "Set volume…" presets gated by VOLUME_SET; state-tinted
      `cast-connected.svg`
- [x] **Climate** — Increase / Decrease to relative target, "Set
      temperature…" presets, "Set HVAC mode…" / "Set fan mode…"
      submenus dynamically built from `hvac_modes` / `fan_modes`
- [x] **Vacuums** — Start (state-aware primary: Pause when cleaning),
      Stop, Return to base, Locate, Clean spot, "Set fan speed…" submenu
      from `fan_speed_list`; all gated by `supported_features` bits
- [x] **Automations** — Trigger context item alongside Toggle / Turn on
      / Turn off; state-tinted `robot.svg`
- [x] **Fans** — state-tinted icon, "Set speed…" presets (25/50/75/100),
      Speed up / Speed down stepped from `percentage_step`, gated by
      SET_SPEED bit; details rows for percentage / preset_mode /
      oscillating / direction
- [x] **Updates** — Install (with backup when supported, gated by INSTALL
      bit + state="on" + not in_progress), Skip, Open release notes;
      details rows for title / installed / latest / progress / auto_update;
      ON/OFF tag (ON = update available)
- [ ] **Scripts** — input form for scripts that take `fields` parameters
      (needs CmdPal Form support — defer if blocked)
- [x] **Cameras** — snapshot via `/api/camera_proxy/{entity_id}` written
      to a temp file and rendered as `Details.HeroImage`; per-camera 5 s
      cache in `HaApiClient` so a list render with N cameras issues at
      most N HTTP gets

### Cameras — additional actions to consider

- [ ] Periodic auto-refresh (call `RaiseItemsChanged` on a 3 s timer
      while the camera page is visible — currently snapshots only update
      when CmdPal re-renders the list naturally)
- [ ] Cleanup pass for stale temp snapshots on extension startup
- [x] **Persons** — Location (lat/lon), GPS accuracy, Source tracker
      rows; Open in Google Maps + Copy user ID context actions
      (picture/HeroImage deferred — needs auth-aware image fetching)
- [ ] **Weather** — current conditions + forecast (subscribe via WS later)
- [x] **Helpers — per-type actions** (non-form-blocked):
      - `input_boolean` — toggle (already covered)
      - `input_button` — press (already covered via Buttons mapping)
      - `input_select` — "Select option…" submenu of options ≠ current state
      - `input_number` — Increase / Decrease, gated by `min/max/step`
      - `timer` — state-aware Start/Pause primary; Restart/Pause/Cancel/Finish
        in context menu when `editable=true`; details show duration/remaining/finishes_at
      - `counter` — Increment primary; Decrement / Reset in context menu
- [ ] **Helpers — form-blocked**: `input_text` (set text), `input_datetime`
      (set date/time), `input_number` set-to-arbitrary-value. All blocked
      on CmdPal Form input support.

### Lights — additional actions to consider

- [ ] RGB color picker (preset palette: red, green, blue, warm white, …)
- [ ] Min / Max color temp Kelvin in details

### Covers — additional actions to consider

- [x] Tilt position presets (gated by SET_TILT_POSITION bit)
- [x] `working` (currently moving) flag in details

### Media players — additional actions to consider

- [ ] Position / Duration formatted `MM:SS / MM:SS` (parse
      `media_position` + `media_position_updated_at` to advance position
      client-side)
- [x] Shuffle on/off (gated by SHUFFLE_SET, label flips with current `shuffle`)
- [x] Repeat (off / one / all) (gated by REPEAT_SET)
- [x] Sound mode + sound mode submenu when `sound_mode_list` is present
- [x] Source select submenu from `source_list`

### Climate — additional actions to consider

- [x] Min / Max temp range row in details
- [ ] Target low / high (heat_cool dual setpoint)
- [x] Swing mode + submenu when `swing_modes` is present

### Vacuums — additional actions to consider

- [ ] Cleaning time formatted as duration in details
- [ ] Last error message in details

### New filtered top-level pages (Raycast parity)

- [x] **Batteries** — sensors with `device_class=battery`, sorted ascending
- [x] **Doors** — `binary_sensor` + `device_class=door`
- [x] **Windows** — `binary_sensor` + `device_class=window`
- [x] **Motions** — `binary_sensor` + `device_class=motion` /
      `device_class=occupancy`
- [ ] **All Entities with Attributes** — full attribute drill-down
- [ ] **Custom Entities** — user-defined include / exclude glob
      (needs per-command settings or a single global filter list)
- [x] **Connection Check** — pings `/api/config` and reports HA version,
      location, time zone, run state, latency, configured URL, token
      length (never the value), TLS-ignore flag; offers a deeplink to
      settings on misconfiguration

---

## Phase 3 — Live state + advanced features

Real-time updates and richer features that need WebSocket or specialized
APIs.

- [ ] **WebSocket connection** to `ws(s)://{url}/api/websocket` —
      subscribe to `state_changed` and push updates instead of polling.
      Big UX win because list items reflect state changes within ~1 s,
      and removes the area template round-trip.
- [ ] **Assist conversation** — `POST /api/conversation/process`, render
      the response as a CmdPal page
- [ ] **Notifications** — pull persistent notifications via WS
      `persistent_notification/get` and surface them as a CmdPal list
      page; consider a status command that shows a count
- [ ] **Weather** — render `weather.*` entity forecast (current + hourly
      + daily). Daily/hourly come from the WS `weather/subscribe_forecast`.
- [ ] **Calendar** — `GET /api/calendars` for the list, then
      `GET /api/calendars/{entity_id}?start=...&end=...` for events
- [ ] **History / sensor charts** — `GET /api/history/period` for sensor
      values; render as ASCII sparklines in the entity Details pane
- [ ] **Run Service** — generic free-form service caller (pick domain →
      pick service → fill in JSON data → call). Useful for power users.
      Equivalent to Raycast's `services` browse-mode command. (The separate
      Raycast `runService` deeplink is listed under Out of scope below.)
- [x] **Show Entity IDs setting** — toggle that swaps the friendly-name
      subtitle for the raw `entity_id` (mirrors Raycast's `showEntityId`).
      Useful for power users wiring up automations.
- [ ] **Multi-instance support** — one default instance plus alternates,
      switchable via a top-level command. Add an "Instances" array in
      `JsonSettingsManager`.
- [ ] **Internal/external URL fallback** with home-network detection
      (Wi-Fi SSID via `WlanQueryInterface`, or ping check)
- [ ] **Custom HTTP headers** for users behind Cloudflare Access etc.
- [ ] **Configurable camera refresh interval** — expose the auto-refresh
      cadence as a setting (Raycast `camerarefreshinterval`, default
      3000 ms, 0 disables). Pairs with the periodic auto-refresh item
      under Cameras above.

---

## Out of scope (probably never)

- Mobile companion-app deep links (Raycast `preferredapp=companion`) —
  no Windows companion app exists
- macOS-specific Wi-Fi SSID detection (we'd use Windows `WlanQueryInterface`)
- The Raycast `runService` quicklink/deeplink machinery — CmdPal's
  invocation model is different; we'd want a CmdPal-native equivalent
- Raycast menu-bar commands (Notifications Menu, Weather Menu, Media
  Player Menu, Lights Menu, Covers Menu, Batteries Menu, Entities Menu,
  Entity Menu 1/2/3, Calendar Menu) — CmdPal has no menu-bar surface
- Raycast AI tools (`get-entities`, `get-attributes`, `run-service`) —
  these expose HA to Raycast AI; CmdPal has no equivalent host

---

## Pre-publish polish

- [ ] Decide token storage: keep in `settings.json` vs. Windows Credential Manager. Document the trade-off in README.
- [ ] Microsoft Store listing assets (use the `cmdpal-publishing` skill when the extension is feature-complete)
- [ ] Replace `dev-deploy.ps1`'s 1Password dependency with an optional self-signed cert path so first-time contributors can deploy without our shared cert
- [ ] Privacy policy page on s-nissen.dk/privacy
- [ ] Demo screenshots with `DEMO_MODE` build

---

## Known issues / minor

- [ ] CmdPal sometimes needs a manual restart to pick up new builds even after `dev-deploy.ps1` kills + relaunches; investigate AppExtension catalog refresh behavior
- [ ] arm64 build path — `dev-deploy.ps1` defaults to current arch; confirm `-Platform arm64` works end-to-end
- [ ] When `area_name()` template returns an empty result, the `LastAreaCount` diagnostic stays at 0 forever (cached). Consider retrying once per minute when count is 0.

---

## Maintenance reminders

- [ ] Re-extract vendored CmdPal SDK (`tools/cmdpal-sdk/*`) when Microsoft ships a CmdPal update — see [`tools/cmdpal-sdk/README.md`](../tools/cmdpal-sdk/README.md).
      Symptom: extension doesn't load after a CmdPal upgrade.
- [ ] Bump `Microsoft.CommandPalette.Extensions` NuGet version when Microsoft publishes an SDK that matches the runtime IIDs (lets us drop the vendored DLL workaround entirely).

---

## Notes for whoever picks this up

- `Microsoft.CommandPalette.Extensions.Toolkit` exposes
  `JsonSettingsManager`, `TextSetting`, `ToggleSetting`,
  `ChoiceSetSetting`, and `Setting<T>`. No password-style setting today
  — use `TextSetting` with a note in the UI description if it matters.
- `CommandResult` returns `Dismiss`, `KeepOpen`, `GoHome`, `GoBack`,
  `GoToPage`, `ShowToast`, `Confirm`. For service calls we want
  `KeepOpen` so users can chain actions.
- Each top-level `CommandItem` is searched by **Title + Subtitle**. There's no dedicated keyword/alias property — bake aliases into the Subtitle string if you need them.
- Give each top-level command a stable `Id` so CmdPal can remember user-pinned/disabled state across reinstalls (we use `ha.<domain>`).
- `IconInfo` has no runtime tint. State-tinted icons (yellow on / blue off / grey unavailable) are pre-baked from a single source SVG; the generator script lives inline in the `Assets/Icons/` workflow.
