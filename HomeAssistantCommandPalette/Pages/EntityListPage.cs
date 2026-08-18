using System;
using System.Collections.Concurrent;
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
internal sealed partial class EntityListPage : DynamicListPage
{
    private const string EntityCommandIdPrefix = "ha.entity:";

    // Every item handed to CmdPal costs ~19 cross-process property reads
    // plus a PropChanged subscription, and a fresh navigation starts with
    // an empty ViewModel cache — so the hand-off, not the data, is what
    // makes a 1000-entity page slow to open. Give the host a page at a
    // time and let it ask for more by scrolling.
    private const int PageSize = 100;

    private readonly HaSettings _settings;
    private readonly IHaClient _client;
    private readonly IEntityIconResolver _iconResolver;
    private readonly HashSet<string>? _domains;
    private readonly HashSet<string>? _deviceClasses;
    private readonly bool _sortByNumericStateAscending;
    private readonly bool _openAttributesPage;
    private readonly bool _onlyOnState;
    private readonly bool _isCameraGridPage;
    private readonly ConcurrentDictionary<string, byte> _pinnedIds = new(StringComparer.Ordinal);

    // One ListItem per entity, reused across renders. CmdPal caches its
    // ViewModels keyed on item *reference identity*, so handing back the
    // same instances means an unchanged row costs nothing to re-render —
    // and we stop minting a fresh set of COM wrappers on every refresh.
    private readonly ConcurrentDictionary<string, CachedItem> _itemCache = new(StringComparer.Ordinal);

    private sealed record CachedItem(HaEntity Source, LazyDetailsListItem Item);

    // How many PageSize chunks the host has asked for via LoadMore.
    private int _loadedChunks = 1;

    // Frozen "recently changed first" ordering for the unfiltered page.
    // Re-sorting on every state push would make rows jump around while
    // the user is reading them, so the order is held until the entity set
    // changes, the search box is cleared, or it simply goes stale.
    private static readonly TimeSpan RecencyOrderMaxAge = TimeSpan.FromSeconds(60);
    private List<string>? _recencyOrder;
    private long _recencyOrderStampUtcTicks;

    // HA can burst many state_changed events in a short window (e.g. an
    // automation toggling 20 lights). Coalesce into one RaiseItemsChanged
    // call per quiet window so we don't thrash CmdPal's render path.
    private static readonly TimeSpan WsRefreshDebounce = TimeSpan.FromMilliseconds(250);
    // A quiet window never arrives on a chatty instance — every event
    // would push the debounce out again and the list would go stale
    // (or, worse, only refresh once the user stops interacting). Cap how
    // long a pending refresh can be postponed; past the cap the armed
    // tick stands, so a busy instance settles at roughly one rebuild per
    // second instead of one per event.
    private static readonly TimeSpan WsRefreshMaxDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CameraAutoRefreshIdleGrace = TimeSpan.FromMilliseconds(500);
    private readonly System.Threading.Timer _wsRefreshTimer;
    private long _refreshPendingSinceUtcTicks;
    private readonly System.Threading.Timer? _cameraRefreshTimer;
    private readonly bool _autoRefreshCameras;
    private readonly TimeSpan _cameraAutoRefreshInterval;
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
        bool sortByNumericStateAscending = false,
        bool openAttributesPage = false,
        bool onlyOnState = false)
    {
        _settings = settings;
        _client = client;
        _iconResolver = iconResolver;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);
        _deviceClasses = deviceClasses is null ? null : new HashSet<string>(deviceClasses, StringComparer.Ordinal);
        _sortByNumericStateAscending = sortByNumericStateAscending;
        _openAttributesPage = openAttributesPage;
        _onlyOnState = onlyOnState;
        _cameraAutoRefreshInterval = CameraAutoRefreshIntervalFromSettings(_settings);
        _isCameraGridPage = IsCameraAutoRefreshPage(_domains, _deviceClasses);
        _autoRefreshCameras = _isCameraGridPage && _cameraAutoRefreshInterval > TimeSpan.Zero;

        Icon = icon ?? Icons.App;
        Title = title;
        Name = "Open";
        Id = id;
        ShowDetails = true;
        PlaceholderText = $"Search {title.ToLowerInvariant()}";

        if (_isCameraGridPage)
        {
            ShowDetails = false;
            GridProperties = new GalleryGridLayout
            {
                ShowTitle = true,
                ShowSubtitle = true,
            };
        }

        _wsRefreshTimer = new System.Threading.Timer(_ =>
        {
            System.Threading.Interlocked.Exchange(ref _refreshPendingSinceUtcTicks, 0);
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
        RefreshPinnedItems(entityId);

        // Filter at the page level — without this, an unrelated sensor
        // pushing updates would re-render the Lights page (and reset the
        // user's selection to position 1) every few seconds. Null
        // entityId = full reset (hydration / reconnect); always refresh.
        if (entityId is not null && !MatchesPageFilter(entityId))
        {
            return;
        }
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        var now = DateTime.UtcNow.Ticks;
        var pendingSince = System.Threading.Interlocked.CompareExchange(ref _refreshPendingSinceUtcTicks, now, 0);
        if (pendingSince != 0 && new TimeSpan(now - pendingSince) >= WsRefreshMaxDelay)
        {
            // A refresh has been waiting longer than the cap — leave the
            // already-armed tick alone rather than postponing it again.
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

    /// <summary>
    /// The search box drives us, not CmdPal: we own the filtering so the
    /// host only ever receives the matches, not all 1000 entities.
    /// </summary>
    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // A new query starts back at the first page, and clearing the box
        // re-freshens the "recently changed" ordering.
        _loadedChunks = 1;
        if (string.IsNullOrWhiteSpace(newSearch))
        {
            _recencyOrder = null;
        }
        RaiseItemsChanged(0);
    }

    public override void LoadMore()
    {
        _loadedChunks++;
        // -2 tells CmdPal this is an incremental refresh so it keeps the
        // user's selection instead of snapping back to the first row.
        RaiseItemsChanged(-2);
    }

    public override IListItem[] GetItems()
    {
        TouchCameraAutoRefresh();

        var result = _client.GetStates();
        if (result.HasError)
        {
            HasMoreItems = false;
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
        if (_onlyOnState)
        {
            // Pending-only filter for the dock band's Updates view —
            // hides update entities whose state is "off" (already
            // installed) so the row count matches the dock band's badge.
            items = items.Where(e => string.Equals(e.State, "on", StringComparison.OrdinalIgnoreCase));
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

        var query = SearchText?.Trim() ?? string.Empty;
        var candidates = items.ToList();

        // Unfiltered All Entities: newest activity first, so the page
        // answers "what just happened" instead of opening on whatever
        // sorts first alphabetically. Domain pages keep their own order.
        if (query.Length == 0 && _domains is null && !_sortByNumericStateAscending)
        {
            candidates = ApplyFrozenRecencyOrder(candidates);
        }

        PruneItemCache(candidates);

        IEnumerable<HaEntity> ordered = candidates;
        if (query.Length > 0)
        {
            ordered = ListHelpers.FilterList(candidates, query, ScoreEntity);
        }

        // Score and page over entities, then build items only for the rows
        // actually handed over — a match that never reaches the host costs
        // nothing.
        var limit = Math.Max(1, _loadedChunks) * PageSize;
        var page = ordered.Take(limit + 1).ToList();
        HasMoreItems = page.Count > limit;
        if (HasMoreItems)
        {
            page.RemoveAt(page.Count - 1);
        }

        return page.Select(GetOrUpdateItem).ToArray<IListItem>();
    }

    /// <summary>
    /// Ranks an entity against the query. Scoring is ours rather than
    /// <c>ListHelpers.ScoreListItem</c>'s: that one routes through the
    /// SDK's fuzzy matcher, which needs a pinyin assembly the extension
    /// doesn't ship and would throw on the first keystroke. Doing it here
    /// also lets a search hit <c>entity_id</c> and area, which is what
    /// people reach for when wiring automations.
    /// </summary>
    internal static int ScoreEntity(string query, HaEntity entity)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 1;
        }

        var total = 0;
        foreach (var token in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // Every token has to land somewhere — "kitchen lamp" should not
            // match a bedroom lamp just because "lamp" hit.
            var score = ScoreToken(token, entity);
            if (score == 0)
            {
                return 0;
            }
            total += score;
        }
        return total;
    }

    private static int ScoreToken(string token, HaEntity entity)
    {
        var name = entity.FriendlyName ?? string.Empty;

        if (string.Equals(name, token, StringComparison.OrdinalIgnoreCase)) return 100;

        var nameIndex = name.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (nameIndex == 0) return 80;
        if (nameIndex > 0) return IsWordStart(name, nameIndex) ? 60 : 40;

        if (entity.EntityId.Contains(token, StringComparison.OrdinalIgnoreCase)) return 30;
        if (entity.AreaName?.Contains(token, StringComparison.OrdinalIgnoreCase) == true) return 20;

        return 0;
    }

    private static bool IsWordStart(string text, int index)
        => index > 0 && text[index - 1] is ' ' or '_' or '-' or '.';

    /// <summary>
    /// Returns the cached item for an entity, updating it in place when
    /// the entity actually changed. Identity is the point: the same
    /// instance across renders lets CmdPal reuse its ViewModel instead of
    /// re-reading every property over COM.
    /// </summary>
    private LazyDetailsListItem GetOrUpdateItem(HaEntity entity)
    {
        if (_itemCache.TryGetValue(entity.EntityId, out var cached))
        {
            if (!HasRenderableChange(cached.Source, entity))
            {
                return cached.Item;
            }

            CopyItemProperties(CreateItem(entity), cached.Item);
            _itemCache[entity.EntityId] = cached with { Source = entity };
            return cached.Item;
        }

        var item = CreateItem(entity);
        _itemCache[entity.EntityId] = new CachedItem(entity, item);
        return item;
    }

    // last_updated moves whenever HA touches the state *or* any attribute,
    // so it covers icon / tag / details changes without walking the
    // attribute dictionary. The rest are what the row itself renders.
    private static bool HasRenderableChange(HaEntity previous, HaEntity current)
        => !string.Equals(previous.State, current.State, StringComparison.Ordinal)
            || previous.LastUpdated != current.LastUpdated
            || !string.Equals(previous.FriendlyName, current.FriendlyName, StringComparison.Ordinal)
            || !string.Equals(previous.AreaName, current.AreaName, StringComparison.Ordinal);

    private void PruneItemCache(List<HaEntity> candidates)
    {
        // Entities disappear when an integration is removed or renamed.
        // Only pay for the sweep when the cache has outgrown the snapshot.
        if (_itemCache.Count <= candidates.Count)
        {
            return;
        }

        var live = new HashSet<string>(candidates.Select(e => e.EntityId), StringComparer.Ordinal);
        foreach (var key in _itemCache.Keys)
        {
            if (!live.Contains(key) && !_pinnedIds.ContainsKey(key))
            {
                _itemCache.TryRemove(key, out _);
            }
        }
    }

    private List<HaEntity> ApplyFrozenRecencyOrder(List<HaEntity> candidates)
    {
        var order = _recencyOrder;
        var stale = order is null
            || order.Count != candidates.Count
            || DateTime.UtcNow - new DateTime(System.Threading.Interlocked.Read(ref _recencyOrderStampUtcTicks), DateTimeKind.Utc) > RecencyOrderMaxAge
            || !new HashSet<string>(candidates.Select(e => e.EntityId), StringComparer.Ordinal).SetEquals(order);

        if (stale)
        {
            order = candidates
                .OrderByDescending(e => e.LastChanged ?? DateTimeOffset.MinValue)
                .Select(e => e.EntityId)
                .ToList();
            _recencyOrder = order;
            System.Threading.Interlocked.Exchange(ref _recencyOrderStampUtcTicks, DateTime.UtcNow.Ticks);
        }

        var rank = new Dictionary<string, int>(order!.Count, StringComparer.Ordinal);
        for (var i = 0; i < order.Count; i++)
        {
            rank[order[i]] = i;
        }

        return candidates
            .OrderBy(e => rank.TryGetValue(e.EntityId, out var r) ? r : int.MaxValue)
            .ToList();
    }

    internal ListItem? TryCreateItemForCommandId(string id)
    {
        if (!TryGetEntityIdFromCommandId(id, out var entityId))
        {
            return null;
        }

        var result = _client.GetStates();
        if (result.HasError)
        {
            return null;
        }

        var entity = result.Items.FirstOrDefault(e => string.Equals(e.EntityId, entityId, StringComparison.Ordinal));
        if (entity is null)
        {
            return null;
        }

        // Shares the page's item cache, so a pinned dock row and the same
        // row in the list are one object that updates once.
        _pinnedIds[entityId] = 0;
        return GetOrUpdateItem(entity);
    }

    internal static string EntityCommandId(string entityId) => EntityCommandIdPrefix + entityId;

    internal static bool TryGetEntityIdFromCommandId(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? entityId)
    {
        if (id.StartsWith(EntityCommandIdPrefix, StringComparison.Ordinal) && id.Length > EntityCommandIdPrefix.Length)
        {
            entityId = id[EntityCommandIdPrefix.Length..];
            return true;
        }

        entityId = null;
        return false;
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

    internal static TimeSpan CameraAutoRefreshIntervalFromSettings(HaSettings settings)
        => settings.CameraRefreshIntervalMs <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromMilliseconds(settings.CameraRefreshIntervalMs);

    private void TouchCameraAutoRefresh()
    {
        if (!_autoRefreshCameras || _cameraRefreshTimer is null) return;

        System.Threading.Interlocked.Exchange(ref _lastCameraGetItemsUtcTicks, DateTime.UtcNow.Ticks);
        _cameraRefreshTimer.Change(_cameraAutoRefreshInterval, _cameraAutoRefreshInterval);
    }

    private void OnCameraRefreshTimerTick()
    {
        var lastTicks = System.Threading.Interlocked.Read(ref _lastCameraGetItemsUtcTicks);
        if (lastTicks == 0 || DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc) > _cameraAutoRefreshInterval + CameraAutoRefreshIdleGrace)
        {
            _cameraRefreshTimer?.Change(System.Threading.Timeout.InfiniteTimeSpan, System.Threading.Timeout.InfiniteTimeSpan);
            return;
        }

        try { RaiseItemsChanged(0); } catch { /* page may have been closed */ }
    }

    private LazyDetailsListItem CreateItem(HaEntity entity)
    {
        var behavior = DomainRegistry.For(entity.Domain, entity.EntityId);
        var ctx = new DomainCtx(entity, _client, _settings, OnServiceCallSucceeded);

        var primary = _openAttributesPage
            ? new EntityAttributesPage(entity)
            : behavior.BuildPrimary(in ctx);
        SetCommandId(primary, EntityCommandId(entity.EntityId));

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

        // Camera grid cards use the snapshot as the card thumbnail, so
        // that page — and only that page — has to fetch hero images while
        // building the list. Everywhere else the hero is details-only and
        // rides the deferred build, which keeps a camera on a mixed page
        // from costing an HTTP round trip per render.
        var eagerHero = _isCameraGridPage ? behavior.BuildHeroImage(in ctx) : null;
        var itemIcon = eagerHero ?? _iconResolver.Resolve(entity);

        return new LazyDetailsListItem(primary, () => BuildDetails(behavior, ctx, eagerHero))
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            Icon = itemIcon,
            MoreCommands = ctxItems.ToArray(),
        };
    }

    /// <summary>
    /// Builds one entity's details pane. Runs on first read of
    /// <see cref="LazyDetailsListItem.Details"/> — i.e. when the row is
    /// selected — because the domain hooks it calls may hit Home
    /// Assistant (sensor history, camera snapshot).
    /// </summary>
    private static Details BuildDetails(DomainBehavior behavior, DomainCtx ctx, IconInfo? eagerHero)
    {
        var entity = ctx.Entity;

        var rows = new List<IDetailsElement> { DomainHelpers.Row("State", DomainHelpers.FormatStateWithUnit(entity)) };
        behavior.AddDetailRows(in ctx, rows);
        DomainHelpers.AppendCommonRows(entity, rows);

        var details = new Details
        {
            Title = entity.FriendlyName,
            Metadata = rows.ToArray(),
        };

        // HeroImage: only behaviors that need one (e.g. camera) override
        // BuildHeroImage; the toolkit type rejects null assignment.
        var hero = eagerHero ?? behavior.BuildHeroImage(in ctx);
        if (hero is not null) details.HeroImage = hero;
        return details;
    }

    private void RefreshPinnedItems(string? changedEntityId)
    {
        if (_pinnedIds.IsEmpty)
        {
            return;
        }

        var result = _client.GetStates();
        if (result.HasError)
        {
            return;
        }

        if (changedEntityId is not null)
        {
            RefreshPinnedItem(changedEntityId, result.Items);
            return;
        }

        foreach (var entityId in _pinnedIds.Keys)
        {
            RefreshPinnedItem(entityId, result.Items);
        }
    }

    private void RefreshPinnedItem(string entityId, IReadOnlyCollection<HaEntity> snapshot)
    {
        if (!_pinnedIds.ContainsKey(entityId) || !_itemCache.ContainsKey(entityId))
        {
            return;
        }

        var entity = snapshot.FirstOrDefault(e => string.Equals(e.EntityId, entityId, StringComparison.Ordinal));
        if (entity is null)
        {
            return;
        }

        // Updates the cached instance in place — the dock is watching this
        // very object's PropChanged.
        GetOrUpdateItem(entity);
    }

    private static void CopyItemProperties(LazyDetailsListItem source, LazyDetailsListItem target)
    {
        // Each assignment raises PropChanged, which makes CmdPal re-read
        // that property over COM — so only assign what actually moved.
        // State-dependent parts (tags, icon, commands) are rebuilt because
        // the caller only gets here when the entity changed.
        if (!string.Equals(target.Title, source.Title, StringComparison.Ordinal))
        {
            target.Title = source.Title;
        }
        if (!string.Equals(target.Subtitle, source.Subtitle, StringComparison.Ordinal))
        {
            target.Subtitle = source.Subtitle;
        }
        target.Icon = source.Icon;
        target.Tags = source.Tags;
        // Hand over the deferred build rather than the built pane —
        // reading source.Details here would fetch history / snapshots for
        // a row nobody has selected.
        target.ResetDetails(source.Factory);
        target.MoreCommands = source.MoreCommands;
        target.Command = source.Command;
    }

    private static void SetCommandId(ICommand command, string id)
    {
        switch (command)
        {
            case InvokableCommand invokable:
                invokable.Id = id;
                break;
            case ListPage page:
                page.Id = id;
                break;
        }
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
