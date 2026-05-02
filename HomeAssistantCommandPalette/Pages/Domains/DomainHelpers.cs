using System;
using System.Collections.Generic;
using System.Globalization;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Internal-use formatting and attribute helpers shared by
/// <see cref="DomainBehavior"/> subclasses and
/// <c>EntityListPage.CreateItem</c>.
/// </summary>
internal static class DomainHelpers
{
    public static IDetailsElement Row(string key, string value) => new DetailsElement
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };

    /// <summary>
    /// Renders a duration in whole minutes as <c>Hh Mm</c> (or <c>Hh</c>
    /// when minutes are zero, or <c>Mm</c> when under an hour).
    /// </summary>
    public static string FormatMinutes(long minutes)
    {
        if (minutes < 60) return $"{minutes}m";
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0 ? $"{h}h" : $"{h}h {m}m";
    }

    /// <summary>
    /// Tolerant double extractor for HA attributes: integers come back as
    /// <c>long</c> after JSON deserialization, so try both.
    /// </summary>
    public static bool TryGetDouble(IReadOnlyDictionary<string, object?> attrs, string key, out double value)
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

    /// <summary>
    /// Renders the entity's state with its unit suffix when present
    /// (e.g. "23.5 °C"). Falls back to "(no state)" for empty states.
    /// </summary>
    public static string FormatStateWithUnit(HaEntity entity)
    {
        var state = string.IsNullOrEmpty(entity.State) ? "(no state)" : entity.State;
        if (entity.Attributes.TryGetValue("unit_of_measurement", out var u) && u is string unit && !string.IsNullOrEmpty(unit))
        {
            return $"{state} {unit}";
        }
        return state;
    }

    /// <summary>
    /// Compact relative-time format used by the "Last changed" row.
    /// Future timestamps and ages over a week fall back to a fixed
    /// yyyy-MM-dd HH:mm date so the UI doesn't render nonsense.
    /// </summary>
    public static string FormatRelativeTime(DateTimeOffset when)
    {
        var diff = DateTimeOffset.UtcNow - when;
        if (diff.TotalSeconds < 0) return when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return when.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Appends the page-level common rows (Area / Last changed /
    /// Attribution / Entity ID) — the universal footer of every entity's
    /// details pane.
    /// </summary>
    public static void AppendCommonRows(HaEntity entity, List<IDetailsElement> rows)
    {
        if (!string.IsNullOrEmpty(entity.AreaName))
        {
            rows.Add(Row("Area", entity.AreaName));
        }

        if (entity.LastChanged is DateTimeOffset changed)
        {
            rows.Add(Row("Last changed", FormatRelativeTime(changed)));
        }

        if (entity.Attributes.TryGetValue("attribution", out var att) && att is string atts && !string.IsNullOrEmpty(atts))
        {
            rows.Add(Row("Attribution", atts));
        }

        rows.Add(Row("Entity ID", entity.EntityId));
    }
}
