using System;
using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Generic list page that shows entities, optionally filtered to a fixed
/// set of domains. The single class backs every per-domain top-level
/// command (Lights, Covers, Scenes, ...) plus the unfiltered "All Entities".
/// </summary>
/// <remarks>
/// Per-entity rendering — icon, primary command, context items, detail
/// rows, hero image — is delegated to the <see cref="DomainBehavior"/>
/// resolved by <see cref="DomainRegistry"/>. The page itself owns only:
/// fetch + filter + sort, error rendering, the refresh callback after a
/// successful service call, the person-avatar wrap, and the page-level
/// subtitle / tags.
/// </remarks>
// Pages live for the extension's lifetime (held in the provider's _commands
// array) and CmdPal's ListPage has no disposal hook, so the Timer field
// never needs releasing — it dies with the process.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Page lifetime equals process lifetime; ListPage has no Dispose hook.")]
internal sealed partial class EntityListPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly IHaClient _client;
    private readonly IEntityIconResolver _iconResolver;
    private readonly HashSet<string>? _domains;
    private readonly HashSet<string>? _deviceClasses;
    private readonly bool _sortByNumericStateAscending;

    // HA can burst many state_changed events in a short window (e.g. an
    // automation toggling 20 lights). Coalesce into one RaiseItemsChanged
    // call per quiet window so we don't thrash CmdPal's render path.
    private static readonly TimeSpan WsRefreshDebounce = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan CameraAutoRefreshInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan CameraAutoRefreshIdleGrace = TimeSpan.FromMilliseconds(500);
    private readonly System.Threading.Timer _wsRefreshTimer;
    private readonly System.Threading.Timer? _cameraRefreshTimer;
    private readonly bool _autoRefreshCameras;
    private long _lastCameraGetItemsUtcTicks;

    public EntityListPage(
        HaSettings settings,
        IHaClient client,
        IEntityIconResolver iconResolver,
        string title,
        string id,
        IReadOnlyCollection<string>? domains = null,
        IconInfo? icon = null,
        IReadOnlyCollection<string>? deviceClasses = null,
        bool sortByNumericStateAscending = false)
    {
        _settings = settings;
        _client = client;
        _iconResolver = iconResolver;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);
        _deviceClasses = deviceClasses is null ? null : new HashSet<string>(deviceClasses, StringComparer.Ordinal);
        _sortByNumericStateAscending = sortByNumericStateAscending;
        _autoRefreshCameras = IsCameraAutoRefreshPage(_domains, _deviceClasses);

        Icon = icon ?? Icons.App;
        Title = title;
        Name = "Open";
        Id = id;
        ShowDetails = true;
        PlaceholderText = $"Search {title.ToLowerInvariant()}";

        _wsRefreshTimer = new System.Threading.Timer(_ =>
        {
            try { RaiseItemsChanged(0); } catch { /* page may be torn down */ }
        }, state: null, dueTime: System.Threading.Timeout.Infinite, period: System.Threading.Timeout.Infinite);

        if (_autoRefreshCameras)
        {
            _cameraRefreshTimer = new System.Threading.Timer(_ => OnCameraRefreshTimerTick(),
                state: null,
                dueTime: System.Threading.Timeout.Infinite,
                period: System.Threading.Timeout.Infinite);
        }

        // Pages live for the extension's lifetime (held in the provider's
        // _commands array), so we never unsubscribe — the handler dies
        // with the process.
        _client.StateChanged += OnClientStateChanged;
    }

    private void OnClientStateChanged(string? entityId)
    {
        // Filter at the page level — without this, an unrelated sensor
        // pushing updates would re-render the Lights page (and reset the
        // user's selection to position 1) every few seconds. Null
        // entityId = full reset (hydration / reconnect); always refresh.
        if (entityId is not null && !MatchesPageFilter(entityId))
        {
            return;
        }
        _wsRefreshTimer.Change(WsRefreshDebounce, System.Threading.Timeout.InfiniteTimeSpan);
    }

    private bool MatchesPageFilter(string entityId)
    {
        // No domain filter (All Entities) — every event is a candidate.
        if (_domains is null) return true;

        var dot = entityId.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0) return false;
        var domain = entityId.AsSpan(0, dot).ToString();
        // Device-class is a finer cut (Batteries, Doors, ...) — checking
        // it would need an attribute lookup against the snapshot. Domain
        // alone already filters out 95% of unrelated traffic; accept the
        // few false-positive refreshes as the price of simplicity.
        return _domains.Contains(domain);
    }

    public override IListItem[] GetItems()
    {
        TouchCameraAutoRefresh();

        var result = _client.GetStates();
        if (result.HasError)
        {
            // For configuration errors, make the error item itself navigate
            // to the settings page so the user can fix it in one click.
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
            return [
                new ListItem(errorCommand)
                {
                    Title = result.ErrorTitle,
                    Subtitle = subtitle,
                }
            ];
        }

        IEnumerable<HaEntity> items = result.Items;
        if (_settings.HideUnavailable)
        {
            items = items.Where(e => !string.Equals(e.State, "unavailable", StringComparison.OrdinalIgnoreCase));
        }
        if (_domains is not null)
        {
            items = items.Where(e => _domains.Contains(e.Domain));
        }
        if (_deviceClasses is not null)
        {
            items = items.Where(e => e.Attributes.TryGetValue("device_class", out var dc)
                && dc is string dcs && _deviceClasses.Contains(dcs));
        }
        if (_sortByNumericStateAscending)
        {
            // Used by the Batteries page to surface lowest-charge sensors
            // first. Non-numeric states (e.g. "unavailable") sort to the
            // end via double.PositiveInfinity.
            items = items.OrderBy(e => double.TryParse(e.State,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : double.PositiveInfinity);
        }

        return items.Select(CreateItem).ToArray();
    }

    // HA dispatches services asynchronously — even after a 200 response,
    // the entity state we'd refetch may still be stale for a few hundred ms.
    // Wait briefly before signalling the list to refresh.
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(250);

    private void OnServiceCallSucceeded()
    {
        // When WS push is live, the state_changed event will refresh the
        // list naturally — adding a second timed RaiseItemsChanged here
        // causes visible flicker (two re-renders within ~500 ms of one
        // user action).
        if (_client.IsLive) return;

        // REST-only path (cold start, or WS unreachable): we own the
        // refresh ourselves. Fire-and-forget: tell CmdPal to re-call
        // GetItems after HA has had a moment to propagate the new state.
        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(RefreshDelay).ConfigureAwait(false);
            try { RaiseItemsChanged(0); } catch { /* page may have been closed */ }
        });
    }

    internal static bool IsCameraAutoRefreshPage(IReadOnlyCollection<string>? domains, IReadOnlyCollection<string>? deviceClasses)
        => deviceClasses is null
            && domains is not null
            && domains.Count == 1
            && domains.Contains("camera");

    private void TouchCameraAutoRefresh()
    {
        if (!_autoRefreshCameras || _cameraRefreshTimer is null) return;

        System.Threading.Interlocked.Exchange(ref _lastCameraGetItemsUtcTicks, DateTime.UtcNow.Ticks);
        _cameraRefreshTimer.Change(CameraAutoRefreshInterval, CameraAutoRefreshInterval);
    }

    private void OnCameraRefreshTimerTick()
    {
        var lastTicks = System.Threading.Interlocked.Read(ref _lastCameraGetItemsUtcTicks);
        if (lastTicks == 0 || DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc) > CameraAutoRefreshInterval + CameraAutoRefreshIdleGrace)
        {
            _cameraRefreshTimer?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
            return;
        }

        try { RaiseItemsChanged(0); } catch { /* page may have been closed */ }
    }

    private ListItem CreateItem(HaEntity entity)
    {
        var behavior = DomainRegistry.For(entity.Domain, entity.EntityId);
        var ctx = new DomainCtx(entity, _client, _settings, OnServiceCallSucceeded);

        var primary = behavior.BuildPrimary(in ctx);

        var rows = new List<IDetailsElement> { DomainHelpers.Row("State", DomainHelpers.FormatStateWithUnit(entity)) };
        behavior.AddDetailRows(in ctx, rows);
        DomainHelpers.AppendCommonRows(entity, rows);

        var ctxItems = new List<IContextItem>(8);
        behavior.AddContextItems(in ctx, ctxItems);

        // Tail items: Open dashboard (skipped when it'd duplicate the
        // primary action) and Copy entity ID always last.
        if (primary is not OpenDashboardCommand)
        {
            ctxItems.Add(new CommandContextItem(new OpenDashboardCommand(_settings, entity.EntityId)));
        }
        ctxItems.Add(new CommandContextItem(new CopyTextCommand(entity.EntityId)
        {
            Name = "Copy entity ID",
        }));

        var details = new Details
        {
            Title = entity.FriendlyName,
            Metadata = rows.ToArray(),
        };
        // HeroImage: only behaviors that need one (e.g. camera) override
        // BuildHeroImage; the toolkit type rejects null assignment.
        var hero = behavior.BuildHeroImage(in ctx);
        if (hero is not null) details.HeroImage = hero;

        return new ListItem(primary)
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            Icon = _iconResolver.Resolve(entity),
            MoreCommands = ctxItems.ToArray(),
            Details = details,
        };
    }

    private string BuildSubtitle(HaEntity entity)
    {
        // Power users wiring up automations want to see entity_id; the
        // Show Entity IDs setting swaps it in. Default mirrors Raycast:
        // area (room) name only — state lives in the tags.
        if (_settings.ShowEntityId)
        {
            return entity.EntityId;
        }
        return entity.AreaName ?? string.Empty;
    }

    private Tag[] BuildTags(HaEntity entity)
    {
        // Hide the domain tag on single-domain pages (Lights, Covers, ...) —
        // it's redundant. Keep it on All Entities and multi-domain pages
        // (Buttons, Helpers) so users can tell entities apart.
        var showDomainTag = _domains is null || _domains.Count > 1;
        var tags = new List<Tag>(2);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "media_player" or "binary_sensor" or "cover" or "update")
        {
            tags.Add(entity.IsOn
                ? new Tag("ON")
                {
                    Background = ColorHelpers.FromArgb(255, 76, 161, 222),
                    Foreground = ColorHelpers.FromRgb(255, 255, 255),
                }
                : new Tag("OFF")
                {
                    Background = ColorHelpers.FromRgb(120, 120, 120),
                    Foreground = ColorHelpers.FromRgb(255, 255, 255),
                });
        }

        if (showDomainTag)
        {
            tags.Add(new Tag(entity.Domain) { ToolTip = "Domain" });
        }

        return tags.ToArray();
    }
}
