using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;

namespace HomeAssistantCommandPalette.Services;

/// <summary>
/// Transport-agnostic client surface for the Home Assistant data and
/// command operations the extension needs. The pages depend on this
/// interface rather than a concrete implementation so we can swap
/// transports (REST polling today, WebSocket subscription later) and
/// stand up demo / test doubles without rewriting page code.
/// </summary>
public interface IHaClient : IDisposable
{
    /// <summary>
    /// Snapshot of every entity's current state, with area names stitched
    /// in when available. Implementations decide whether the snapshot is
    /// served from a per-call REST fetch, an in-memory cache, or a
    /// long-lived push subscription.
    /// </summary>
    HaQueryResult GetStates();

    bool TryCallService(string domain, string service, string entityId, out string errorMessage);

    bool TryCallService(
        string domain,
        string service,
        string entityId,
        IReadOnlyDictionary<string, object?>? extraData,
        out string errorMessage);

    string? GetCameraSnapshotPath(string entityId);

    string? GetEntityPicturePath(string entityId, string entityPicture);

    HaCalendarsResult GetCalendars();

    IReadOnlyList<HaCalendarEvent> GetCalendarEvents(HaCalendar calendar, DateTimeOffset start, DateTimeOffset endTime);

    HaAssistResult AskAssist(string text);

    HaWeatherForecastResult GetWeatherForecast(string entityId);

    IReadOnlyList<HaHistoryPoint> GetHistory(string entityId, DateTimeOffset since);

    HaConfigProbe ProbeConfig();

    /// <summary>
    /// Diagnostic for the most recent area-resolution attempt. -1 means
    /// the last attempt failed or hasn't run; 0 means the instance has no
    /// area assignments; positive values are the count of mapped entities.
    /// Surfaced by Connection Check.
    /// </summary>
    int LastAreaCount { get; }

    /// <summary>
    /// Last error message from the area-resolution path, or empty when
    /// the most recent attempt succeeded.
    /// </summary>
    string LastAreaError { get; }

    /// <summary>
    /// Raised when the underlying state snapshot mutates. Argument is the
    /// entity_id that changed, or <c>null</c> for a full reset (initial
    /// hydration, reconnect). Page consumers filter by their domain set
    /// before kicking <c>RaiseItemsChanged</c>; without filtering, every
    /// background state_changed would re-render every open page and reset
    /// the user's selection. The REST-only fallback path never fires.
    /// </summary>
    event Action<string?> StateChanged;

    /// <summary>
    /// True when state is being pushed live (WS hydrated). Pages use this
    /// to skip the post-service-call REST refresh — when WS is live, the
    /// state_changed event does the same job and double-refreshing causes
    /// visible flicker.
    /// </summary>
    bool IsLive { get; }
}
