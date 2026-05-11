using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.Json;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

internal sealed partial class EntityAttributesPage : ListPage
{
    private readonly HaEntity _entity;

    public EntityAttributesPage(HaEntity entity)
    {
        _entity = entity;
        Title = entity.FriendlyName;
        Name = "Show attributes";
        Icon = Icons.App;
        ShowDetails = true;
        PlaceholderText = $"Search {_entity.FriendlyName.ToLowerInvariant()} attributes";
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>
        {
            BuildRow("entity_id", _entity.EntityId),
            BuildRow("state", _entity.State),
        };

        foreach (var attr in _entity.Attributes.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            items.Add(BuildRow(attr.Key, FormatValue(attr.Value)));
        }

        if (_entity.LastChanged is { } changed)
        {
            items.Add(BuildRow("last_changed", changed.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
        }
        if (_entity.LastUpdated is { } updated)
        {
            items.Add(BuildRow("last_updated", updated.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
        }
        if (!string.IsNullOrEmpty(_entity.AreaName))
        {
            items.Add(BuildRow("area", _entity.AreaName));
        }

        return items.ToArray();
    }

    private ListItem BuildRow(string key, string value)
    {
        var copyValue = new CopyTextCommand(value) { Name = "Copy attribute value" };
        return new ListItem(copyValue)
        {
            Title = key,
            Subtitle = OneLine(value),
            Details = new Details
            {
                Title = key,
                Metadata = new IDetailsElement[]
                {
                    new DetailsElement
                    {
                        Key = "Value",
                        Data = new DetailsLink { Text = value },
                    },
                },
            },
            MoreCommands = new IContextItem[]
            {
                new CommandContextItem(copyValue),
                new CommandContextItem(new CopyTextCommand(key) { Name = "Copy attribute name" }),
                new CommandContextItem(new CopyTextCommand(_entity.EntityId) { Name = "Copy entity ID" }),
            },
        };
    }

    private static string OneLine(string value)
    {
        var oneLine = value.Replace("\r", " ").Replace("\n", " ");
        return oneLine.Length <= 160 ? oneLine : oneLine[..157] + "...";
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return "null";
        if (value is string s) return TryPrettyJson(s, out var pretty) ? pretty : s;
        if (value is bool b) return b ? "true" : "false";
        if (value is IFormattable formattable) return formattable.ToString(null, CultureInfo.InvariantCulture);
        if (value is IEnumerable enumerable)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteEnumerable(writer, enumerable);
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
        return value.ToString() ?? string.Empty;
    }

    private static bool TryPrettyJson(string raw, out string pretty)
    {
        pretty = string.Empty;
        var trimmed = raw.TrimStart();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('[')) return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                doc.RootElement.WriteTo(writer);
            }
            pretty = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void WriteEnumerable(Utf8JsonWriter writer, IEnumerable enumerable)
    {
        writer.WriteStartArray();
        foreach (var item in enumerable)
        {
            WriteValue(writer, item);
        }
        writer.WriteEndArray();
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s when TryPrettyJson(s, out _):
                using (var doc = JsonDocument.Parse(s)) doc.RootElement.WriteTo(writer);
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case IEnumerable nested:
                WriteEnumerable(writer, nested);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
