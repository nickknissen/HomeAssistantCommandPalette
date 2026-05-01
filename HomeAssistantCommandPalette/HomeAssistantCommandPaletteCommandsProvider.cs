// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HomeAssistantCommandPalette.Pages;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette;

public partial class HomeAssistantCommandPaletteCommandsProvider : CommandProvider
{
    private readonly HaSettings _settings;
    private readonly HaApiClient _apiClient;
    private readonly ICommandItem[] _commands;

    public HomeAssistantCommandPaletteCommandsProvider()
    {
        DisplayName = "Home Assistant";
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-200.png");

        _settings = new HaSettings();
        _apiClient = new HaApiClient(_settings);
        Settings = _settings.Settings;

        _commands = [
            new CommandItem(new HomeAssistantPage(_settings, _apiClient))
            {
                Title = "Home Assistant",
                // "ha" surfaces this entry when users type the alias — CmdPal
                // search hits Title and Subtitle.
                Subtitle = "ha — Smart home control",
            },
        ];
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }
}
