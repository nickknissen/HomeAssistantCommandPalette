using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;

namespace HomeAssistantCommandPalette.Services;

/// <summary>
/// Demo implementation of <see cref="IHaClient"/>. Returns the canned
/// fixture from <see cref="DemoHaData"/> for state queries; everything
/// else is a no-op success / empty result so Microsoft Store screenshots
/// can navigate the full extension surface without touching a real
/// Home Assistant instance.
/// </summary>
internal sealed partial class DemoHaClient : IHaClient
{
    public int LastAreaCount => 0;
    public string LastAreaError => string.Empty;

    // Demo data is static — nothing pushes updates, so no subscriber will
    // ever fire. Declared as add/remove so the compiler doesn't warn about
    // an event field that's never assigned.
    public event Action<string?> StateChanged { add { } remove { } }

    public bool IsLive => false;

    public HaQueryResult GetStates() => DemoHaData.Result();

    public bool TryCallService(string domain, string service, string entityId, out string error)
        => TryCallService(domain, service, entityId, extraData: null, out error);

    public bool TryCallService(
        string domain,
        string service,
        string entityId,
        IReadOnlyDictionary<string, object?>? extraData,
        out string error)
    {
        error = string.Empty;
        // Pretend the call succeeded; the demo screenshots only need the
        // dispatch to look like it worked.
        return true;
    }

    public string? GetCameraSnapshotPath(string entityId) => null;

    public string? GetEntityPicturePath(string entityId, string entityPicture) => null;

    public HaCalendarsResult GetCalendars() => new();

    public IReadOnlyList<HaCalendarEvent> GetCalendarEvents(HaCalendar calendar, DateTimeOffset start, DateTimeOffset end)
        => Array.Empty<HaCalendarEvent>();

    public HaAssistResult AskAssist(string text)
        => new(true, "Demo mode — Assist is offline.", "demo", null);

    public IReadOnlyList<HaHistoryPoint> GetHistory(string entityId, DateTimeOffset since)
        => Array.Empty<HaHistoryPoint>();

    public HaWeatherForecastResult GetWeatherForecast(string entityId)
    {
        var now = DateTimeOffset.Now;
        return new HaWeatherForecastResult
        {
            Success = true,
            Hourly = new[]
            {
                new HaWeatherForecast { Time = now.AddHours(1), Condition = "sunny", Temperature = 21, PrecipitationProbability = 5 },
                new HaWeatherForecast { Time = now.AddHours(2), Condition = "partlycloudy", Temperature = 20, PrecipitationProbability = 10 },
                new HaWeatherForecast { Time = now.AddHours(3), Condition = "rainy", Temperature = 18, PrecipitationProbability = 60 },
            },
            Daily = new[]
            {
                new HaWeatherForecast { Time = now.Date, Condition = "sunny", Temperature = 22, Templow = 14, PrecipitationProbability = 5 },
                new HaWeatherForecast { Time = now.Date.AddDays(1), Condition = "rainy", Temperature = 18, Templow = 12, PrecipitationProbability = 70 },
            },
        };
    }

    public HaConfigProbe ProbeConfig()
        => new(true, HaErrorKind.None, null, "demo", "Demo Home", "UTC", "RUNNING", 0);

    public void Dispose() { }
}
