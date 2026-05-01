using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Flat upcoming-events page across every configured HA calendar. Picks
/// up calendars from <c>GET /api/calendars</c> and pulls each one's
/// events for the next 7 days, sorted chronologically.
/// </summary>
internal sealed partial class CalendarPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly HaApiClient _client;

    // 7 days lines up with how a typical calendar UI renders an at-a-glance
    // upcoming view. Far enough to plan around, short enough that a noisy
    // calendar doesn't overwhelm the list.
    private static readonly TimeSpan LookaheadWindow = TimeSpan.FromDays(7);

    public CalendarPage(HaSettings settings, HaApiClient client)
    {
        _settings = settings;
        _client = client;
        Icon = Icons.App;
        Title = "Calendar";
        Name = "Open";
        Id = "ha.calendar";
        // Off by default — users can toggle the details pane on; the
        // single-line title already carries the time range and summary.
        ShowDetails = false;
        PlaceholderText = "Search upcoming events";
    }

    public override IListItem[] GetItems()
    {
        var result = _client.GetCalendars();
        if (result.HasError)
        {
            var openSettings = (ICommand)_settings.Settings.SettingsPage;
            ICommand errorCommand = result.ErrorKind switch
            {
                HaErrorKind.NotConfigured or HaErrorKind.Unauthorized or HaErrorKind.InvalidUrl => openSettings,
                _ => new NoOpCommand(),
            };
            var subtitle = result.ErrorKind switch
            {
                HaErrorKind.NotConfigured => "Press Enter to open settings and add your URL + access token.",
                HaErrorKind.Unauthorized => "Press Enter to open settings and update your access token.",
                HaErrorKind.InvalidUrl => "Press Enter to open settings and fix the URL.",
                _ => result.ErrorDescription,
            };
            return [new ListItem(errorCommand) { Title = result.ErrorTitle, Subtitle = subtitle }];
        }

        if (result.Calendars.Count == 0)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = "No calendars configured",
                Subtitle = "Add a calendar integration in Home Assistant to see upcoming events here.",
            }];
        }

        var now = DateTimeOffset.Now;
        var end = now + LookaheadWindow;
        var events = new List<HaCalendarEvent>();
        foreach (var cal in result.Calendars)
        {
            // Per-calendar errors are silently swallowed by GetCalendarEvents
            // so a single broken calendar doesn't take the page down.
            events.AddRange(_client.GetCalendarEvents(cal, now, end));
        }
        events.Sort((a, b) => a.Start.CompareTo(b.Start));

        if (events.Count == 0)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = "No upcoming events",
                Subtitle = $"Nothing scheduled across {result.Calendars.Count} calendar{(result.Calendars.Count == 1 ? string.Empty : "s")} in the next 7 days.",
            }];
        }

        return events.Select(BuildItem).ToArray();
    }

    private static ListItem BuildItem(HaCalendarEvent ev)
    {
        var details = new List<IDetailsElement>
        {
            Row("Calendar", ev.CalendarName),
            Row("Starts", FormatTimestamp(ev.Start, ev.AllDay)),
            Row("Ends", FormatTimestamp(ev.End, ev.AllDay)),
        };
        if (!string.IsNullOrEmpty(ev.Location)) details.Add(Row("Location", ev.Location));
        if (!string.IsNullOrEmpty(ev.Description)) details.Add(Row("Description", ev.Description));

        var (bg, fg) = ColorForCalendar(ev.CalendarEntityId);

        return new ListItem(new CopyTextCommand(ev.Summary) { Name = "Copy event title" })
        {
            // Raycast-style "<from> - <to> | <title>" so the time range is
            // the first thing the eye lands on. Day label moves to subtitle
            // (we don't have List sections to group by day yet).
            Title = $"{FormatTimeRange(ev)} | {ev.Summary}",
            Subtitle = FormatDayLabel(ev.Start),
            Icon = Icons.InputDate,
            Tags = [new Tag(ev.CalendarName) { ToolTip = "Calendar", Background = bg, Foreground = fg }],
            Details = new Details { Title = ev.Summary, Metadata = details.ToArray() },
        };
    }

    // Stable palette of background colours used for the calendar tag.
    // Chosen to be visually distinct on dark + light themes with white
    // text. Order doesn't matter — each calendar entity_id is hashed into
    // a slot, so the same calendar always gets the same colour even if
    // calendars are added or reordered.
    private static readonly OptionalColor[] CalendarColors =
    [
        ColorHelpers.FromArgb(255,  76, 161, 222), // blue
        ColorHelpers.FromArgb(255,  76, 175,  80), // green
        ColorHelpers.FromArgb(255, 244,  67,  54), // red
        ColorHelpers.FromArgb(255, 255, 152,   0), // orange
        ColorHelpers.FromArgb(255, 156,  39, 176), // purple
        ColorHelpers.FromArgb(255,   0, 150, 136), // teal
        ColorHelpers.FromArgb(255, 233,  30,  99), // pink
        ColorHelpers.FromArgb(255,  63,  81, 181), // indigo
        ColorHelpers.FromArgb(255, 121,  85,  72), // brown
    ];

    private static readonly OptionalColor CalendarTagForeground = ColorHelpers.FromRgb(255, 255, 255);

    private static (OptionalColor Background, OptionalColor Foreground) ColorForCalendar(string entityId)
    {
        // FNV-1a 32-bit — small, deterministic, no allocation, doesn't
        // depend on the runtime's randomized string-hash seed (which would
        // re-shuffle colours on every CmdPal restart).
        const uint offset = 2166136261;
        const uint prime = 16777619;
        uint h = offset;
        foreach (var c in entityId)
        {
            h ^= c;
            h *= prime;
        }
        var idx = (int)(h % (uint)CalendarColors.Length);
        return (CalendarColors[idx], CalendarTagForeground);
    }

    /// <summary>
    /// "All Day" / "10:00 - 11:00" / "10:00 - Tue 09:00" — the time-range
    /// piece that prefixes the event title. Mirrors Raycast's
    /// humanEventTimeRange but drops down to a date prefix on the end side
    /// when the event crosses days, instead of falling back to raw ISO.
    /// </summary>
    private static string FormatTimeRange(HaCalendarEvent ev)
    {
        if (ev.AllDay) return "All Day";

        var startLocal = ev.Start.ToLocalTime();
        var endLocal = ev.End.ToLocalTime();
        var startTime = startLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        var endTime = endLocal.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (startLocal.Date == endLocal.Date)
        {
            return $"{startTime} - {endTime}";
        }
        // Cross-day: prefix the end with its day so the range stays readable.
        return $"{startTime} - {FormatShortDay(endLocal)} {endTime}";
    }

    private static string FormatDayLabel(DateTimeOffset start)
    {
        var local = start.ToLocalTime();
        var today = DateTime.Today;
        var date = local.Date;
        return date == today ? "Today"
            : date == today.AddDays(1) ? "Tomorrow"
            : date < today.AddDays(7) ? local.ToString("dddd", CultureInfo.CurrentCulture)
            : local.ToString("ddd, MMM d", CultureInfo.CurrentCulture);
    }

    private static string FormatShortDay(DateTimeOffset ts)
    {
        var local = ts.LocalDateTime;
        var today = DateTime.Today;
        if (local.Date == today) return "Today";
        if (local.Date == today.AddDays(1)) return "Tomorrow";
        return local.ToString("ddd", CultureInfo.CurrentCulture);
    }

    private static string FormatTimestamp(DateTimeOffset ts, bool allDay)
    {
        var local = ts.ToLocalTime();
        return allDay
            ? local.ToString("dddd, MMM d", CultureInfo.CurrentCulture)
            : local.ToString("dddd, MMM d · HH:mm", CultureInfo.CurrentCulture);
    }

    private static DetailsElement Row(string key, string value) => new()
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };
}
