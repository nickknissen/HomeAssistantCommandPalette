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
        Icon = Icons.InputDate;
        Title = "Calendar";
        Name = "Open";
        Id = "ha.calendar";
        ShowDetails = true;
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
        var now = DateTimeOffset.Now;
        var subtitle = $"{ev.CalendarName} · {FormatWhen(ev, now)}";

        var details = new List<IDetailsElement>
        {
            Row("Calendar", ev.CalendarName),
            Row("Starts", FormatTimestamp(ev.Start, ev.AllDay)),
            Row("Ends", FormatTimestamp(ev.End, ev.AllDay)),
        };
        if (!string.IsNullOrEmpty(ev.Location)) details.Add(Row("Location", ev.Location));
        if (!string.IsNullOrEmpty(ev.Description)) details.Add(Row("Description", ev.Description));

        return new ListItem(new CopyTextCommand(ev.Summary) { Name = "Copy event title" })
        {
            Title = ev.Summary,
            Subtitle = subtitle,
            Icon = Icons.InputDate,
            Details = new Details { Title = ev.Summary, Metadata = details.ToArray() },
        };
    }

    // Compact "when" — lean on relative phrasing for near-term events
    // (Today / Tomorrow), drop down to weekday + clock for events later
    // this week, and use a fully-qualified date for anything beyond.
    private static string FormatWhen(HaCalendarEvent ev, DateTimeOffset now)
    {
        var local = ev.Start.ToLocalTime();
        var today = DateTime.Today;
        var date = local.Date;
        var dayLabel = date == today ? "Today"
            : date == today.AddDays(1) ? "Tomorrow"
            : date < today.AddDays(7) ? local.ToString("dddd", CultureInfo.CurrentCulture)
            : local.ToString("ddd, MMM d", CultureInfo.CurrentCulture);

        if (ev.AllDay) return $"{dayLabel} · all-day";
        return $"{dayLabel} · {local.ToString("t", CultureInfo.CurrentCulture)}";
    }

    private static string FormatTimestamp(DateTimeOffset ts, bool allDay)
    {
        var local = ts.ToLocalTime();
        return allDay
            ? local.ToString("dddd, MMM d", CultureInfo.CurrentCulture)
            : local.ToString("dddd, MMM d · t", CultureInfo.CurrentCulture);
    }

    private static DetailsElement Row(string key, string value) => new()
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };
}
