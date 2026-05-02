using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class WeatherBehavior : DomainBehavior
{
    private static readonly string[] Compass16 =
    [
        "N", "NNE", "NE", "ENE",
        "E", "ESE", "SE", "SSE",
        "S", "SSW", "SW", "WSW",
        "W", "WNW", "NW", "NNW",
    ];

    public override string Domain => "weather";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        var unavailable = string.Equals(ctx.Entity.State, "unavailable", System.StringComparison.OrdinalIgnoreCase);
        return Icons.WeatherForCondition(ctx.Entity.State, unavailable);
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        // HA reports the temperature unit on the weather entity itself
        // (°C / °F). Pressure / wind / visibility / precipitation each
        // carry their own *_unit; fall back to a sensible default if the
        // integration omits one.
        var tempUnit = (entity.Attributes.TryGetValue("temperature_unit", out var tu) && tu is string tus && !string.IsNullOrEmpty(tus)) ? tus : "°";

        if (DomainHelpers.TryGetDouble(entity.Attributes, "temperature", out var temp))
            rows.Add(DomainHelpers.Row("Temperature", $"{FormatNum(temp)} {tempUnit}"));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "apparent_temperature", out var feels))
            rows.Add(DomainHelpers.Row("Feels like", $"{FormatNum(feels)} {tempUnit}"));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "dew_point", out var dew))
            rows.Add(DomainHelpers.Row("Dew point", $"{FormatNum(dew)} {tempUnit}"));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "humidity", out var hum))
            rows.Add(DomainHelpers.Row("Humidity", $"{(int)System.Math.Round(hum)}%"));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "pressure", out var pres))
        {
            var unit = (entity.Attributes.TryGetValue("pressure_unit", out var pu) && pu is string pus && !string.IsNullOrEmpty(pus)) ? pus : "hPa";
            rows.Add(DomainHelpers.Row("Pressure", $"{FormatNum(pres)} {unit}"));
        }
        // Wind speed + bearing as a single row — bearing alone is hard to
        // read; speed alone is missing the direction. e.g. "12 km/h NW (315°)".
        if (DomainHelpers.TryGetDouble(entity.Attributes, "wind_speed", out var wind))
        {
            var unit = (entity.Attributes.TryGetValue("wind_speed_unit", out var wu) && wu is string wus && !string.IsNullOrEmpty(wus)) ? wus : "m/s";
            var label = $"{FormatNum(wind)} {unit}";
            if (DomainHelpers.TryGetDouble(entity.Attributes, "wind_bearing", out var bearing))
            {
                label = $"{label} {CompassFromBearing(bearing)} ({(int)System.Math.Round(bearing)}°)";
            }
            rows.Add(DomainHelpers.Row("Wind", label));
        }
        if (DomainHelpers.TryGetDouble(entity.Attributes, "wind_gust_speed", out var gust))
        {
            var unit = (entity.Attributes.TryGetValue("wind_speed_unit", out var wu) && wu is string wus && !string.IsNullOrEmpty(wus)) ? wus : "m/s";
            rows.Add(DomainHelpers.Row("Wind gust", $"{FormatNum(gust)} {unit}"));
        }
        if (DomainHelpers.TryGetDouble(entity.Attributes, "visibility", out var vis))
        {
            var unit = (entity.Attributes.TryGetValue("visibility_unit", out var vu) && vu is string vus && !string.IsNullOrEmpty(vus)) ? vus : "km";
            rows.Add(DomainHelpers.Row("Visibility", $"{FormatNum(vis)} {unit}"));
        }
        if (DomainHelpers.TryGetDouble(entity.Attributes, "cloud_coverage", out var cloud))
            rows.Add(DomainHelpers.Row("Cloud cover", $"{(int)System.Math.Round(cloud)}%"));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "uv_index", out var uv))
            rows.Add(DomainHelpers.Row("UV index", FormatNum(uv)));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "ozone", out var ozone))
            rows.Add(DomainHelpers.Row("Ozone", $"{FormatNum(ozone)} DU"));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "precipitation", out var precip))
        {
            var unit = (entity.Attributes.TryGetValue("precipitation_unit", out var pcu) && pcu is string pcus && !string.IsNullOrEmpty(pcus)) ? pcus : "mm";
            rows.Add(DomainHelpers.Row("Precipitation", $"{FormatNum(precip)} {unit}"));
        }
    }

    /// <summary>
    /// Maps a 0..360° bearing to a 16-point compass label
    /// (N / NNE / NE / ENE / … / NNW). Each sector spans 22.5°,
    /// centred on the canonical bearing.
    /// </summary>
    public static string CompassFromBearing(double degrees)
    {
        var n = ((degrees % 360) + 360) % 360;
        var idx = (int)System.Math.Round(n / 22.5) % 16;
        return Compass16[idx];
    }

    private static string FormatNum(double v) =>
        v == System.Math.Floor(v)
            ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
