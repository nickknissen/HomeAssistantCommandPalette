using System;
using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
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
    private readonly HaApiClient _client;
    private readonly HashSet<string>? _domains;

    public EntityListPage(
        HaSettings settings,
        HaApiClient client,
        string title,
        string id,
        IReadOnlyCollection<string>? domains = null,
        IconInfo? icon = null)
    {
        _settings = settings;
        _client = client;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);

        Icon = icon ?? Icons.App;
        Title = title;
        Name = "Open";
        Id = id;
        ShowDetails = false;
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

        var items = _domains is null
            ? result.Items
            : result.Items.Where(e => _domains.Contains(e.Domain));

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
        return new ListItem(BuildPrimaryCommand(entity))
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            Icon = IconForEntity(entity),
            MoreCommands = BuildContextCommands(entity),
        };
    }

    private static IconInfo IconForEntity(HaEntity entity)
    {
        if (entity.Domain != "light") return Icons.App;

        // Detect a lights group via the standard HA `mdi:lightbulb-group`
        // entity icon override. Falls back to a single bulb otherwise.
        var isGroup = entity.Attributes.TryGetValue("icon", out var ic)
            && ic is string s
            && string.Equals(s, "mdi:lightbulb-group", System.StringComparison.OrdinalIgnoreCase);

        var unavailable = string.Equals(entity.State, "unavailable", System.StringComparison.OrdinalIgnoreCase);
        if (unavailable)
        {
            return isGroup ? Icons.LightGroupUnavailable : Icons.LightUnavailable;
        }
        if (entity.IsOn)
        {
            return isGroup ? Icons.LightGroupOn : Icons.LightOn;
        }
        return isGroup ? Icons.LightGroupOff : Icons.LightOff;
    }

    private ICommand BuildPrimaryCommand(HaEntity entity)
    {
        // Default action picked per-domain. Falls back to "open in dashboard"
        // for read-only or unsupported domains.
        return entity.Domain switch
        {
            "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "cover" or "media_player"
                => new CallServiceCommand(_client, entity.Domain, "toggle", entity.EntityId, $"Toggle {entity.FriendlyName}", icon: Icons.Toggle, onSuccess: OnServiceCallSucceeded),
            "scene"
                => new CallServiceCommand(_client, "scene", "turn_on", entity.EntityId, $"Activate {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "script"
                => new CallServiceCommand(_client, "script", "turn_on", entity.EntityId, $"Run {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "button" or "input_button"
                => new CallServiceCommand(_client, entity.Domain, "press", entity.EntityId, $"Press {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            _ => new OpenDashboardCommand(_settings, entity.EntityId),
        };
    }

    private CommandContextItem BrightnessPreset(HaEntity entity, int pct) =>
        new(new CallServiceCommand(
            _client,
            domain: "light",
            service: "turn_on",
            entityId: entity.EntityId,
            displayName: $"{pct}%",
            icon: Icons.Brightness,
            extraData: new Dictionary<string, object?> { ["brightness_pct"] = pct },
            onSuccess: OnServiceCallSucceeded));

    private IContextItem[] BuildContextCommands(HaEntity entity)
    {
        var items = new List<IContextItem>(8);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "media_player")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_on", entity.EntityId, $"Turn on {entity.FriendlyName}", icon: Icons.TurnOn, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_off", entity.EntityId, $"Turn off {entity.FriendlyName}", icon: Icons.TurnOff, onSuccess: OnServiceCallSucceeded)));
        }

        // Light brightness presets — nested under a single "Set brightness"
        // parent so the top-level context menu stays short. Each preset
        // calls light.turn_on with brightness_pct (HA turns the light on
        // at that level whether it was on or off).
        if (entity.Domain == "light")
        {
            var presets = new IContextItem[]
            {
                BrightnessPreset(entity, 25),
                BrightnessPreset(entity, 50),
                BrightnessPreset(entity, 75),
                BrightnessPreset(entity, 100),
            };
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set brightness…",
                Icon = Icons.Brightness,
                MoreCommands = presets,
            });
        }

        items.Add(new CommandContextItem(new OpenDashboardCommand(_settings, entity.EntityId)));
        items.Add(new CommandContextItem(new CopyTextCommand(entity.EntityId)
        {
            Name = "Copy entity ID",
        }));

        return items.ToArray();
    }

    private static string BuildSubtitle(HaEntity entity)
    {
        // Mirror the Raycast subtitle: area (room) name only. State stays
        // in the tags. Empty when the entity has no area — a clean list
        // beats noisy fallbacks.
        return entity.AreaName ?? string.Empty;
    }

    private Tag[] BuildTags(HaEntity entity)
    {
        // Hide the domain tag on single-domain pages (Lights, Covers, ...) —
        // it's redundant. Keep it on All Entities and multi-domain pages
        // (Buttons, Helpers) so users can tell entities apart.
        var showDomainTag = _domains is null || _domains.Count > 1;
        var tags = new List<Tag>(2);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "media_player" or "binary_sensor" or "cover")
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
