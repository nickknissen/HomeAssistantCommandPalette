using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Free-form Assist page. The CmdPal search bar acts as the input field —
/// type a question, press Enter on the "Ask: …" row, and the speech
/// response renders below. The most recent exchange sticks around so the
/// user can re-read it while typing the next query.
/// </summary>
internal sealed partial class AssistPage : DynamicListPage
{
    private readonly HaSettings _settings;
    private readonly HaApiClient _client;

    private string? _lastQuery;
    private HaAssistResult? _lastResult;

    public AssistPage(HaSettings settings, HaApiClient client)
    {
        _settings = settings;
        _client = client;
        Icon = Icons.App;
        Title = "Assist";
        Name = "Open";
        Id = "ha.assist";
        PlaceholderText = "Ask Home Assistant…";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        // The search box IS the input field. Re-render so the "Ask: …" row
        // tracks what the user has typed.
        RaiseItemsChanged(0);
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
                Icon = Icons.App,
            }];
        }

        var items = new List<IListItem>(2);

        var query = SearchText?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(query))
        {
            items.Add(new ListItem(new AskAssistCommand(_client, query, OnAssistResult))
            {
                Title = $"Ask: {query}",
                Subtitle = "Press Enter to send to Home Assistant Assist",
                Icon = Icons.App,
            });
        }

        if (_lastResult is { } result)
        {
            items.Add(BuildResultItem(_lastQuery ?? string.Empty, result));
        }

        if (items.Count == 0)
        {
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = "Ask Home Assistant Assist",
                Subtitle = "Type a question above, then press Enter on the \"Ask: …\" row.",
                Icon = Icons.App,
            });
        }

        return items.ToArray();
    }

    private void OnAssistResult(HaAssistResult result)
    {
        _lastQuery = SearchText?.Trim();
        _lastResult = result;
        RaiseItemsChanged(0);
    }

    private static ListItem BuildResultItem(string query, HaAssistResult result)
    {
        var title = result.Success
            ? (string.IsNullOrEmpty(result.Speech) ? "(no spoken response)" : result.Speech)
            : (result.Error ?? "Assist returned an error.");
        var subtitle = result.Success
            ? $"Response to: {query}"
            : $"Error · {query}";

        // Activate copies the speech / error text so the user can paste it
        // elsewhere; CopyTextCommand handles the clipboard plumbing.
        return new ListItem(new CopyTextCommand(title) { Name = "Copy response" })
        {
            Title = title,
            Subtitle = subtitle,
            Icon = Icons.App,
        };
    }
}
