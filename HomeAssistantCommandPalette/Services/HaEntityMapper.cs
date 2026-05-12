using System;
using System.Collections.Generic;
using System.Text.Json;
using HomeAssistantCommandPalette.Models;

namespace HomeAssistantCommandPalette.Services;

internal static class HaEntityMapper
{
    public static HaEntity FromDto(HaStateDto dto, string? areaName = null)
    {
        var attrs = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (dto.Attributes is not null)
        {
            foreach (var (key, value) in dto.Attributes)
            {
                attrs[key] = ToObject(value);
            }
        }

        return new HaEntity
        {
            EntityId = dto.EntityId ?? string.Empty,
            State = dto.State ?? string.Empty,
            Attributes = attrs,
            LastChanged = dto.LastChanged,
            LastUpdated = dto.LastUpdated,
            AreaName = areaName,
        };
    }

    public static object? ToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => ParseJsonArray(el),
        JsonValueKind.Object => ParseJsonObject(el),
        _ => el.GetRawText(),
    };

    private static List<object?> ParseJsonArray(JsonElement el)
    {
        var items = new List<object?>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            items.Add(ToObject(item));
        }
        return items;
    }

    private static Dictionary<string, object?> ParseJsonObject(JsonElement el)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in el.EnumerateObject())
        {
            dict[prop.Name] = ToObject(prop.Value);
        }
        return dict;
    }

    public static int CompareByFriendlyName(HaEntity a, HaEntity b)
        => string.Compare(a.FriendlyName, b.FriendlyName, StringComparison.OrdinalIgnoreCase);
}
