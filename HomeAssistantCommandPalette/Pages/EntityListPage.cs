using System;
using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Generic list page that shows entities, optionally filtered to a fixed
/// set of domains. The single class backs every per-domain top-level
/// command (Lights, Covers, Scenes, ...) plus the unfiltered "All Entities".
/// </summary>
internal sealed partial class EntityListPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly HaApiClient _client;
    private readonly HashSet<string>? _domains;

    public EntityListPage(
        HaSettings settings,
        HaApiClient client,
        string title,
        string id,
        IReadOnlyCollection<string>? domains = null,
        IconInfo? icon = null)
    {
        _settings = settings;
        _client = client;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);

        Icon = icon ?? Icons.App;
        Title = title;
        Name = "Open";
        Id = id;
        ShowDetails = true;
        PlaceholderText = $"Search {title.ToLowerInvariant()}";
    }

    public override IListItem[] GetItems()
    {
        var result = _client.GetStates();
        if (result.HasError)
        {
            // For configuration errors, make the error item itself navigate
            // to the settings page so the user can fix it in one click.
            var openSettings = (ICommand)_settings.Settings.SettingsPage;
            ICommand errorCommand = result.ErrorKind switch
            {
                HaErrorKind.NotConfigured or HaErrorKind.Unauthorized or HaErrorKind.InvalidUrl => openSettings,
                _ => new NoOpCommand(),
            };
            var subtitle = result.ErrorKind switch
            {
                HaErrorKind.NotConfigured => "Press Enter to open settings and add your URL + access token.",
                HaErrorKind.Unauthorized => "Press Enter to open settings and update your access token.",
                HaErrorKind.InvalidUrl => "Press Enter to open settings and fix the URL.",
                _ => result.ErrorDescription,
            };
            return [
                new ListItem(errorCommand)
                {
                    Title = result.ErrorTitle,
                    Subtitle = subtitle,
                }
            ];
        }

        var items = _domains is null
            ? result.Items
            : result.Items.Where(e => _domains.Contains(e.Domain));

        return items.Select(CreateItem).ToArray();
    }

    // HA dispatches services asynchronously — even after a 200 response,
    // the entity state we'd refetch may still be stale for a few hundred ms.
    // Wait briefly before signalling the list to refresh.
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(250);

    private void OnServiceCallSucceeded()
    {
        // Fire-and-forget: invalidate cache + tell CmdPal to re-call GetItems
        // after HA has had a moment to propagate the new state.
        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(RefreshDelay).ConfigureAwait(false);
            try { RaiseItemsChanged(0); } catch { /* page may have been closed */ }
        });
    }

    private ListItem CreateItem(HaEntity entity)
    {
        var primary = BuildPrimaryCommand(entity);
        return new ListItem(primary)
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            Icon = IconForEntity(entity),
            MoreCommands = BuildContextCommands(entity, primary),
            Details = BuildDetails(entity),
        };
    }

    private static Details BuildDetails(HaEntity entity)
    {
        var meta = new List<IDetailsElement>
        {
            Row("State", FormatStateWithUnit(entity)),
        };

        if (entity.Domain == "light")
        {
            AddLightRows(entity, meta);
        }
        else if (entity.Domain == "cover")
        {
            AddCoverRows(entity, meta);
        }
        else if (entity.Domain == "media_player")
        {
            AddMediaPlayerRows(entity, meta);
        }
        else if (entity.Domain == "climate")
        {
            AddClimateRows(entity, meta);
        }
        else if (entity.Domain == "vacuum")
        {
            AddVacuumRows(entity, meta);
        }
        else if (entity.Domain == "fan")
        {
            AddFanRows(entity, meta);
        }
        else if (entity.Domain == "person")
        {
            AddPersonRows(entity, meta);
        }
        else if (entity.Domain == "automation")
        {
            AddAutomationRows(entity, meta);
        }
        else if (entity.Domain == "sensor" || entity.Domain == "binary_sensor")
        {
            AddSensorRows(entity, meta);
        }

        if (!string.IsNullOrEmpty(entity.AreaName))
        {
            meta.Add(Row("Area", entity.AreaName));
        }

        if (entity.LastChanged is DateTimeOffset changed)
        {
            meta.Add(Row("Last changed", FormatRelativeTime(changed)));
        }

        if (entity.Attributes.TryGetValue("attribution", out var att) && att is string atts && !string.IsNullOrEmpty(atts))
        {
            meta.Add(Row("Attribution", atts));
        }

        meta.Add(Row("Entity ID", entity.EntityId));

        return new Details
        {
            Title = entity.FriendlyName,
            Metadata = meta.ToArray(),
        };
    }

    private static string FormatStateWithUnit(HaEntity entity)
    {
        var state = string.IsNullOrEmpty(entity.State) ? "(no state)" : entity.State;
        if (entity.Attributes.TryGetValue("unit_of_measurement", out var u) && u is string unit && !string.IsNullOrEmpty(unit))
        {
            return $"{state} {unit}";
        }
        return state;
    }

    private static string FormatRelativeTime(DateTimeOffset when)
    {
        var diff = DateTimeOffset.UtcNow - when;
        if (diff.TotalSeconds < 0) return when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return when.ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddSensorRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("device_class", out var dc) && dc is string dcs && !string.IsNullOrEmpty(dcs))
        {
            meta.Add(Row("Device class", dcs));
        }
        if (entity.Attributes.TryGetValue("state_class", out var sc) && sc is string scs && !string.IsNullOrEmpty(scs))
        {
            meta.Add(Row("State class", scs));
        }
    }

    private static void AddLightRows(HaEntity entity, List<IDetailsElement> meta)
    {
        // brightness in HA states is 0-255
        if (entity.Attributes.TryGetValue("brightness", out var b) && b is long br && br > 0)
        {
            meta.Add(Row("Brightness", $"{(int)System.Math.Round(br / 255.0 * 100)}%"));
        }
        if (entity.Attributes.TryGetValue("color_temp_kelvin", out var ctk) && ctk is long k && k > 0)
        {
            meta.Add(Row("Color temp", $"{k}K"));
        }
        if (entity.Attributes.TryGetValue("rgb_color", out var rgb) && rgb is not null)
        {
            meta.Add(Row("RGB", rgb.ToString() ?? string.Empty));
        }
        if (entity.Attributes.TryGetValue("color_mode", out var mode) && mode is string m && !string.IsNullOrEmpty(m))
        {
            meta.Add(Row("Color mode", m));
        }
        if (entity.Attributes.TryGetValue("effect", out var fx) && fx is string fxs && !string.IsNullOrEmpty(fxs) && !string.Equals(fxs, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            meta.Add(Row("Effect", fxs));
        }
    }

    private static void AddPersonRows(HaEntity entity, List<IDetailsElement> meta)
    {
        // Person `state` is the zone name ("home" / "not_home" / a custom
        // zone). Lat/lon come from whichever device_tracker reports the
        // freshest data; HA exposes that tracker via the `source` attribute.
        if (TryGetDouble(entity.Attributes, "latitude", out var lat) &&
            TryGetDouble(entity.Attributes, "longitude", out var lon))
        {
            meta.Add(Row("Location", $"{lat}, {lon}"));
        }
        if (TryGetDouble(entity.Attributes, "gps_accuracy", out var acc))
        {
            meta.Add(Row("GPS accuracy", $"{(int)acc} m"));
        }
        if (entity.Attributes.TryGetValue("source", out var src) && src is string srcs && !string.IsNullOrEmpty(srcs))
        {
            meta.Add(Row("Source", srcs));
        }
    }

    private static bool TryGetDouble(IReadOnlyDictionary<string, object?> attrs, string key, out double value)
    {
        if (attrs.TryGetValue(key, out var v))
        {
            switch (v)
            {
                case double d: value = d; return true;
                case long l: value = l; return true;
            }
        }
        value = 0;
        return false;
    }

    private static void AddFanRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("percentage", out var pct))
        {
            var v = pct switch { long l => $"{l}%", double d => $"{(int)d}%", _ => null };
            if (v is not null) meta.Add(Row("Speed", v));
        }
        if (entity.Attributes.TryGetValue("preset_mode", out var pm) && pm is string pms && !string.IsNullOrEmpty(pms))
        {
            meta.Add(Row("Preset", pms));
        }
        if (entity.Attributes.TryGetValue("oscillating", out var osc) && osc is bool b)
        {
            meta.Add(Row("Oscillating", b ? "yes" : "no"));
        }
        if (entity.Attributes.TryGetValue("direction", out var dir) && dir is string dirs && !string.IsNullOrEmpty(dirs))
        {
            meta.Add(Row("Direction", dirs));
        }
    }

    private static void AddAutomationRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("last_triggered", out var lt) && lt is string lts && !string.IsNullOrEmpty(lts))
        {
            // ISO timestamp from HA. Show as the original ISO — clearer than
            // a fragile relative-time format, and tooling-friendly.
            meta.Add(Row("Last triggered", lts));
        }
        if (entity.Attributes.TryGetValue("mode", out var mode) && mode is string ms && !string.IsNullOrEmpty(ms))
        {
            meta.Add(Row("Mode", ms));
        }
        if (entity.Attributes.TryGetValue("current", out var current))
        {
            // Number of currently-running instances (relevant for parallel
            // / queued mode). 0 normally; >0 means the automation is mid-run.
            var v = current switch { long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture), double d => ((int)d).ToString(System.Globalization.CultureInfo.InvariantCulture), _ => null };
            if (v is not null) meta.Add(Row("Running", v));
        }
        if (entity.Attributes.TryGetValue("id", out var id) && id is string ids && !string.IsNullOrEmpty(ids))
        {
            meta.Add(Row("Automation ID", ids));
        }
    }

    private static void AddVacuumRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("status", out var st) && st is string sts && !string.IsNullOrEmpty(sts))
        {
            meta.Add(Row("Status", sts));
        }
        if (entity.Attributes.TryGetValue("battery_level", out var bat))
        {
            var v = bat switch { long l => $"{l}%", double d => $"{(int)d}%", _ => null };
            if (v is not null) meta.Add(Row("Battery", v));
        }
        if (entity.Attributes.TryGetValue("fan_speed", out var fs) && fs is string fss && !string.IsNullOrEmpty(fss))
        {
            meta.Add(Row("Fan speed", fss));
        }
        if (entity.Attributes.TryGetValue("cleaned_area", out var area))
        {
            var v = area switch { long l => $"{l} m²", double d => $"{d} m²", _ => null };
            if (v is not null) meta.Add(Row("Cleaned", v));
        }
    }

    private static void AddClimateRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("current_temperature", out var ct))
        {
            var v = ct switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) meta.Add(Row("Current temp", v));
        }
        if (entity.Attributes.TryGetValue("temperature", out var tt))
        {
            var v = tt switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) meta.Add(Row("Target temp", v));
        }
        if (entity.Attributes.TryGetValue("current_humidity", out var ch))
        {
            var v = ch switch { double d => $"{(int)d}%", long l => $"{l}%", _ => null };
            if (v is not null) meta.Add(Row("Humidity", v));
        }
        if (entity.Attributes.TryGetValue("hvac_action", out var ha) && ha is string has && !string.IsNullOrEmpty(has))
        {
            meta.Add(Row("Action", has));
        }
        if (entity.Attributes.TryGetValue("fan_mode", out var fm) && fm is string fms && !string.IsNullOrEmpty(fms))
        {
            meta.Add(Row("Fan", fms));
        }
        if (entity.Attributes.TryGetValue("preset_mode", out var pm) && pm is string pms && !string.IsNullOrEmpty(pms))
        {
            meta.Add(Row("Preset", pms));
        }
    }

    private static void AddMediaPlayerRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("media_title", out var title) && title is string ts && !string.IsNullOrEmpty(ts))
        {
            meta.Add(Row("Track", ts));
        }
        if (entity.Attributes.TryGetValue("media_artist", out var artist) && artist is string ars && !string.IsNullOrEmpty(ars))
        {
            meta.Add(Row("Artist", ars));
        }
        if (entity.Attributes.TryGetValue("media_album_name", out var album) && album is string als && !string.IsNullOrEmpty(als))
        {
            meta.Add(Row("Album", als));
        }
        if (entity.Attributes.TryGetValue("source", out var source) && source is string src && !string.IsNullOrEmpty(src))
        {
            meta.Add(Row("Source", src));
        }
        // volume_level is 0.0..1.0
        if (entity.Attributes.TryGetValue("volume_level", out var vol) && vol is double v)
        {
            meta.Add(Row("Volume", $"{(int)System.Math.Round(v * 100)}%"));
        }
        else if (entity.Attributes.TryGetValue("volume_level", out var vol2) && vol2 is long lv)
        {
            meta.Add(Row("Volume", $"{lv * 100}%"));
        }
        if (entity.Attributes.TryGetValue("is_volume_muted", out var muted) && muted is bool m)
        {
            meta.Add(Row("Muted", m ? "yes" : "no"));
        }
        if (entity.Attributes.TryGetValue("app_name", out var app) && app is string apps && !string.IsNullOrEmpty(apps))
        {
            meta.Add(Row("App", apps));
        }
    }

    private static void AddCoverRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("current_position", out var pos) && pos is long p)
        {
            meta.Add(Row("Position", $"{p}%"));
        }
        if (entity.Attributes.TryGetValue("current_tilt_position", out var tilt) && tilt is long t)
        {
            meta.Add(Row("Tilt", $"{t}%"));
        }
        if (entity.Attributes.TryGetValue("device_class", out var dc) && dc is string dcs && !string.IsNullOrEmpty(dcs))
        {
            meta.Add(Row("Device class", dcs));
        }
    }

    private static DetailsElement Row(string key, string value) => new()
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };

    private static IconInfo IconForEntity(HaEntity entity)
    {
        var unavailable = string.Equals(entity.State, "unavailable", System.StringComparison.OrdinalIgnoreCase);

        if (entity.Domain == "light")
        {
            // Detect a lights group via the standard HA `mdi:lightbulb-group`
            // entity icon override. Falls back to a single bulb otherwise.
            var isGroup = entity.Attributes.TryGetValue("icon", out var ic)
                && ic is string s
                && string.Equals(s, "mdi:lightbulb-group", System.StringComparison.OrdinalIgnoreCase);

            if (unavailable) return isGroup ? Icons.LightGroupUnavailable : Icons.LightUnavailable;
            if (entity.IsOn) return isGroup ? Icons.LightGroupOn : Icons.LightOn;
            return isGroup ? Icons.LightGroupOff : Icons.LightOff;
        }

        if (entity.Domain == "cover")
        {
            if (unavailable) return Icons.CoverUnavailable;
            return entity.State.ToLowerInvariant() switch
            {
                "opening" => Icons.CoverOpening,
                "closing" => Icons.CoverClosing,
                "closed" => Icons.CoverClosed,
                _ => Icons.CoverOpen, // open + unknown → open
            };
        }

        if (entity.Domain == "media_player")
        {
            if (unavailable) return Icons.MediaPlayerUnavailable;
            return string.Equals(entity.State, "playing", System.StringComparison.OrdinalIgnoreCase)
                ? Icons.MediaPlayerPlaying
                : Icons.MediaPlayerIdle;
        }

        if (entity.Domain == "climate")
        {
            if (unavailable) return Icons.ClimateUnavailable;
            return entity.State.ToLowerInvariant() switch
            {
                "off" => Icons.ClimateOff,
                "auto" or "heat_cool" => Icons.ClimateAuto,
                _ => Icons.ClimateActive, // heat / cool / dry / fan_only
            };
        }

        if (entity.Domain == "vacuum")
        {
            if (unavailable) return Icons.VacuumUnavailable;
            return string.Equals(entity.State, "cleaning", System.StringComparison.OrdinalIgnoreCase)
                ? Icons.VacuumCleaning
                : Icons.VacuumIdle;
        }

        if (entity.Domain == "automation")
        {
            if (unavailable) return Icons.AutomationUnavailable;
            return entity.IsOn ? Icons.AutomationOn : Icons.AutomationOff;
        }

        if (entity.Domain == "fan")
        {
            if (unavailable) return Icons.FanUnavailable;
            return entity.IsOn ? Icons.FanOn : Icons.FanOff;
        }

        return Icons.App;
    }

    private ICommand BuildPrimaryCommand(HaEntity entity)
    {
        // Default action picked per-domain. Falls back to "open in dashboard"
        // for read-only or unsupported domains.
        return entity.Domain switch
        {
            "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "cover" or "media_player"
                => new CallServiceCommand(_client, entity.Domain, "toggle", entity.EntityId, $"Toggle {entity.FriendlyName}", icon: Icons.Toggle, onSuccess: OnServiceCallSucceeded),
            "scene"
                => new CallServiceCommand(_client, "scene", "turn_on", entity.EntityId, $"Activate {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "script"
                => new CallServiceCommand(_client, "script", "turn_on", entity.EntityId, $"Run {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "button" or "input_button"
                => new CallServiceCommand(_client, entity.Domain, "press", entity.EntityId, $"Press {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "vacuum"
                => string.Equals(entity.State, "cleaning", System.StringComparison.OrdinalIgnoreCase)
                    ? new CallServiceCommand(_client, "vacuum", "pause", entity.EntityId, $"Pause {entity.FriendlyName}", icon: Icons.Pause, onSuccess: OnServiceCallSucceeded)
                    : new CallServiceCommand(_client, "vacuum", "start", entity.EntityId, $"Start {entity.FriendlyName}", icon: Icons.Play, onSuccess: OnServiceCallSucceeded),
            _ => new OpenDashboardCommand(_settings, entity.EntityId),
        };
    }

    private CommandContextItem BrightnessPreset(HaEntity entity, int pct) =>
        new(new CallServiceCommand(
            _client,
            domain: "light",
            service: "turn_on",
            entityId: entity.EntityId,
            displayName: $"{pct}%",
            icon: Icons.Brightness,
            extraData: new Dictionary<string, object?> { ["brightness_pct"] = pct },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem CoverPositionPreset(HaEntity entity, int position) =>
        new(new CallServiceCommand(
            _client,
            domain: "cover",
            service: "set_cover_position",
            entityId: entity.EntityId,
            displayName: $"{position}%",
            icon: position == 0 ? Icons.Close : (position == 100 ? Icons.Open : Icons.Stop),
            extraData: new Dictionary<string, object?> { ["position"] = position },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem VolumePreset(HaEntity entity, int pct) =>
        new(new CallServiceCommand(
            _client,
            domain: "media_player",
            service: "volume_set",
            entityId: entity.EntityId,
            displayName: $"{pct}%",
            icon: Icons.Volume,
            // volume_level wants 0.0..1.0 — convert from percentage.
            extraData: new Dictionary<string, object?> { ["volume_level"] = pct / 100.0 },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem FanSpeedPreset(HaEntity entity, int pct) =>
        new(new CallServiceCommand(
            _client,
            domain: "fan",
            // turn_on with percentage starts the fan if it was off (matches
            // Raycast behaviour and avoids a no-op when state="off").
            service: "turn_on",
            entityId: entity.EntityId,
            displayName: $"{pct}%",
            icon: Icons.Fan,
            extraData: new Dictionary<string, object?> { ["percentage"] = pct },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem TemperaturePreset(HaEntity entity, double temp) =>
        new(new CallServiceCommand(
            _client,
            domain: "climate",
            service: "set_temperature",
            entityId: entity.EntityId,
            displayName: $"{temp}°",
            icon: Icons.Thermometer,
            extraData: new Dictionary<string, object?> { ["temperature"] = temp },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem ClimateModeItem(HaEntity entity, string mode) =>
        new(new CallServiceCommand(
            _client,
            domain: "climate",
            service: "set_hvac_mode",
            entityId: entity.EntityId,
            displayName: mode,
            extraData: new Dictionary<string, object?> { ["hvac_mode"] = mode },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem FanModeItem(HaEntity entity, string mode) =>
        new(new CallServiceCommand(
            _client,
            domain: "climate",
            service: "set_fan_mode",
            entityId: entity.EntityId,
            displayName: mode,
            extraData: new Dictionary<string, object?> { ["fan_mode"] = mode },
            onSuccess: OnServiceCallSucceeded));

    private static double GetCurrentTargetTemp(HaEntity entity)
    {
        if (!entity.Attributes.TryGetValue("temperature", out var t)) return double.NaN;
        return t switch
        {
            double d => d,
            long l => l,
            _ => double.NaN,
        };
    }

    private static double GetTempStep(HaEntity entity)
    {
        if (!entity.Attributes.TryGetValue("target_temp_step", out var s)) return 0.5;
        return s switch
        {
            double d => d,
            long l => l,
            _ => 0.5,
        };
    }

    private IContextItem[] BuildContextCommands(HaEntity entity, ICommand primary)
    {
        var items = new List<IContextItem>(8);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "media_player")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_on", entity.EntityId, $"Turn on {entity.FriendlyName}", icon: Icons.TurnOn, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_off", entity.EntityId, $"Turn off {entity.FriendlyName}", icon: Icons.TurnOff, onSuccess: OnServiceCallSucceeded)));
        }

        // Manual trigger — fire the automation regardless of trigger
        // conditions. Distinct from turn_on, which only enables it.
        if (entity.Domain == "automation")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "automation", "trigger", entity.EntityId, $"Trigger {entity.FriendlyName}", icon: Icons.Trigger, onSuccess: OnServiceCallSucceeded)));
        }

        // Light brightness presets — nested under a single "Set brightness"
        // parent so the top-level context menu stays short. Each preset
        // calls light.turn_on with brightness_pct (HA turns the light on
        // at that level whether it was on or off).
        if (entity.Domain == "light")
        {
            var presets = new IContextItem[]
            {
                BrightnessPreset(entity, 25),
                BrightnessPreset(entity, 50),
                BrightnessPreset(entity, 75),
                BrightnessPreset(entity, 100),
            };
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set brightness…",
                Icon = Icons.Brightness,
                MoreCommands = presets,
            });
        }

        if (entity.Domain == "fan")
        {
            // Gate speed actions by SET_SPEED bit (1) when supported_features
            // is reported. If the attribute is missing, optimistically allow.
            var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
            var supportsSpeed = sf < 0 || (sf & 1) == 1;

            if (supportsSpeed)
            {
                // Speed up / down — single step from current percentage.
                // Skip when the device doesn't publish percentage_step or
                // when the resulting value would clamp out of range.
                var currentPct = entity.Attributes.TryGetValue("percentage", out var pv) ? pv switch { long l => (double)l, double d => d, _ => double.NaN } : double.NaN;
                var step = entity.Attributes.TryGetValue("percentage_step", out var sv) ? sv switch { long l => (double)l, double d => d, _ => double.NaN } : double.NaN;
                if (!double.IsNaN(currentPct) && !double.IsNaN(step) && step > 0)
                {
                    var up = (int)System.Math.Round(currentPct + step);
                    var down = (int)System.Math.Round(currentPct - step);
                    if (up <= 100)
                    {
                        items.Add(new CommandContextItem(
                            new CallServiceCommand(_client, "fan", "turn_on", entity.EntityId, $"Speed up to {up}%", icon: Icons.Fan,
                                extraData: new Dictionary<string, object?> { ["percentage"] = up },
                                onSuccess: OnServiceCallSucceeded)));
                    }
                    if (down >= 0)
                    {
                        items.Add(new CommandContextItem(
                            new CallServiceCommand(_client, "fan", "turn_on", entity.EntityId, $"Speed down to {down}%", icon: Icons.Fan,
                                extraData: new Dictionary<string, object?> { ["percentage"] = down },
                                onSuccess: OnServiceCallSucceeded)));
                    }
                }

                // Set speed presets — mirrors lights' brightness shape.
                // 0% is intentionally omitted because Turn Off already
                // exists at the top of the menu.
                var presets = new IContextItem[]
                {
                    FanSpeedPreset(entity, 25),
                    FanSpeedPreset(entity, 50),
                    FanSpeedPreset(entity, 75),
                    FanSpeedPreset(entity, 100),
                };
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set speed…",
                    Icon = Icons.Fan,
                    MoreCommands = presets,
                });
            }
        }

        if (entity.Domain == "media_player")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_play_pause", entity.EntityId, $"Play / Pause {entity.FriendlyName}", icon: Icons.PlayPause, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_play", entity.EntityId, $"Play {entity.FriendlyName}", icon: Icons.Play, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_pause", entity.EntityId, $"Pause {entity.FriendlyName}", icon: Icons.Pause, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_stop", entity.EntityId, $"Stop {entity.FriendlyName}", icon: Icons.Stop, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_next_track", entity.EntityId, $"Next track on {entity.FriendlyName}", icon: Icons.Next, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_previous_track", entity.EntityId, $"Previous track on {entity.FriendlyName}", icon: Icons.Previous, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "volume_up", entity.EntityId, $"Volume up on {entity.FriendlyName}", icon: Icons.VolumeUp, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "volume_down", entity.EntityId, $"Volume down on {entity.FriendlyName}", icon: Icons.VolumeDown, onSuccess: OnServiceCallSucceeded)));

            // Mute toggle — read current is_volume_muted to flip it. Skip if
            // unknown (not all media_players publish the attribute).
            if (entity.Attributes.TryGetValue("is_volume_muted", out var muted) && muted is bool isMuted)
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(
                        _client,
                        domain: "media_player",
                        service: "volume_mute",
                        entityId: entity.EntityId,
                        displayName: isMuted ? $"Unmute {entity.FriendlyName}" : $"Mute {entity.FriendlyName}",
                        icon: Icons.VolumeMute,
                        extraData: new Dictionary<string, object?> { ["is_volume_muted"] = !isMuted },
                        onSuccess: OnServiceCallSucceeded)));
            }

            // Volume presets — only when the entity supports volume_set.
            // supported_features bit 4 (value 4) = VOLUME_SET on media_player.
            if (entity.Attributes.TryGetValue("supported_features", out var sf) && sf is long bits && (bits & 4) == 4)
            {
                var presets = new IContextItem[]
                {
                    VolumePreset(entity, 25),
                    VolumePreset(entity, 50),
                    VolumePreset(entity, 75),
                    VolumePreset(entity, 100),
                };
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set volume…",
                    Icon = Icons.Volume,
                    MoreCommands = presets,
                });
            }
        }

        if (entity.Domain == "climate")
        {
            // Target temperature ± buttons (clamped to min/max if HA reports them).
            var current = GetCurrentTargetTemp(entity);
            var step = GetTempStep(entity);
            if (!double.IsNaN(current))
            {
                var increased = System.Math.Round(current + step, 1);
                var decreased = System.Math.Round(current - step, 1);
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "climate", "set_temperature", entity.EntityId,
                        $"Increase to {increased}°", icon: Icons.Thermometer,
                        extraData: new Dictionary<string, object?> { ["temperature"] = increased },
                        onSuccess: OnServiceCallSucceeded)));
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "climate", "set_temperature", entity.EntityId,
                        $"Decrease to {decreased}°", icon: Icons.Thermometer,
                        extraData: new Dictionary<string, object?> { ["temperature"] = decreased },
                        onSuccess: OnServiceCallSucceeded)));
            }

            // Common temperature presets — handy when the climate isn't
            // currently running (no current target to ±-step from).
            var tempPresets = new IContextItem[]
            {
                TemperaturePreset(entity, 18),
                TemperaturePreset(entity, 20),
                TemperaturePreset(entity, 21),
                TemperaturePreset(entity, 22),
                TemperaturePreset(entity, 24),
            };
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set temperature…",
                Icon = Icons.Thermometer,
                MoreCommands = tempPresets,
            });

            // HVAC mode submenu — surface every supported mode reported by
            // the entity (off/heat/cool/heat_cool/auto/dry/fan_only).
            if (entity.Attributes.TryGetValue("hvac_modes", out var hvm) && hvm is List<object?> modes)
            {
                var modeItems = modes
                    .OfType<string>()
                    .Select(m => (IContextItem)ClimateModeItem(entity, m))
                    .ToArray();
                if (modeItems.Length > 0)
                {
                    items.Add(new CommandContextItem(new NoOpCommand())
                    {
                        Title = "Set HVAC mode…",
                        Icon = Icons.Thermostat,
                        MoreCommands = modeItems,
                    });
                }
            }

            // Fan mode submenu — same pattern, using fan_modes attribute.
            if (entity.Attributes.TryGetValue("fan_modes", out var fm) && fm is List<object?> fanModes)
            {
                var modeItems = fanModes
                    .OfType<string>()
                    .Select(m => (IContextItem)FanModeItem(entity, m))
                    .ToArray();
                if (modeItems.Length > 0)
                {
                    items.Add(new CommandContextItem(new NoOpCommand())
                    {
                        Title = "Set fan mode…",
                        Icon = Icons.Fan,
                        MoreCommands = modeItems,
                    });
                }
            }
        }

        if (entity.Domain == "vacuum")
        {
            // supported_features bits — only surface actions the device
            // declares it can do. Bit values from HA's vacuum component:
            //   1 TURN_ON, 2 TURN_OFF, 4 PAUSE, 8 STOP, 16 RETURN_HOME,
            //   32 FAN_SPEED, 64 BATTERY, 128 STATUS, 256 SEND_COMMAND,
            //   512 LOCATE, 1024 CLEAN_SPOT, 4096 STATE, 8192 START.
            var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
            bool Has(long bit) => sf < 0 || (sf & bit) == bit;

            if (Has(8192))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "vacuum", "start", entity.EntityId, $"Start {entity.FriendlyName}", icon: Icons.Play, onSuccess: OnServiceCallSucceeded)));
            }
            if (Has(4))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "vacuum", "pause", entity.EntityId, $"Pause {entity.FriendlyName}", icon: Icons.Pause, onSuccess: OnServiceCallSucceeded)));
            }
            if (Has(8))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "vacuum", "stop", entity.EntityId, $"Stop {entity.FriendlyName}", icon: Icons.Stop, onSuccess: OnServiceCallSucceeded)));
            }
            if (Has(16))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "vacuum", "return_to_base", entity.EntityId, $"Send {entity.FriendlyName} home", icon: Icons.Home, onSuccess: OnServiceCallSucceeded)));
            }
            if (Has(512))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "vacuum", "locate", entity.EntityId, $"Locate {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            }
            if (Has(1024))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "vacuum", "clean_spot", entity.EntityId, $"Clean spot with {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            }
            // Fan speed submenu when the vacuum reports fan_speed_list and
            // supports FAN_SPEED.
            if (Has(32) && entity.Attributes.TryGetValue("fan_speed_list", out var fsl) && fsl is List<object?> speeds)
            {
                var speedItems = speeds
                    .OfType<string>()
                    .Select(s => (IContextItem)new CommandContextItem(new CallServiceCommand(
                        _client, "vacuum", "set_fan_speed", entity.EntityId,
                        s, extraData: new Dictionary<string, object?> { ["fan_speed"] = s },
                        onSuccess: OnServiceCallSucceeded)))
                    .ToArray();
                if (speedItems.Length > 0)
                {
                    items.Add(new CommandContextItem(new NoOpCommand())
                    {
                        Title = "Set fan speed…",
                        Icon = Icons.Fan,
                        MoreCommands = speedItems,
                    });
                }
            }
        }

        if (entity.Domain == "cover")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "cover", "open_cover", entity.EntityId, $"Open {entity.FriendlyName}", icon: Icons.Open, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "cover", "close_cover", entity.EntityId, $"Close {entity.FriendlyName}", icon: Icons.Close, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "cover", "stop_cover", entity.EntityId, $"Stop {entity.FriendlyName}", icon: Icons.Stop, onSuccess: OnServiceCallSucceeded)));

            // Position presets only when the cover supports set_cover_position
            // (HA exposes this via the supported_features bitmask; bit 2 = 4
            // means SET_POSITION). Falls back to skipping the menu silently.
            if (entity.Attributes.TryGetValue("supported_features", out var sf) && sf is long bits && (bits & 4) == 4)
            {
                var presets = new IContextItem[]
                {
                    CoverPositionPreset(entity, 0),
                    CoverPositionPreset(entity, 25),
                    CoverPositionPreset(entity, 50),
                    CoverPositionPreset(entity, 75),
                    CoverPositionPreset(entity, 100),
                };
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set position…",
                    Icon = Icons.Stop,
                    MoreCommands = presets,
                });
            }
        }

        if (entity.Domain == "person")
        {
            // Open in Google Maps — universal across platforms (Apple Maps
            // is macOS-only, so we don't ship it on Windows).
            if (TryGetDouble(entity.Attributes, "latitude", out var lat) &&
                TryGetDouble(entity.Attributes, "longitude", out var lon))
            {
                var url = $"https://www.google.com/maps/search/?api=1&query={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                items.Add(new CommandContextItem(new OpenUrlCommand(url) { Name = "Open in Google Maps" }));
            }
            // user_id is the HA user UUID this person is linked to — handy
            // when wiring up automations or template conditions.
            if (entity.Attributes.TryGetValue("user_id", out var uid) && uid is string uids && !string.IsNullOrEmpty(uids))
            {
                items.Add(new CommandContextItem(new CopyTextCommand(uids) { Name = "Copy user ID" }));
            }
        }

        // Skip the dashboard context item when it'd duplicate the primary
        // action (sensors, climate, weather, etc. fall through to dashboard).
        if (primary is not OpenDashboardCommand)
        {
            items.Add(new CommandContextItem(new OpenDashboardCommand(_settings, entity.EntityId)));
        }
        items.Add(new CommandContextItem(new CopyTextCommand(entity.EntityId)
        {
            Name = "Copy entity ID",
        }));

        return items.ToArray();
    }

    private static string BuildSubtitle(HaEntity entity)
    {
        // Mirror the Raycast subtitle: area (room) name only. State stays
        // in the tags. Empty when the entity has no area — a clean list
        // beats noisy fallbacks.
        return entity.AreaName ?? string.Empty;
    }

    private Tag[] BuildTags(HaEntity entity)
    {
        // Hide the domain tag on single-domain pages (Lights, Covers, ...) —
        // it's redundant. Keep it on All Entities and multi-domain pages
        // (Buttons, Helpers) so users can tell entities apart.
        var showDomainTag = _domains is null || _domains.Count > 1;
        var tags = new List<Tag>(2);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "media_player" or "binary_sensor" or "cover")
        {
            tags.Add(entity.IsOn
                ? new Tag("ON")
                {
                    Background = ColorHelpers.FromArgb(255, 76, 161, 222),
                    Foreground = ColorHelpers.FromRgb(255, 255, 255),
                }
                : new Tag("OFF")
                {
                    Background = ColorHelpers.FromRgb(120, 120, 120),
                    Foreground = ColorHelpers.FromRgb(255, 255, 255),
                });
        }

        if (showDomainTag)
        {
            tags.Add(new Tag(entity.Domain) { ToolTip = "Domain" });
        }

        return tags.ToArray();
    }
}
