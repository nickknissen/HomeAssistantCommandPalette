using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantCommandPalette.Services;

// DTOs for the Home Assistant REST surface. Source-gen via HaJsonContext
// keeps deserialization trim/AOT-safe — see HomeAssistantCommandPalette.csproj.

internal sealed class HaStateDto
{
    [JsonPropertyName("entity_id")] public string? EntityId { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("attributes")] public Dictionary<string, JsonElement>? Attributes { get; init; }
    [JsonPropertyName("last_changed")] public DateTimeOffset? LastChanged { get; init; }
    [JsonPropertyName("last_updated")] public DateTimeOffset? LastUpdated { get; init; }
}

internal sealed class HaCalendarDto
{
    [JsonPropertyName("entity_id")] public string? EntityId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
}

internal sealed class HaCalendarEventDto
{
    [JsonPropertyName("summary")] public string? Summary { get; init; }
    [JsonPropertyName("start")] public CalendarTime? Start { get; init; }
    [JsonPropertyName("end")] public CalendarTime? End { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("location")] public string? Location { get; init; }
}

[JsonConverter(typeof(CalendarTimeConverter))]
internal sealed class CalendarTime
{
    public DateTimeOffset Value { get; init; }
    public bool AllDay { get; init; }
}

// HA has shipped two on-the-wire shapes for calendar event times:
//   1. Newer REST: a flat ISO string ("2025-01-15T19:00:00+00:00" or
//      "2025-01-15" for all-day events).
//   2. Older / WS-derived: an object {"dateTime": "..."} or {"date": "..."}.
// Returning null on bad shapes lets the caller skip the event instead of
// failing the whole list.
internal sealed class CalendarTimeConverter : JsonConverter<CalendarTime?>
{
    public override CalendarTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.String)
        {
            return Parse(reader.GetString(), forceAllDay: false);
        }
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            string? dateTime = null;
            string? date = null;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;
                var prop = reader.GetString();
                reader.Read();
                if (reader.TokenType == JsonTokenType.String)
                {
                    if (string.Equals(prop, "dateTime", StringComparison.Ordinal)) dateTime = reader.GetString();
                    else if (string.Equals(prop, "date", StringComparison.Ordinal)) date = reader.GetString();
                }
                else
                {
                    reader.Skip();
                }
            }
            if (dateTime is not null) return Parse(dateTime, forceAllDay: false);
            if (date is not null) return Parse(date, forceAllDay: true);
            return null;
        }
        return null;
    }

    public override void Write(Utf8JsonWriter writer, CalendarTime? value, JsonSerializerOptions options)
        => throw new NotSupportedException("CalendarTime is read-only.");

    private static CalendarTime? Parse(string? raw, bool forceAllDay)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        var allDay = forceAllDay || !raw.Contains('T');
        if (allDay)
        {
            if (DateTime.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                return new CalendarTime
                {
                    Value = new DateTimeOffset(d, TimeZoneInfo.Local.GetUtcOffset(d)),
                    AllDay = true,
                };
            }
            return null;
        }
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            return new CalendarTime { Value = dto, AllDay = false };
        }
        return null;
    }
}

internal sealed class HaAssistDto
{
    [JsonPropertyName("response")] public HaAssistResponseDto? Response { get; init; }
}

internal sealed class HaAssistResponseDto
{
    [JsonPropertyName("response_type")] public string? ResponseType { get; init; }
    [JsonPropertyName("speech")] public HaAssistSpeechDto? Speech { get; init; }
}

internal sealed class HaAssistSpeechDto
{
    [JsonPropertyName("plain")] public HaAssistPlainDto? Plain { get; init; }
}

internal sealed class HaAssistPlainDto
{
    [JsonPropertyName("speech")] public string? Speech { get; init; }
}

internal sealed class HaConfigDto
{
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("location_name")] public string? LocationName { get; init; }
    [JsonPropertyName("time_zone")] public string? TimeZone { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
}

[JsonSerializable(typeof(List<HaStateDto>))]
[JsonSerializable(typeof(List<HaCalendarDto>))]
[JsonSerializable(typeof(List<HaCalendarEventDto>))]
[JsonSerializable(typeof(HaAssistDto))]
[JsonSerializable(typeof(HaConfigDto))]
[JsonSerializable(typeof(List<List<string>>))]
[JsonSerializable(typeof(string))]
internal sealed partial class HaJsonContext : JsonSerializerContext;
