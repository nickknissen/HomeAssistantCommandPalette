// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Pages;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette;

public partial class HomeAssistantCommandPaletteCommandsProvider : CommandProvider
{
    // Each tuple becomes one top-level CommandItem. Modeled on the Raycast
    // homeassistant extension's command list. `Domains = null` means "no
    // filter" (the All Entities entry).
    private sealed record DomainPage(
        string Title,
        string Id,
        IReadOnlyCollection<string>? Domains);

    private static readonly DomainPage[] DomainPages =
    [
        new("All Entities",   "ha.all-entities",     null),
        new("Lights",         "ha.lights",           ["light"]),
        new("Switches",       "ha.switches",         ["switch"]),
        new("Covers",         "ha.covers",           ["cover"]),
        new("Fans",           "ha.fans",             ["fan"]),
        new("Media Players",  "ha.media-players",    ["media_player"]),
        new("Scenes",         "ha.scenes",           ["scene"]),
        new("Scripts",        "ha.scripts",          ["script"]),
        new("Automations",    "ha.automations",      ["automation"]),
        new("Sensors",        "ha.sensors",          ["sensor"]),
        new("Binary Sensors", "ha.binary-sensors",   ["binary_sensor"]),
        new("Climate",        "ha.climate",          ["climate"]),
        new("Buttons",        "ha.buttons",          ["button", "input_button"]),
        new("Persons",        "ha.persons",          ["person"]),
        new("Zones",          "ha.zones",            ["zone"]),
        new("Cameras",        "ha.cameras",          ["camera"]),
        new("Vacuums",        "ha.vacuums",          ["vacuum"]),
        new("Helpers",        "ha.helpers",
            [
                "input_boolean", "input_number", "input_select",
                "input_text", "input_datetime", "counter", "timer",
            ]),
        new("Updates",        "ha.updates",          ["update"]),
        new("Weather",        "ha.weather",          ["weather"]),
    ];

    private readonly HaSettings _settings;
    private readonly HaApiClient _apiClient;
    private readonly ICommandItem[] _commands;

    public HomeAssistantCommandPaletteCommandsProvider()
    {
        DisplayName = "Home Assistant";
        Icon = Icons.App;

        _settings = new HaSettings();
        _apiClient = new HaApiClient(_settings);
        Settings = _settings.Settings;

        var commands = new List<ICommandItem>(DomainPages.Length + 1);

        foreach (var page in DomainPages)
        {
            commands.Add(new CommandItem(new EntityListPage(_settings, _apiClient, page.Title, page.Id, page.Domains, Icons.App))
            {
                Title = page.Title,
                Subtitle = "Home Assistant",
                Icon = Icons.App,
            });
        }

        commands.Add(new CommandItem(new OpenDashboardCommand(_settings))
        {
            Title = "Open Dashboard",
            Subtitle = "Home Assistant",
            Icon = Icons.App,
        });

        _commands = commands.ToArray();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
