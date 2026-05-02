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
    private readonly IHaClient _client;
    private readonly HashSet<string>? _domains;
    private readonly HashSet<string>? _deviceClasses;
    private readonly bool _sortByNumericStateAscending;

    public EntityListPage(
        HaSettings settings,
        IHaClient client,
        string title,
        string id,
        IReadOnlyCollection<string>? domains = null,
        IconInfo? icon = null,
        IReadOnlyCollection<string>? deviceClasses = null,
        bool sortByNumericStateAscending = false)
    {
        _settings = settings;
        _client = client;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);
        _deviceClasses = deviceClasses is null ? null : new HashSet<string>(deviceClasses, StringComparer.Ordinal);
        _sortByNumericStateAscending = sortByNumericStateAscending;

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

        IEnumerable<HaEntity> items = result.Items;
        if (_domains is not null)
        {
            items = items.Where(e => _domains.Contains(e.Domain));
        }
        if (_deviceClasses is not null)
        {
            items = items.Where(e => e.Attributes.TryGetValue("device_class", out var dc)
                && dc is string dcs && _deviceClasses.Contains(dcs));
        }
        if (_sortByNumericStateAscending)
        {
            // Used by the Batteries page to surface lowest-charge sensors
            // first. Non-numeric states (e.g. "unavailable") sort to the
            // end via double.PositiveInfinity.
            items = items.OrderBy(e => double.TryParse(e.State,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : double.PositiveInfinity);
        }

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
            Icon = ResolveEntityIcon(entity),
            MoreCommands = BuildContextCommands(entity, primary),
            Details = BuildDetails(entity),
        };
    }

    /// <summary>
    /// Wraps <see cref="IconForEntity"/> with instance-level fallbacks that
    /// need authenticated HTTP. Persons get their <c>entity_picture</c>
    /// avatar (Gravatar or HA-served) when one is set; if the fetch fails
    /// we drop back to the state-tinted account glyph.
    /// </summary>
    private IconInfo ResolveEntityIcon(HaEntity entity)
    {
        if (entity.Domain == "person"
            && entity.Attributes.TryGetValue("entity_picture", out var pic)
            && pic is string picUrl
            && !string.IsNullOrEmpty(picUrl))
        {
            var path = _client.GetEntityPicturePath(entity.EntityId, picUrl);
            if (path is not null) return new IconInfo(path);
        }

        return IconForEntity(entity);
    }

    private Details BuildDetails(HaEntity entity)
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
        else if (entity.Domain == "update")
        {
            AddUpdateRows(entity, meta);
        }
        else if (entity.Domain == "input_number")
        {
            AddInputNumberRows(entity, meta);
        }
        else if (entity.Domain == "timer")
        {
            AddTimerRows(entity, meta);
        }
        else if (entity.Domain == "input_select")
        {
            AddInputSelectRows(entity, meta);
        }
        else if (entity.Domain == "automation")
        {
            AddAutomationRows(entity, meta);
        }
        else if (entity.Domain == "weather")
        {
            AddWeatherRows(entity, meta);
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

        var details = new Details
        {
            Title = entity.FriendlyName,
            Metadata = meta.ToArray(),
        };

        if (entity.Domain == "camera")
        {
            // /api/camera_proxy/{entity_id} requires a Bearer header that
            // CmdPal can't add to a remote URL, so we cache the bytes to a
            // temp file and hand the file path to HeroImage. Returns null
            // on auth/timeout failure — the rest of the details still
            // render fine in that case.
            var snapshot = _client.GetCameraSnapshotPath(entity.EntityId);
            if (snapshot is not null)
            {
                details.HeroImage = new IconInfo(snapshot);
            }
        }

        return details;
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

    private static void AddWeatherRows(HaEntity entity, List<IDetailsElement> meta)
    {
        // HA reports the temperature unit on the weather entity itself
        // (°C / °F). Pressure / wind / visibility / precipitation each
        // carry their own *_unit; fall back to a sensible default if the
        // integration omits one.
        var tempUnit = (entity.Attributes.TryGetValue("temperature_unit", out var tu) && tu is string tus && !string.IsNullOrEmpty(tus)) ? tus : "°";

        if (TryGetDouble(entity.Attributes, "temperature", out var temp))
        {
            meta.Add(Row("Temperature", $"{FormatNum(temp)} {tempUnit}"));
        }
        if (TryGetDouble(entity.Attributes, "apparent_temperature", out var feels))
        {
            meta.Add(Row("Feels like", $"{FormatNum(feels)} {tempUnit}"));
        }
        if (TryGetDouble(entity.Attributes, "dew_point", out var dew))
        {
            meta.Add(Row("Dew point", $"{FormatNum(dew)} {tempUnit}"));
        }
        if (TryGetDouble(entity.Attributes, "humidity", out var hum))
        {
            meta.Add(Row("Humidity", $"{(int)System.Math.Round(hum)}%"));
        }
        if (TryGetDouble(entity.Attributes, "pressure", out var pres))
        {
            var unit = (entity.Attributes.TryGetValue("pressure_unit", out var pu) && pu is string pus && !string.IsNullOrEmpty(pus)) ? pus : "hPa";
            meta.Add(Row("Pressure", $"{FormatNum(pres)} {unit}"));
        }
        // Wind speed + bearing as a single row — bearing alone is hard to
        // read; speed alone is missing the direction. e.g. "12 km/h NW (315°)".
        if (TryGetDouble(entity.Attributes, "wind_speed", out var wind))
        {
            var unit = (entity.Attributes.TryGetValue("wind_speed_unit", out var wu) && wu is string wus && !string.IsNullOrEmpty(wus)) ? wus : "m/s";
            var label = $"{FormatNum(wind)} {unit}";
            if (TryGetDouble(entity.Attributes, "wind_bearing", out var bearing))
            {
                label = $"{label} {CompassFromBearing(bearing)} ({(int)System.Math.Round(bearing)}°)";
            }
            meta.Add(Row("Wind", label));
        }
        if (TryGetDouble(entity.Attributes, "wind_gust_speed", out var gust))
        {
            var unit = (entity.Attributes.TryGetValue("wind_speed_unit", out var wu) && wu is string wus && !string.IsNullOrEmpty(wus)) ? wus : "m/s";
            meta.Add(Row("Wind gust", $"{FormatNum(gust)} {unit}"));
        }
        if (TryGetDouble(entity.Attributes, "visibility", out var vis))
        {
            var unit = (entity.Attributes.TryGetValue("visibility_unit", out var vu) && vu is string vus && !string.IsNullOrEmpty(vus)) ? vus : "km";
            meta.Add(Row("Visibility", $"{FormatNum(vis)} {unit}"));
        }
        if (TryGetDouble(entity.Attributes, "cloud_coverage", out var cloud))
        {
            meta.Add(Row("Cloud cover", $"{(int)System.Math.Round(cloud)}%"));
        }
        if (TryGetDouble(entity.Attributes, "uv_index", out var uv))
        {
            meta.Add(Row("UV index", FormatNum(uv)));
        }
        if (TryGetDouble(entity.Attributes, "ozone", out var ozone))
        {
            meta.Add(Row("Ozone", $"{FormatNum(ozone)} DU"));
        }
        if (TryGetDouble(entity.Attributes, "precipitation", out var precip))
        {
            var unit = (entity.Attributes.TryGetValue("precipitation_unit", out var pcu) && pcu is string pcus && !string.IsNullOrEmpty(pcus)) ? pcus : "mm";
            meta.Add(Row("Precipitation", $"{FormatNum(precip)} {unit}"));
        }
    }

    private static string CompassFromBearing(double degrees)
    {
        // 16-point compass: N / NNE / NE / ENE / E / … / NNW. Each sector
        // spans 22.5°, centred on the canonical bearing.
        var dirs = new[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        var n = ((degrees % 360) + 360) % 360;
        var idx = (int)System.Math.Round(n / 22.5) % 16;
        return dirs[idx];
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
        // Min / max Kelvin range — only when both are reported. Helps the
        // user know the bulb's tunable-white window without round-tripping
        // to the HA UI. Some firmwares report only the legacy mireds pair,
        // so fall back to mireds → kelvin (k = 1_000_000 / mired).
        var minK = entity.Attributes.TryGetValue("min_color_temp_kelvin", out var mnk) && mnk is long mnki ? mnki : 0;
        var maxK = entity.Attributes.TryGetValue("max_color_temp_kelvin", out var mxk) && mxk is long mxki ? mxki : 0;
        if (minK == 0 && maxK == 0
            && entity.Attributes.TryGetValue("min_mireds", out var mnm) && mnm is long mnmi && mnmi > 0
            && entity.Attributes.TryGetValue("max_mireds", out var mxm) && mxm is long mxmi && mxmi > 0)
        {
            // k = 1_000_000 / mired. Smaller mired → higher kelvin, so the
            // mired pair swaps: min Kelvin comes from max mireds.
            minK = 1_000_000 / mxmi;
            maxK = 1_000_000 / mnmi;
        }
        if (minK > 0 && maxK > 0)
        {
            meta.Add(Row("Color temp range", $"{minK}K – {maxK}K"));
        }
        // HA reports `rgb_color` as a 3-element list of ints (sometimes
        // longs after JSON deserialization). Stringify each component so
        // we don't end up rendering the .NET list's type name.
        if (entity.Attributes.TryGetValue("rgb_color", out var rgb) && rgb is List<object?> rgbList && rgbList.Count == 3)
        {
            var parts = rgbList.Select(c => c switch
            {
                long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
                int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                double d => ((int)System.Math.Round(d)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => c?.ToString() ?? "?",
            });
            meta.Add(Row("RGB", string.Join(", ", parts)));
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

    private static void AddInputNumberRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (TryGetDouble(entity.Attributes, "min", out var min)) meta.Add(Row("Min", FormatNum(min)));
        if (TryGetDouble(entity.Attributes, "max", out var max)) meta.Add(Row("Max", FormatNum(max)));
        if (TryGetDouble(entity.Attributes, "step", out var step)) meta.Add(Row("Step", FormatNum(step)));
        if (entity.Attributes.TryGetValue("mode", out var mode) && mode is string ms && !string.IsNullOrEmpty(ms))
        {
            meta.Add(Row("Mode", ms));
        }
    }

    private static void AddTimerRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("duration", out var d) && d is string ds && !string.IsNullOrEmpty(ds))
        {
            meta.Add(Row("Duration", ds));
        }
        if (entity.Attributes.TryGetValue("remaining", out var r) && r is string rs && !string.IsNullOrEmpty(rs))
        {
            meta.Add(Row("Remaining", rs));
        }
        if (entity.Attributes.TryGetValue("finishes_at", out var f) && f is string fs && !string.IsNullOrEmpty(fs))
        {
            meta.Add(Row("Finishes at", fs));
        }
    }

    private static void AddInputSelectRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("options", out var opts) && opts is List<object?> options)
        {
            meta.Add(Row("Options", options.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
    }

    private static string FormatNum(double v) =>
        v == System.Math.Floor(v)
            ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static void AddUpdateRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("title", out var t) && t is string ts && !string.IsNullOrEmpty(ts))
        {
            meta.Add(Row("Title", ts));
        }
        if (entity.Attributes.TryGetValue("installed_version", out var iv) && iv is string ivs && !string.IsNullOrEmpty(ivs))
        {
            meta.Add(Row("Installed", ivs));
        }
        if (entity.Attributes.TryGetValue("latest_version", out var lv) && lv is string lvs && !string.IsNullOrEmpty(lvs))
        {
            meta.Add(Row("Latest", lvs));
        }
        // in_progress can be a bool (false) or a numeric percent during install.
        if (entity.Attributes.TryGetValue("in_progress", out var ip))
        {
            switch (ip)
            {
                case bool b when b: meta.Add(Row("In progress", "yes")); break;
                case long l: meta.Add(Row("In progress", $"{l}%")); break;
                case double d: meta.Add(Row("In progress", $"{(int)d}%")); break;
            }
        }
        if (entity.Attributes.TryGetValue("auto_update", out var au) && au is bool aub)
        {
            meta.Add(Row("Auto update", aub ? "yes" : "no"));
        }
        if (entity.Attributes.TryGetValue("release_url", out var ru) && ru is string rus && !string.IsNullOrEmpty(rus))
        {
            meta.Add(Row("Release notes", rus));
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
        // `cleaning_time` is integration-specific: most expose minutes as
        // an int, a few expose a pre-formatted "HH:MM:SS" string. Pass
        // strings through untouched and format ints as Hh Mm assuming
        // minutes (matches the frontend's default rendering).
        if (entity.Attributes.TryGetValue("cleaning_time", out var ct))
        {
            var v = ct switch
            {
                string s when !string.IsNullOrEmpty(s) => s,
                long l => FormatMinutes(l),
                double d => FormatMinutes((long)d),
                _ => null,
            };
            if (v is not null) meta.Add(Row("Cleaning time", v));
        }
        if (entity.Attributes.TryGetValue("error", out var err) && err is string errs && !string.IsNullOrEmpty(errs))
        {
            meta.Add(Row("Last error", errs));
        }
    }

    private static string FormatMinutes(long minutes)
    {
        if (minutes < 60) return $"{minutes}m";
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? $"{h}h" : $"{h}h {m}m";
    }

    private static double? TryGetSeconds(IReadOnlyDictionary<string, object?> attrs, string key) =>
        attrs.TryGetValue(key, out var v)
            ? v switch { double d => d, long l => (double)l, _ => (double?)null }
            : null;

    /// <summary>
    /// Renders a duration in seconds as MM:SS, or H:MM:SS when ≥ 1 hour.
    /// </summary>
    private static string FormatTimecode(double totalSeconds)
    {
        var s = (long)System.Math.Round(totalSeconds);
        var hours = s / 3600;
        var minutes = (s % 3600) / 60;
        var seconds = s % 60;
        return hours > 0
            ? $"{hours}:{minutes:D2}:{seconds:D2}"
            : $"{minutes}:{seconds:D2}";
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
        // Dual setpoint (heat_cool / auto): the integration reports
        // target_temp_low / target_temp_high instead of `temperature`.
        if (entity.Attributes.TryGetValue("target_temp_low", out var tlo))
        {
            var v = tlo switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) meta.Add(Row("Target low", v));
        }
        if (entity.Attributes.TryGetValue("target_temp_high", out var thi))
        {
            var v = thi switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) meta.Add(Row("Target high", v));
        }
        // Min / max range — only if both are reported.
        var min = entity.Attributes.TryGetValue("min_temp", out var mn) ? mn : null;
        var max = entity.Attributes.TryGetValue("max_temp", out var mx) ? mx : null;
        static string? FormatTemp(object? o) => o switch { double d => $"{d}°", long l => $"{l}°", _ => null };
        var minStr = FormatTemp(min);
        var maxStr = FormatTemp(max);
        if (minStr is not null && maxStr is not null)
        {
            meta.Add(Row("Range", $"{minStr} – {maxStr}"));
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
        if (entity.Attributes.TryGetValue("swing_mode", out var swm) && swm is string swms && !string.IsNullOrEmpty(swms))
        {
            meta.Add(Row("Swing", swms));
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
        // Position / Duration as MM:SS — when state="playing", advance the
        // reported position by (now - media_position_updated_at) so the row
        // doesn't lag the actual playback by the full list-cache window.
        var posSeconds = TryGetSeconds(entity.Attributes, "media_position");
        var durSeconds = TryGetSeconds(entity.Attributes, "media_duration");
        if (posSeconds is double pos && durSeconds is double dur && dur > 0)
        {
            if (string.Equals(entity.State, "playing", System.StringComparison.OrdinalIgnoreCase)
                && entity.Attributes.TryGetValue("media_position_updated_at", out var puat)
                && puat is string puatS
                && System.DateTimeOffset.TryParse(puatS, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var updated))
            {
                var elapsed = (System.DateTimeOffset.UtcNow - updated).TotalSeconds;
                if (elapsed > 0) pos += elapsed;
            }
            if (pos < 0) pos = 0;
            if (pos > dur) pos = dur;
            meta.Add(Row("Position", $"{FormatTimecode(pos)} / {FormatTimecode(dur)}"));
        }
        if (entity.Attributes.TryGetValue("source", out var source) && source is string src && !string.IsNullOrEmpty(src))
        {
            meta.Add(Row("Source", src));
        }
        if (entity.Attributes.TryGetValue("sound_mode", out var sm) && sm is string sms && !string.IsNullOrEmpty(sms))
        {
            meta.Add(Row("Sound mode", sms));
        }
        if (entity.Attributes.TryGetValue("shuffle", out var shuf) && shuf is bool sb)
        {
            meta.Add(Row("Shuffle", sb ? "on" : "off"));
        }
        if (entity.Attributes.TryGetValue("repeat", out var rep) && rep is string reps && !string.IsNullOrEmpty(reps))
        {
            meta.Add(Row("Repeat", reps));
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
        // `working` is true while the cover is actively moving — distinct
        // from `state == opening|closing` because some integrations report
        // it independently of the discrete state machine.
        if (entity.Attributes.TryGetValue("working", out var working) && working is bool wb)
        {
            meta.Add(Row("Working", wb ? "yes" : "no"));
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

        if (entity.Domain == "switch")
        {
            if (unavailable) return Icons.SwitchUnavailable;
            return entity.IsOn ? Icons.SwitchOn : Icons.SwitchOff;
        }

        if (entity.Domain == "input_boolean")
        {
            if (unavailable) return Icons.InputBooleanUnavailable;
            return entity.IsOn ? Icons.InputBooleanOn : Icons.InputBooleanOff;
        }

        if (entity.Domain == "timer")
        {
            if (unavailable) return Icons.TimerUnavailable;
            // Yellow when active; blue for idle / paused. Mirrors Raycast.
            return string.Equals(entity.State, "active", System.StringComparison.OrdinalIgnoreCase)
                ? Icons.TimerOn
                : Icons.TimerOff;
        }

        if (entity.Domain == "script")
        {
            if (unavailable) return Icons.ScriptUnavailable;
            return entity.IsOn ? Icons.ScriptOn : Icons.ScriptOff;
        }

        if (entity.Domain == "update")
        {
            if (unavailable) return Icons.UpdateUnavailable;
            // state="on" means an update is available — yellow draws the eye.
            return entity.IsOn ? Icons.UpdateOn : Icons.UpdateOff;
        }

        if (entity.Domain == "person")
        {
            if (unavailable) return Icons.PersonUnavailable;
            // state is the zone the person is in. "home" → yellow; any
            // other zone (including "not_home") → blue.
            return string.Equals(entity.State, "home", System.StringComparison.OrdinalIgnoreCase)
                ? Icons.PersonOn
                : Icons.PersonOff;
        }

        if (entity.EntityId == "sun.sun")
        {
            if (unavailable) return Icons.SunUnavailable;
            return string.Equals(entity.State, "below_horizon", System.StringComparison.OrdinalIgnoreCase)
                ? Icons.SunNight
                : Icons.SunDay;
        }

        if (entity.Domain == "scene") return unavailable ? Icons.SceneUnavailable : Icons.Scene;
        if (entity.Domain == "zone") return unavailable ? Icons.ZoneUnavailable : Icons.Zone;
        if (entity.Domain == "camera") return unavailable ? Icons.CameraUnavailable : Icons.Camera;
        if (entity.Domain == "counter") return unavailable ? Icons.CounterUnavailable : Icons.Counter;
        if (entity.Domain == "button") return unavailable ? Icons.ButtonUnavailable : Icons.Button;
        if (entity.Domain == "weather") return Icons.WeatherForCondition(entity.State, unavailable);

        if (entity.Domain == "input_select") return Icons.InputSelect;
        if (entity.Domain == "input_button") return Icons.InputButton;
        if (entity.Domain == "input_number") return Icons.InputNumber;
        if (entity.Domain == "input_text") return Icons.InputText;
        if (entity.Domain == "input_datetime")
        {
            // Pick calendar / clock / both based on the entity's published
            // has_date / has_time flags.
            var hasDate = entity.Attributes.TryGetValue("has_date", out var hd) && hd is bool hdb && hdb;
            var hasTime = entity.Attributes.TryGetValue("has_time", out var ht) && ht is bool htb && htb;
            if (hasDate && !hasTime) return Icons.InputDate;
            if (hasTime && !hasDate) return Icons.InputTime;
            return Icons.InputDateTime;
        }

        // sensor / binary_sensor — drive the icon off device_class. binary
        // sensors carry on/off state; numeric sensors are stateless blue.
        if (entity.Domain is "binary_sensor" or "sensor")
        {
            return IconForDeviceClass(entity, unavailable);
        }

        return unavailable ? Icons.ShapeUnavailable : Icons.Shape;
    }

    private static IconInfo IconForDeviceClass(HaEntity entity, bool unavailable)
    {
        var dc = entity.Attributes.TryGetValue("device_class", out var raw) && raw is string s ? s : null;

        // binary_sensor — yellow when "detected/open/connected", blue when
        // clear, grey when unavailable.
        if (entity.Domain == "binary_sensor")
        {
            switch (dc)
            {
                case "door":
                case "garage_door":
                    if (unavailable) return Icons.DoorUnavailable;
                    return entity.IsOn ? Icons.DoorOpen : Icons.DoorClosed;
                case "window":
                case "opening":
                    if (unavailable) return Icons.WindowUnavailable;
                    return entity.IsOn ? Icons.WindowOpen : Icons.WindowClosed;
                case "motion":
                case "occupancy":
                case "presence":
                case "moving":
                    if (unavailable) return Icons.MotionUnavailable;
                    return entity.IsOn ? Icons.MotionDetected : Icons.MotionClear;
                case "connectivity":
                    if (unavailable) return Icons.ConnectivityUnavailable;
                    return entity.IsOn ? Icons.ConnectivityOn : Icons.ConnectivityOff;
                case "plug":
                case "power":
                    if (unavailable) return Icons.PlugUnavailable;
                    return entity.IsOn ? Icons.PlugOn : Icons.PlugOff;
                case "update":
                    if (unavailable) return Icons.UpdateUnavailable;
                    return entity.IsOn ? Icons.UpdateOn : Icons.UpdateOff;
            }
        }

        // sensor — always-blue icons keyed off device_class. Battery uses a
        // dedicated icon set; the rest fall through to a generic shape when
        // the device_class is missing or unrecognized.
        switch (dc)
        {
            case "battery":
                // Charge level drives both shape (10% buckets) and tint
                // (red ≤ 20, yellow ≤ 30, blue otherwise). When the state
                // doesn't parse as a number we keep the static fallback.
                if (double.TryParse(entity.State,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var pct))
                {
                    return Icons.BatteryForLevel(pct, unavailable);
                }
                return unavailable ? Icons.BatteryUnavailable : Icons.Battery;
            case "temperature":
                return unavailable ? Icons.TemperatureUnavailable : Icons.Temperature;
            case "humidity":
            case "moisture":
                return unavailable ? Icons.HumidityUnavailable : Icons.Humidity;
            case "pressure":
            case "atmospheric_pressure":
                return unavailable ? Icons.PressureUnavailable : Icons.Pressure;
            case "energy":
            case "power":
            case "current":
            case "voltage":
            case "apparent_power":
                return unavailable ? Icons.EnergyUnavailable : Icons.Energy;
            case "power_factor":
                return unavailable ? Icons.PowerFactorUnavailable : Icons.PowerFactor;
            case "carbon_dioxide":
                return unavailable ? Icons.CarbonDioxideUnavailable : Icons.CarbonDioxide;
        }

        return unavailable ? Icons.ShapeUnavailable : Icons.Shape;
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
            "timer"
                => string.Equals(entity.State, "active", System.StringComparison.OrdinalIgnoreCase)
                    ? new CallServiceCommand(_client, "timer", "pause", entity.EntityId, $"Pause {entity.FriendlyName}", icon: Icons.Pause, onSuccess: OnServiceCallSucceeded)
                    : new CallServiceCommand(_client, "timer", "start", entity.EntityId, $"Start {entity.FriendlyName}", icon: Icons.Play, onSuccess: OnServiceCallSucceeded),
            "counter"
                => new CallServiceCommand(_client, "counter", "increment", entity.EntityId, $"Increment {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
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

    // Preset palette for the light "Set color…" submenu. RGB triplets are
    // pure primaries / secondaries so each option behaves the same on every
    // RGB-capable bulb regardless of its colour-mode (rgb / rgbw / hs / xy
    // — HA converts internally). Order mirrors a standard rainbow + white.
    private static readonly (string Name, int[] Rgb)[] ColorPalette =
    [
        ("Red",    new[] { 255,   0,   0 }),
        ("Orange", new[] { 255, 128,   0 }),
        ("Yellow", new[] { 255, 255,   0 }),
        ("Green",  new[] {   0, 255,   0 }),
        ("Cyan",   new[] {   0, 255, 255 }),
        ("Blue",   new[] {   0,   0, 255 }),
        ("Purple", new[] { 128,   0, 255 }),
        ("Pink",   new[] { 255,   0, 128 }),
        ("White",  new[] { 255, 255, 255 }),
    ];

    private CommandContextItem ColorPreset(HaEntity entity, string name, int[] rgb) =>
        new(new CallServiceCommand(
            _client,
            domain: "light",
            service: "turn_on",
            entityId: entity.EntityId,
            displayName: name,
            icon: Icons.Brightness,
            extraData: new Dictionary<string, object?> { ["rgb_color"] = rgb },
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

    private CommandContextItem CoverTiltPreset(HaEntity entity, int position) =>
        new(new CallServiceCommand(
            _client,
            domain: "cover",
            service: "set_cover_tilt_position",
            entityId: entity.EntityId,
            displayName: $"{position}%",
            icon: position == 0 ? Icons.Close : (position == 100 ? Icons.Open : Icons.Stop),
            extraData: new Dictionary<string, object?> { ["tilt_position"] = position },
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

    private CommandContextItem SwingModeItem(HaEntity entity, string mode) =>
        new(new CallServiceCommand(
            _client,
            domain: "climate",
            service: "set_swing_mode",
            entityId: entity.EntityId,
            displayName: mode,
            extraData: new Dictionary<string, object?> { ["swing_mode"] = mode },
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

            // RGB color picker — only when the bulb declares an RGB-capable
            // colour mode. HA exposes capability via `supported_color_modes`
            // (a list of strings). Modes that accept rgb_color: rgb, rgbw,
            // rgbww, hs, xy. If the attribute is missing, skip the menu —
            // older single-channel bulbs would error on rgb_color.
            if (entity.Attributes.TryGetValue("supported_color_modes", out var modes)
                && modes is List<object?> modeList
                && modeList.OfType<string>().Any(m =>
                    m is "rgb" or "rgbw" or "rgbww" or "hs" or "xy"))
            {
                var colorItems = ColorPalette
                    .Select(c => (IContextItem)ColorPreset(entity, c.Name, c.Rgb))
                    .ToArray();
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set color…",
                    Icon = Icons.Brightness,
                    MoreCommands = colorItems,
                });
            }
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

            // supported_features bits on media_player. Values from HA's
            // media_player component:
            //   1 PAUSE, 2 SEEK, 4 VOLUME_SET, 8 VOLUME_MUTE, 16 PREVIOUS_TRACK,
            //   32 NEXT_TRACK, 128 TURN_ON, 256 TURN_OFF, 512 PLAY_MEDIA,
            //   1024 VOLUME_STEP, 2048 SELECT_SOURCE, 4096 STOP, 16384 PLAY,
            //   32768 SHUFFLE_SET, 65536 SELECT_SOUND_MODE, 262144 REPEAT_SET.
            var mpsf = entity.Attributes.TryGetValue("supported_features", out var sfo2) && sfo2 is long mpb ? mpb : -1;
            bool MpHas(long bit) => mpsf < 0 || (mpsf & bit) == bit;

            // Volume presets — only when the entity supports volume_set.
            if (MpHas(4))
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

            // Shuffle toggle — flip the current `shuffle` bool. Skip when the
            // attribute is missing (some players publish only when supported).
            if (MpHas(32768) && entity.Attributes.TryGetValue("shuffle", out var sh) && sh is bool isShuffling)
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(
                        _client,
                        domain: "media_player",
                        service: "shuffle_set",
                        entityId: entity.EntityId,
                        displayName: isShuffling ? $"Disable shuffle on {entity.FriendlyName}" : $"Enable shuffle on {entity.FriendlyName}",
                        icon: Icons.PlayPause,
                        extraData: new Dictionary<string, object?> { ["shuffle"] = !isShuffling },
                        onSuccess: OnServiceCallSucceeded)));
            }

            // Repeat submenu — fixed set of options the HA service accepts.
            if (MpHas(262144))
            {
                var repeatOptions = new[] { "off", "one", "all" };
                var repeatItems = repeatOptions
                    .Select(r => (IContextItem)new CommandContextItem(new CallServiceCommand(
                        _client, "media_player", "repeat_set", entity.EntityId,
                        r, extraData: new Dictionary<string, object?> { ["repeat"] = r },
                        onSuccess: OnServiceCallSucceeded)))
                    .ToArray();
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set repeat…",
                    Icon = Icons.PlayPause,
                    MoreCommands = repeatItems,
                });
            }

            // Source submenu — populated from the entity's `source_list`.
            if (MpHas(2048) && entity.Attributes.TryGetValue("source_list", out var sl) && sl is List<object?> sources)
            {
                var sourceItems = sources
                    .OfType<string>()
                    .Select(s => (IContextItem)new CommandContextItem(new CallServiceCommand(
                        _client, "media_player", "select_source", entity.EntityId,
                        s, extraData: new Dictionary<string, object?> { ["source"] = s },
                        onSuccess: OnServiceCallSucceeded)))
                    .ToArray();
                if (sourceItems.Length > 0)
                {
                    items.Add(new CommandContextItem(new NoOpCommand())
                    {
                        Title = "Select source…",
                        Icon = Icons.Volume,
                        MoreCommands = sourceItems,
                    });
                }
            }

            // Sound mode submenu — same shape, gated on SELECT_SOUND_MODE.
            if (MpHas(65536) && entity.Attributes.TryGetValue("sound_mode_list", out var sml) && sml is List<object?> soundModes)
            {
                var soundItems = soundModes
                    .OfType<string>()
                    .Select(s => (IContextItem)new CommandContextItem(new CallServiceCommand(
                        _client, "media_player", "select_sound_mode", entity.EntityId,
                        s, extraData: new Dictionary<string, object?> { ["sound_mode"] = s },
                        onSuccess: OnServiceCallSucceeded)))
                    .ToArray();
                if (soundItems.Length > 0)
                {
                    items.Add(new CommandContextItem(new NoOpCommand())
                    {
                        Title = "Select sound mode…",
                        Icon = Icons.Volume,
                        MoreCommands = soundItems,
                    });
                }
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

            // Swing mode submenu — same pattern, using swing_modes attribute.
            if (entity.Attributes.TryGetValue("swing_modes", out var sw) && sw is List<object?> swingModes)
            {
                var modeItems = swingModes
                    .OfType<string>()
                    .Select(m => (IContextItem)SwingModeItem(entity, m))
                    .ToArray();
                if (modeItems.Length > 0)
                {
                    items.Add(new CommandContextItem(new NoOpCommand())
                    {
                        Title = "Set swing mode…",
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

            // Cover supported_features bits (HA CoverEntityFeature):
            //   1 OPEN, 2 CLOSE, 4 SET_POSITION, 8 STOP, 16 OPEN_TILT,
            //   32 CLOSE_TILT, 64 STOP_TILT, 128 SET_TILT_POSITION.
            var coverSf = entity.Attributes.TryGetValue("supported_features", out var sf) && sf is long bits ? bits : -1;
            bool CoverHas(long bit) => coverSf < 0 || (coverSf & bit) == bit;

            // Position presets only when the cover supports set_cover_position.
            if (CoverHas(4))
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

            // Tilt position presets when the cover supports set_cover_tilt_position.
            if (CoverHas(128))
            {
                var presets = new IContextItem[]
                {
                    CoverTiltPreset(entity, 0),
                    CoverTiltPreset(entity, 25),
                    CoverTiltPreset(entity, 50),
                    CoverTiltPreset(entity, 75),
                    CoverTiltPreset(entity, 100),
                };
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set tilt…",
                    Icon = Icons.Stop,
                    MoreCommands = presets,
                });
            }
        }

        if (entity.Domain == "update")
        {
            // supported_features bits (HA UpdateEntityFeature):
            //   1 INSTALL, 2 SPECIFIC_VERSION, 4 PROGRESS, 8 BACKUP, 16 RELEASE_NOTES.
            var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
            bool Has(long bit) => sf < 0 || (sf & bit) == bit;

            // Updates are a binary state: state="on" means an update is
            // available. in_progress is either a bool or a numeric percent
            // while installing; either non-false form means an install is
            // already running and Install should be hidden.
            var available = entity.IsOn;
            var inProgress = entity.Attributes.TryGetValue("in_progress", out var ipo)
                && ipo is not (null or false);

            if (available && !inProgress && Has(1))
            {
                // Always request a backup when the integration supports it —
                // matches Raycast's default and is the safer choice. Without
                // BACKUP support the param is dropped (HA would 400 on it).
                IReadOnlyDictionary<string, object?>? extra = Has(8)
                    ? new Dictionary<string, object?> { ["backup"] = true }
                    : null;
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "update", "install", entity.EntityId,
                        $"Install {entity.FriendlyName}", icon: Icons.Play,
                        extraData: extra,
                        onSuccess: OnServiceCallSucceeded)));
            }

            if (available)
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "update", "skip", entity.EntityId,
                        $"Skip {entity.FriendlyName}", icon: Icons.Next,
                        onSuccess: OnServiceCallSucceeded)));
            }

            if (entity.Attributes.TryGetValue("release_url", out var ru) && ru is string rus && !string.IsNullOrEmpty(rus))
            {
                items.Add(new CommandContextItem(new OpenUrlCommand(rus) { Name = "Open release notes" }));
            }
        }

        if (entity.Domain == "input_select")
        {
            // "Select option…" submenu — every option except the current
            // state. Hidden if the entity has no options or is unavailable.
            if (!string.Equals(entity.State, "unavailable", System.StringComparison.OrdinalIgnoreCase)
                && entity.Attributes.TryGetValue("options", out var opts) && opts is List<object?> options)
            {
                var subItems = options
                    .OfType<string>()
                    .Where(o => !string.Equals(o, entity.State, System.StringComparison.Ordinal))
                    .Select(o => (IContextItem)new CommandContextItem(new CallServiceCommand(
                        _client, "input_select", "select_option", entity.EntityId,
                        o, extraData: new Dictionary<string, object?> { ["option"] = o },
                        onSuccess: OnServiceCallSucceeded)))
                    .ToArray();
                if (subItems.Length > 0)
                {
                    items.Add(new CommandContextItem(new NoOpCommand())
                    {
                        Title = "Select option…",
                        MoreCommands = subItems,
                    });
                }
            }
        }

        if (entity.Domain == "input_number")
        {
            // input_number.increment / decrement use the entity's own step;
            // we only need to gate the action when the result would clamp.
            var hasValue = double.TryParse(entity.State,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v);
            var hasMin = TryGetDouble(entity.Attributes, "min", out var min);
            var hasMax = TryGetDouble(entity.Attributes, "max", out var max);
            var hasStep = TryGetDouble(entity.Attributes, "step", out var step);

            if (hasValue && hasStep && (!hasMax || v + step <= max))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "input_number", "increment", entity.EntityId,
                        $"Increase {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            }
            if (hasValue && hasStep && (!hasMin || v - step >= min))
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "input_number", "decrement", entity.EntityId,
                        $"Decrease {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            }
        }

        if (entity.Domain == "timer")
        {
            // Mirror Raycast: only act on `editable` timers (HA exposes
            // start/pause/cancel for code-defined timers too, but they
            // round-trip via the YAML config — Raycast hides those.)
            var editable = entity.Attributes.TryGetValue("editable", out var ed) && ed is bool eb && eb;
            if (editable)
            {
                var isActive = string.Equals(entity.State, "active", System.StringComparison.OrdinalIgnoreCase);
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "timer", "start", entity.EntityId,
                        isActive ? $"Restart {entity.FriendlyName}" : $"Start {entity.FriendlyName}",
                        icon: Icons.Play, onSuccess: OnServiceCallSucceeded)));
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "timer", "pause", entity.EntityId,
                        $"Pause {entity.FriendlyName}", icon: Icons.Pause, onSuccess: OnServiceCallSucceeded)));
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "timer", "cancel", entity.EntityId,
                        $"Cancel {entity.FriendlyName}", icon: Icons.Stop, onSuccess: OnServiceCallSucceeded)));
                items.Add(new CommandContextItem(
                    new CallServiceCommand(_client, "timer", "finish", entity.EntityId,
                        $"Finish {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            }
        }

        if (entity.Domain == "counter")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "counter", "increment", entity.EntityId,
                    $"Increment {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "counter", "decrement", entity.EntityId,
                    $"Decrement {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "counter", "reset", entity.EntityId,
                    $"Reset {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
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

    private string BuildSubtitle(HaEntity entity)
    {
        // Power users wiring up automations want to see entity_id; the
        // Show Entity IDs setting swaps it in. Default mirrors Raycast:
        // area (room) name only — state lives in the tags.
        if (_settings.ShowEntityId)
        {
            return entity.EntityId;
        }
        return entity.AreaName ?? string.Empty;
    }

    private Tag[] BuildTags(HaEntity entity)
    {
        // Hide the domain tag on single-domain pages (Lights, Covers, ...) —
        // it's redundant. Keep it on All Entities and multi-domain pages
        // (Buttons, Helpers) so users can tell entities apart.
        var showDomainTag = _domains is null || _domains.Count > 1;
        var tags = new List<Tag>(2);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "media_player" or "binary_sensor" or "cover" or "update")
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
