using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Internal-use formatting and attribute helpers shared by
/// <see cref="DomainBehavior"/> subclasses.
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
}
