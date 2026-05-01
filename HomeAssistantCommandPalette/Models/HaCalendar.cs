using System;
using System.Collections.Generic;

namespace HomeAssistantCommandPalette.Models;

public sealed record HaCalendar(string EntityId, string Name);

public sealed record HaCalendarEvent(
    string CalendarEntityId,
    string CalendarName,
    string Summary,
    DateTimeOffset Start,
    DateTimeOffset End,
    bool AllDay,
    string? Description,
    string? Location);

/// <summary>
/// Result of <c>GET /api/calendars</c>. Mirrors <see cref="HaQueryResult"/>
/// so the calendar page can render auth/network errors using the same
/// "press Enter to open settings" UX.
/// </summary>
public sealed class HaCalendarsResult
{
    public IReadOnlyList<HaCalendar> Calendars { get; init; } = Array.Empty<HaCalendar>();
    public HaErrorKind ErrorKind { get; init; }
    public string ErrorTitle { get; init; } = string.Empty;
    public string ErrorDescription { get; init; } = string.Empty;
    public bool HasError => ErrorKind != HaErrorKind.None;
}
