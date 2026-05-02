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
/// <remarks>
/// Per-entity rendering — icon, primary command, context items, detail
/// rows, hero image — is delegated to the <see cref="DomainBehavior"/>
/// resolved by <see cref="DomainRegistry"/>. The page itself owns only:
/// fetch + filter + sort, error rendering, the refresh callback after a
/// successful service call, the person-avatar wrap, and the page-level
/// subtitle / tags.
/// </remarks>
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
            Icon = ResolveEntityIcon(entity, behavior.BuildIcon(in ctx)),
            MoreCommands = ctxItems.ToArray(),
            Details = details,
        };
    }

    /// <summary>
    /// Wraps a behavior-supplied icon with the page-level person-avatar
    /// override. Persons get their <c>entity_picture</c> avatar (Gravatar
    /// or HA-served) when one is set; if the fetch fails we drop back to
    /// the supplied icon. The override lives on the page (not in
    /// <see cref="Behaviors.PersonBehavior"/>) because it requires
    /// authenticated HTTP through <see cref="IHaClient"/>.
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
