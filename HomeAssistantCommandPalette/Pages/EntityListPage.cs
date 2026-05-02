using System;
using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Generic list page that shows entities, optionally filtered to a fixed
/// set of domains. The single class backs every per-domain top-level
/// command (Lights, Covers, Scenes, ...) plus the unfiltered "All Entities".
/// </summary>
internal sealed partial class EntityListPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly IHaClient _client;
    private readonly HashSet<string>? _domains;
    private readonly HashSet<string>? _deviceClasses;
    private readonly bool _sortByNumericStateAscending;

    public EntityListPage(
        HaSettings settings,
        IHaClient client,
        string title,
        string id,
        IReadOnlyCollection<string>? domains = null,
        IconInfo? icon = null,
        IReadOnlyCollection<string>? deviceClasses = null,
        bool sortByNumericStateAscending = false)
    {
        _settings = settings;
        _client = client;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);
        _deviceClasses = deviceClasses is null ? null : new HashSet<string>(deviceClasses, StringComparer.Ordinal);
        _sortByNumericStateAscending = sortByNumericStateAscending;

        Icon = icon ?? Icons.App;
        Title = title;
        Name = "Open";
        Id = id;
        ShowDetails = true;
        PlaceholderText = $"Search {title.ToLowerInvariant()}";
    }

    public override IListItem[] GetItems()
    {
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
        // Fire-and-forget: invalidate cache + tell CmdPal to re-call GetItems
        // after HA has had a moment to propagate the new state.
        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(RefreshDelay).ConfigureAwait(false);
            try { RaiseItemsChanged(0); } catch { /* page may have been closed */ }
        });
    }

    private ListItem CreateItem(HaEntity entity)
    {
        // Domains migrated to the new abstraction render through their
        // DomainBehavior; everything else still flows through the legacy
        // dispatch sites (BuildPrimaryCommand / BuildContextCommands /
        // BuildDetails / IconForEntity) until those branches move.
        if (DomainRegistry.TryGet(entity.Domain, entity.EntityId, out var behavior))
        {
            return CreateItemViaBehavior(entity, behavior);
        }

        var primary = BuildPrimaryCommand(entity);
        return new ListItem(primary)
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            Icon = ResolveEntityIcon(entity),
            MoreCommands = BuildContextCommands(entity, primary),
            Details = BuildDetails(entity),
        };
    }

    private ListItem CreateItemViaBehavior(HaEntity entity, DomainBehavior behavior)
    {
        var ctx = new DomainCtx(entity, _client, _settings, OnServiceCallSucceeded);

        var primary = behavior.BuildPrimary(in ctx);

        var rows = new List<IDetailsElement> { Row("State", FormatStateWithUnit(entity)) };
        behavior.AddDetailRows(in ctx, rows);
        AppendCommonRows(entity, rows);

        var ctxItems = new List<IContextItem>(8);
        behavior.AddContextItems(in ctx, ctxItems);

        // Tail items mirror the legacy BuildContextCommands tail: Open
        // dashboard (skipped when it'd duplicate the primary action) and
        // Copy entity ID always last.
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
        // Only assign HeroImage when the behavior produces one — the
        // toolkit type rejects null assignment, and the overwhelming
        // majority of behaviors don't render a hero.
        var hero = behavior.BuildHeroImage(in ctx);
        if (hero is not null) details.HeroImage = hero;

        return new ListItem(primary)
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            Icon = ResolveEntityIcon(entity, behavior.BuildIcon(in ctx)),
            MoreCommands = ctxItems.ToArray(),
            Details = details,
        };
    }

    private static void AppendCommonRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (!string.IsNullOrEmpty(entity.AreaName))
        {
            meta.Add(Row("Area", entity.AreaName));
        }

        if (entity.LastChanged is DateTimeOffset changed)
        {
            meta.Add(Row("Last changed", FormatRelativeTime(changed)));
        }

        if (entity.Attributes.TryGetValue("attribution", out var att) && att is string atts && !string.IsNullOrEmpty(atts))
        {
            meta.Add(Row("Attribution", atts));
        }

        meta.Add(Row("Entity ID", entity.EntityId));
    }

    /// <summary>
    /// Wraps <see cref="IconForEntity"/> with instance-level fallbacks that
    /// need authenticated HTTP. Persons get their <c>entity_picture</c>
    /// avatar (Gravatar or HA-served) when one is set; if the fetch fails
    /// we drop back to the state-tinted account glyph.
    /// </summary>
    private IconInfo ResolveEntityIcon(HaEntity entity)
        => ResolveEntityIcon(entity, IconForEntity(entity));

    /// <summary>
    /// Wraps a behavior-supplied icon with the page-level person-avatar
    /// fallback. Persons get their <c>entity_picture</c> avatar (Gravatar
    /// or HA-served) when one is set; if the fetch fails we drop back to
    /// the supplied icon.
    /// </summary>
    private IconInfo ResolveEntityIcon(HaEntity entity, IconInfo fallback)
    {
        if (entity.Domain == "person"
            && entity.Attributes.TryGetValue("entity_picture", out var pic)
            && pic is string picUrl
            && !string.IsNullOrEmpty(picUrl))
        {
            var path = _client.GetEntityPicturePath(entity.EntityId, picUrl);
            if (path is not null) return new IconInfo(path);
        }

        return fallback;
    }

    private static Details BuildDetails(HaEntity entity)
    {
        var meta = new List<IDetailsElement>
        {
            Row("State", FormatStateWithUnit(entity)),
        };


        if (!string.IsNullOrEmpty(entity.AreaName))
        {
            meta.Add(Row("Area", entity.AreaName));
        }

        if (entity.LastChanged is DateTimeOffset changed)
        {
            meta.Add(Row("Last changed", FormatRelativeTime(changed)));
        }

        if (entity.Attributes.TryGetValue("attribution", out var att) && att is string atts && !string.IsNullOrEmpty(atts))
        {
            meta.Add(Row("Attribution", atts));
        }

        meta.Add(Row("Entity ID", entity.EntityId));

        return new Details
        {
            Title = entity.FriendlyName,
            Metadata = meta.ToArray(),
        };
    }

    private static string FormatStateWithUnit(HaEntity entity)
    {
        var state = string.IsNullOrEmpty(entity.State) ? "(no state)" : entity.State;
        if (entity.Attributes.TryGetValue("unit_of_measurement", out var u) && u is string unit && !string.IsNullOrEmpty(unit))
        {
            return $"{state} {unit}";
        }
        return state;
    }

    private static string FormatRelativeTime(DateTimeOffset when)
    {
        var diff = DateTimeOffset.UtcNow - when;
        if (diff.TotalSeconds < 0) return when.ToLocalTime().ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        if (diff.TotalSeconds < 60) return "just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
        return when.ToLocalTime().ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }


    private static string FormatNum(double v) =>
        v == System.Math.Floor(v)
            ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
            : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static bool TryGetDouble(IReadOnlyDictionary<string, object?> attrs, string key, out double value)
    {
        if (attrs.TryGetValue(key, out var v))
        {
            switch (v)
            {
                case double d: value = d; return true;
                case long l: value = l; return true;
            }
        }
        value = 0;
        return false;
    }


    private static DetailsElement Row(string key, string value) => new()
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };

    private static IconInfo IconForEntity(HaEntity entity)
    {
        var unavailable = string.Equals(entity.State, "unavailable", System.StringComparison.OrdinalIgnoreCase);

if (entity.Domain == "zone") return unavailable ? Icons.ZoneUnavailable : Icons.Zone;

        if (entity.Domain == "input_text") return Icons.InputText;
        if (entity.Domain == "input_datetime")
        {
            // Pick calendar / clock / both based on the entity's published
            // has_date / has_time flags.
            var hasDate = entity.Attributes.TryGetValue("has_date", out var hd) && hd is bool hdb && hdb;
            var hasTime = entity.Attributes.TryGetValue("has_time", out var ht) && ht is bool htb && htb;
            if (hasDate && !hasTime) return Icons.InputDate;
            if (hasTime && !hasDate) return Icons.InputTime;
            return Icons.InputDateTime;
        }

        return unavailable ? Icons.ShapeUnavailable : Icons.Shape;
    }

    private OpenDashboardCommand BuildPrimaryCommand(HaEntity entity)
        // Legacy fallback for unmigrated domains. Every domain that still
        // flows through this path is read-only (no toggle / press service)
        // — so opening the dashboard is the correct primary action.
        => new(_settings, entity.EntityId);

    private IContextItem[] BuildContextCommands(HaEntity entity, ICommand primary)
    {
        var items = new List<IContextItem>(8);

        // Skip the dashboard context item when it'd duplicate the primary
        // action (sensors, climate, weather, etc. fall through to dashboard).
        if (primary is not OpenDashboardCommand)
        {
            items.Add(new CommandContextItem(new OpenDashboardCommand(_settings, entity.EntityId)));
        }
        items.Add(new CommandContextItem(new CopyTextCommand(entity.EntityId)
        {
            Name = "Copy entity ID",
        }));

        return items.ToArray();
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
