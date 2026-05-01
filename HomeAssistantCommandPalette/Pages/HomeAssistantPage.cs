using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

internal sealed partial class HomeAssistantPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly HaApiClient _client;

    public HomeAssistantPage(HaSettings settings, HaApiClient client)
    {
        _settings = settings;
        _client = client;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-200.png");
        Title = "Home Assistant";
        Name = "Open";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        var result = _client.GetStates();
        if (result.HasError)
        {
            return [
                new ListItem(new NoOpCommand())
                {
                    Title = result.ErrorTitle,
                    Subtitle = result.ErrorDescription,
                }
            ];
        }

        return result.Items.Select(CreateItem).ToArray();
    }

    private ListItem CreateItem(HaEntity entity)
    {
        var defaultCommand = BuildPrimaryCommand(entity);
        var more = BuildContextCommands(entity);

        return new ListItem(defaultCommand)
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            MoreCommands = more,
        };
    }

    private ICommand BuildPrimaryCommand(HaEntity entity)
    {
        // Default action picked per-domain. Falls back to "open in dashboard"
        // for read-only or unsupported domains.
        return entity.Domain switch
        {
            "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "cover" or "media_player"
                => new CallServiceCommand(_client, entity.Domain, "toggle", entity.EntityId, $"Toggle {entity.FriendlyName}"),
            "scene"
                => new CallServiceCommand(_client, "scene", "turn_on", entity.EntityId, $"Activate {entity.FriendlyName}"),
            "script"
                => new CallServiceCommand(_client, "script", "turn_on", entity.EntityId, $"Run {entity.FriendlyName}"),
            "button" or "input_button"
                => new CallServiceCommand(_client, entity.Domain, "press", entity.EntityId, $"Press {entity.FriendlyName}"),
            _ => new OpenDashboardCommand(_settings, entity.EntityId),
        };
    }

    private IContextItem[] BuildContextCommands(HaEntity entity)
    {
        var items = new List<IContextItem>(4);

        // For toggleable domains, expose explicit on/off in context — handy
        // when you don't want "toggle" semantics.
        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "media_player")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_on", entity.EntityId, $"Turn on {entity.FriendlyName}")));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_off", entity.EntityId, $"Turn off {entity.FriendlyName}")));
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
        var unit = entity.Attributes.TryGetValue("unit_of_measurement", out var u) && u is string s ? s : null;
        var stateText = string.IsNullOrEmpty(entity.State)
            ? "(no state)"
            : (unit is null ? entity.State : $"{entity.State} {unit}");
        return $"{entity.EntityId} • {stateText}";
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
