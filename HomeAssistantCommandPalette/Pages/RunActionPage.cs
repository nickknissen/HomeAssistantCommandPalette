using System.Globalization;
using System.Linq;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Entry point for the Run Action drilldown (issue #16). Lists every
/// domain HA exposes via <c>/api/services</c>, with the action count
/// per domain. Drilling into a domain opens
/// <see cref="DomainActionsPage"/>.
/// </summary>
internal sealed partial class RunActionPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly IHaClient _client;

    public RunActionPage(HaSettings settings, IHaClient client)
    {
        _settings = settings;
        _client = client;
        Title = "Run Action";
        Name = "Open";
        Id = "ha.run-action";
        Icon = Icons.App;
        PlaceholderText = "Search domains";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        if (!_settings.IsConfigured)
        {
            var openSettings = (ICommand)_settings.Settings.SettingsPage;
            return [new ListItem(openSettings)
            {
                Title = "Home Assistant not configured",
                Subtitle = "Press Enter to open settings and add your URL + access token.",
            }];
        }

        var actions = _client.GetActions();
        if (actions.Count == 0)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = "No actions available",
                Subtitle = "HA returned an empty services registry. Check your connection.",
            }];
        }

        return actions
            .GroupBy(a => a.Domain, System.StringComparer.Ordinal)
            .OrderBy(g => g.Key, System.StringComparer.Ordinal)
            .Select(g => (IListItem)new ListItem(new DomainActionsPage(_client, g.Key))
            {
                Title = g.Key,
                Subtitle = $"{g.Count().ToString(CultureInfo.InvariantCulture)} action{(g.Count() == 1 ? string.Empty : "s")}",
                Icon = Icons.App,
            })
            .ToArray();
    }
}
