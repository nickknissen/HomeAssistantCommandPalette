using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;

namespace HomeAssistantCommandPalette.Services;

/// <summary>
/// Hard-coded fake entities used when the project is built with the
/// DEMO_MODE define. Used for Microsoft Store screenshots — never ships
/// to end users.
/// </summary>
internal static class DemoHaData
{
    public static HaQueryResult Result() => new()
    {
        Items = new[]
        {
            Entity("light.living_room",            "on",        "Living Room"),
            Entity("light.kitchen",                "off",       "Kitchen Lights"),
            Entity("light.bedroom",                "on",        "Bedroom Lamp"),
            Entity("switch.coffee_maker",          "off",       "Coffee Maker"),
            Entity("switch.dehumidifier",          "on",        "Dehumidifier"),
            Entity("cover.garage_door",            "closed",    "Garage Door"),
            Entity("media_player.spotify",         "playing",   "Spotify"),
            Entity("scene.movie_night",            "scening",   "Movie Night"),
            Entity("script.goodnight",             "off",       "Goodnight Routine"),
            Entity("automation.morning",           "on",        "Morning Automation"),
            Entity("sensor.outside_temperature",   "12.4",      "Outside Temperature", "°C"),
            Entity("binary_sensor.front_door",     "off",       "Front Door"),
        },
    };

    private static HaEntity Entity(string entityId, string state, string friendlyName, string? unit = null)
    {
        var attrs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["friendly_name"] = friendlyName,
        };
        if (unit is not null) attrs["unit_of_measurement"] = unit;
        return new HaEntity
        {
            EntityId = entityId,
            State = state,
            Attributes = attrs,
        };
    }
}
