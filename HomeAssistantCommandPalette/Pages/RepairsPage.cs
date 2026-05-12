using System;
using System.Collections.Generic;
using System.Globalization;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Lists entries from Home Assistant's issue registry — the same items
/// the HA frontend shows under Settings → System → Repairs. Read-only:
/// resolving / ignoring an issue still has to happen in HA itself
/// (repairs/ignore + repairs/list_repairs_flows are dialog-oriented
/// flows we don't model here).
/// </summary>
internal sealed partial class RepairsPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly IHaClient _client;

    public RepairsPage(HaSettings settings, IHaClient client)
    {
        _settings = settings;
        _client = client;
        Icon = Icons.App;
        Title = "Repairs";
        Name = "Open";
        Id = "ha.repairs";
        ShowDetails = true;
        PlaceholderText = "Search repairs";
    }

    public override IListItem[] GetItems()
    {
        var result = _client.GetRepairs();
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

        if (result.Issues.Count == 0)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = "No repairs",
                Subtitle = "Home Assistant hasn't flagged any issues.",
                Icon = Icons.App,
            }];
        }

        var items = new List<IListItem>(result.Issues.Count);
        foreach (var issue in result.Issues)
        {
            items.Add(BuildItem(issue));
        }
        return items.ToArray();
    }

    private static ListItem BuildItem(HaRepair issue)
    {
        // Learn-more URL doubles as the primary action when present —
        // mirrors the HA Repairs dialog's main button. Without it, copying
        // the issue ID is the most useful fallback for debugging.
        ICommand primary = !string.IsNullOrEmpty(issue.LearnMoreUrl)
            ? new OpenUrlCommand(issue.LearnMoreUrl) { Name = "Open learn more" }
            : new CopyTextCommand(issue.IssueId) { Name = "Copy issue ID" };

        var ctx = new List<IContextItem>(3);
        if (!string.IsNullOrEmpty(issue.LearnMoreUrl))
        {
            ctx.Add(new CommandContextItem(new CopyTextCommand(issue.LearnMoreUrl) { Name = "Copy learn-more URL" }));
        }
        ctx.Add(new CommandContextItem(new CopyTextCommand(issue.IssueId) { Name = "Copy issue ID" }));

        var rows = new List<IDetailsElement>
        {
            Row("Domain", issue.Domain),
            Row("Severity", issue.Severity),
            Row("Issue ID", issue.IssueId),
        };
        if (!string.IsNullOrEmpty(issue.BreaksInHaVersion))
        {
            rows.Add(Row("Breaks in HA", issue.BreaksInHaVersion));
        }
        if (issue.Created is { } created)
        {
            rows.Add(Row("Reported", created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)));
        }
        if (issue.Ignored)
        {
            rows.Add(Row("Ignored", "yes"));
        }

        return new ListItem(primary)
        {
            Title = issue.Summary,
            Subtitle = issue.Domain,
            Icon = Icons.App,
            Tags = BuildTags(issue),
            MoreCommands = ctx.ToArray(),
            Details = new Details { Title = issue.Summary, Metadata = rows.ToArray() },
        };
    }

    private static Tag[] BuildTags(HaRepair issue)
    {
        // Match the HA frontend's colour scheme: red for critical/error,
        // amber for warning, grey for anything else. Ignored repairs get a
        // muted appearance regardless of severity.
        var tags = new List<Tag>(2);
        var (bg, fg) = SeverityColor(issue.Severity, issue.Ignored);
        tags.Add(new Tag(issue.Severity.ToUpperInvariant()) { Background = bg, Foreground = fg });
        if (issue.Ignored)
        {
            tags.Add(new Tag("IGNORED")
            {
                Background = ColorHelpers.FromRgb(120, 120, 120),
                Foreground = ColorHelpers.FromRgb(255, 255, 255),
            });
        }
        return tags.ToArray();
    }

    private static (OptionalColor Background, OptionalColor Foreground) SeverityColor(string severity, bool ignored)
    {
        if (ignored)
        {
            return (ColorHelpers.FromRgb(120, 120, 120), ColorHelpers.FromRgb(255, 255, 255));
        }
        return severity switch
        {
            "critical" or "error" => (ColorHelpers.FromArgb(255, 211, 47, 47), ColorHelpers.FromRgb(255, 255, 255)),
            "warning" => (ColorHelpers.FromArgb(255, 245, 124, 0), ColorHelpers.FromRgb(255, 255, 255)),
            _ => (ColorHelpers.FromArgb(255, 76, 161, 222), ColorHelpers.FromRgb(255, 255, 255)),
        };
    }

    private static DetailsElement Row(string key, string value) => new()
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };
}
