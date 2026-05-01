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
        IReadOnlyCollection<string>? domains = null)
    {
        _settings = settings;
        _client = client;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);

        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-200.png");
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
            MoreCommands = BuildContextCommands(entity),
        };
    }

    private ICommand BuildPrimaryCommand(HaEntity entity)
    {
        // Default action picked per-domain. Falls back to "open in dashboard"
        // for read-only or unsupported domains.
        return entity.Domain switch
        {
            "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "cover" or "media_player"
                => new CallServiceCommand(_client, entity.Domain, "toggle", entity.EntityId, $"Toggle {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "scene"
                => new CallServiceCommand(_client, "scene", "turn_on", entity.EntityId, $"Activate {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "script"
                => new CallServiceCommand(_client, "script", "turn_on", entity.EntityId, $"Run {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "button" or "input_button"
                => new CallServiceCommand(_client, entity.Domain, "press", entity.EntityId, $"Press {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            _ => new OpenDashboardCommand(_settings, entity.EntityId),
        };
    }

    private IContextItem[] BuildContextCommands(HaEntity entity)
    {
        var items = new List<IContextItem>(4);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "media_player")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_on", entity.EntityId, $"Turn on {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_off", entity.EntityId, $"Turn off {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded)));
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

    private static Tag[] BuildTags(HaEntity entity)
    {
        var domainTag = new Tag(entity.Domain) { ToolTip = "Domain" };

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "media_player" or "binary_sensor" or "cover")
        {
            return entity.IsOn
                ? [
                    new Tag("ON")
                    {
                        Background = ColorHelpers.FromArgb(255, 76, 161, 222),
                        Foreground = ColorHelpers.FromRgb(255, 255, 255),
                    },
                    domainTag,
                ]
                : [
                    new Tag("OFF")
                    {
                        Background = ColorHelpers.FromRgb(120, 120, 120),
                        Foreground = ColorHelpers.FromRgb(255, 255, 255),
                    },
                    domainTag,
                ];
        }

        return [domainTag];
    }
}
