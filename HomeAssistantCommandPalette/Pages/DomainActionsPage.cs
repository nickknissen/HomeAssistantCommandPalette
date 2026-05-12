using System.Linq;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Forms;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Lists every action declared by a given HA domain (issue #16, step 2
/// of the Run Action drilldown). Picking an action opens the
/// <see cref="HelperFormPage"/> with an <see cref="ActionFormContent"/>.
/// </summary>
internal sealed partial class DomainActionsPage : ListPage
{
    private readonly IHaClient _client;
    private readonly string _domain;

    public DomainActionsPage(IHaClient client, string domain)
    {
        _client = client;
        _domain = domain;
        Title = $"{domain} actions";
        Name = "Open";
        Id = "ha.actions." + domain;
        Icon = Icons.App;
        PlaceholderText = "Search actions";
        ShowDetails = false;
    }

    public override IListItem[] GetItems()
    {
        var actions = _client.GetActions()
            .Where(a => string.Equals(a.Domain, _domain, System.StringComparison.Ordinal))
            .OrderBy(a => a.Service, System.StringComparer.Ordinal)
            .ToArray();
        if (actions.Length == 0)
        {
            return [new ListItem(new NoOpCommand())
            {
                Title = "No actions in this domain",
                Subtitle = "The services registry returned nothing for " + _domain,
            }];
        }

        return actions.Select(a => (IListItem)new ListItem(BuildFormPage(a))
        {
            Title = string.IsNullOrWhiteSpace(a.Name) ? a.Service : a.Name,
            Subtitle = string.IsNullOrWhiteSpace(a.Description) ? $"{a.Domain}.{a.Service}" : a.Description,
            Icon = Icons.App,
        }).ToArray();
    }

    private ICommand BuildFormPage(HaAction action)
        => new HelperFormPage(
            new ActionFormContent(_client, OnSuccess, action.Domain, action.Service, action.Fields, action.HasTarget),
            $"Run {action.Domain}.{action.Service}",
            $"ha.action.{action.Domain}.{action.Service}",
            Icons.App);

    private static void OnSuccess() { }
}
