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
}
