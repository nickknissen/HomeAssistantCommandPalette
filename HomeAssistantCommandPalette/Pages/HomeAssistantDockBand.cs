using System;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Dock band landing page. CmdPal renders an IListPage band by inlining
/// every row, so the band collapses to two rows on the dock: Repairs and
/// Updates. Each row drills down to its own page.
/// </summary>
/// <remarks>
/// The page recomputes counts on every <c>GetItems</c> so the row titles
/// match what the user is about to see. Refresh is event-driven for
/// updates (HA fires <c>state_changed</c> on update entities) and
/// best-effort for repairs (HA doesn't push issue-registry changes; the
/// 30 s REST cache absorbs repeat opens).
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Page lifetime equals process lifetime; ListPage has no Dispose hook.")]
internal sealed partial class HomeAssistantDockBand : ListPage
{
    private readonly HaSettings _settings;
    private readonly IHaClient _client;
    private readonly IEntityIconResolver _iconResolver;

    // HA can burst state_changed events when an integration's updates
    // refresh all at once. Coalesce the row-refresh into one render per
    // quiet window so the dock band doesn't thrash.
    private static readonly TimeSpan RefreshDebounce = TimeSpan.FromMilliseconds(250);
    private readonly System.Threading.Timer _refreshTimer;

    private static readonly string[] UpdateDomain = ["update"];

    public HomeAssistantDockBand(HaSettings settings, IHaClient client, IEntityIconResolver iconResolver)
    {
        _settings = settings;
        _client = client;
        _iconResolver = iconResolver;
        Icon = Icons.App;
        Title = "Home Assistant";
        Name = "Open";
        Id = "ha.dock-band";
        ShowDetails = false;
        PlaceholderText = "Repairs and updates";

        _refreshTimer = new System.Threading.Timer(_ =>
        {
            try { RaiseItemsChanged(0); } catch { /* page may be torn down */ }
        }, state: null, dueTime: System.Threading.Timeout.Infinite, period: System.Threading.Timeout.Infinite);

        _client.StateChanged += OnClientStateChanged;
    }

    private void OnClientStateChanged(string? entityId)
    {
        // The Updates count tracks the `update.*` domain. A full reset
        // (null) or any update-domain change should bump the band; ignore
        // the firehose of other state_changed events.
        if (entityId is not null && !entityId.StartsWith("update.", StringComparison.Ordinal))
        {
            return;
        }
        _refreshTimer.Change(RefreshDebounce, System.Threading.Timeout.InfiniteTimeSpan);
    }

    public override IListItem[] GetItems()
    {
        // Hard error (not configured / auth) — short-circuit to a single
        // settings-redirect row instead of advertising broken counts.
        var states = _client.GetStates();
        if (states.HasError)
        {
            var openSettings = (ICommand)_settings.Settings.SettingsPage;
            ICommand errorCommand = states.ErrorKind switch
            {
                HaErrorKind.NotConfigured or HaErrorKind.Unauthorized or HaErrorKind.InvalidUrl => openSettings,
                _ => new NoOpCommand(),
            };
            var subtitle = states.ErrorKind switch
            {
                HaErrorKind.NotConfigured => "Press Enter to open settings and add your URL + access token.",
                HaErrorKind.Unauthorized => "Press Enter to open settings and update your access token.",
                HaErrorKind.InvalidUrl => "Press Enter to open settings and fix the URL.",
                _ => states.ErrorDescription,
            };
            return [new ListItem(errorCommand) { Title = states.ErrorTitle, Subtitle = subtitle, Icon = Icons.App }];
        }

        var updateCount = 0;
        foreach (var entity in states.Items)
        {
            if (string.Equals(entity.Domain, "update", StringComparison.Ordinal)
                && string.Equals(entity.State, "on", StringComparison.OrdinalIgnoreCase))
            {
                updateCount++;
            }
        }

        // Repairs go through their own short-cached fetch — failures
        // resolve to a 0-count row so the band stays readable.
        var repairsResult = _client.GetRepairs();
        var repairCount = repairsResult.HasError ? 0 : repairsResult.Issues.Count;

        var repairsPage = new RepairsPage(_settings, _client);
        var updatesPage = new EntityListPage(
            _settings, _client, _iconResolver,
            title: "Pending Updates",
            id: "ha.dock.updates",
            domains: UpdateDomain,
            icon: Icons.App,
            onlyOnState: true);

        return [
            new ListItem(repairsPage)
            {
                Title = "Repairs",
                Subtitle = FormatCount(repairCount, "repair", "repairs"),
                Icon = Icons.App,
                Tags = CountTag(repairCount, severity: repairCount > 0 ? "warning" : "ok"),
            },
            new ListItem(updatesPage)
            {
                Title = "Updates",
                Subtitle = FormatCount(updateCount, "update available", "updates available"),
                Icon = Icons.UpdateOn,
                Tags = CountTag(updateCount, severity: updateCount > 0 ? "info" : "ok"),
            },
        ];
    }

    private static string FormatCount(int n, string singular, string plural)
        => n switch
        {
            0 => $"No {plural}",
            1 => $"1 {singular}",
            _ => $"{n} {plural}",
        };

    private static Tag[] CountTag(int n, string severity)
    {
        var (bg, fg) = severity switch
        {
            // Amber for pending repairs — they need attention but aren't
            // breaking yet (true breakage shows as a per-row error tag in
            // RepairsPage). Blue for pending updates, grey when nothing.
            "warning" => (ColorHelpers.FromArgb(255, 245, 124, 0), ColorHelpers.FromRgb(255, 255, 255)),
            "info" => (ColorHelpers.FromArgb(255, 76, 161, 222), ColorHelpers.FromRgb(255, 255, 255)),
            _ => (ColorHelpers.FromRgb(120, 120, 120), ColorHelpers.FromRgb(255, 255, 255)),
        };
        return [new Tag(n.ToString(System.Globalization.CultureInfo.InvariantCulture))
        {
            Background = bg,
            Foreground = fg,
        }];
    }

    /// <summary>
    /// Combined pending count for the dock band's title tag. Repairs +
    /// updates needing user action. Cheap enough to call on every dock
    /// render — both reads are cached.
    /// </summary>
    public int GetPendingCount()
    {
        var n = 0;
        var states = _client.GetStates();
        if (!states.HasError)
        {
            foreach (var entity in states.Items)
            {
                if (string.Equals(entity.Domain, "update", StringComparison.Ordinal)
                    && string.Equals(entity.State, "on", StringComparison.OrdinalIgnoreCase))
                {
                    n++;
                }
            }
        }
        var repairs = _client.GetRepairs();
        if (!repairs.HasError) n += repairs.Issues.Count;
        return n;
    }
}
