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
        // The response renders in the details side pane; without this it
        // stays collapsed by default and the user has to toggle it on.
        ShowDetails = true;
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

        var query = SearchText?.Trim() ?? string.Empty;
        var details = _lastResult is { } r ? BuildResponseDetails(_lastQuery, r) : null;

        if (!string.IsNullOrEmpty(query))
        {
            // Send-action row. Details panel carries the most-recent
            // exchange so the user can re-read the answer while typing
            // the next question.
            return [new ListItem(new AskAssistCommand(_client, query, OnAssistResult))
            {
                Title = $"Ask: {query}",
                Subtitle = "Press Enter to send to Home Assistant Assist",
                Icon = Icons.App,
                Details = details,
            }];
        }

        if (details is not null && _lastQuery is not null)
        {
            // Empty search, but we have a previous answer — keep it
            // visible. Activating re-asks the same question.
            return [new ListItem(new AskAssistCommand(_client, _lastQuery, OnAssistResult))
            {
                Title = _lastQuery,
                Subtitle = "Last answer · press Enter to ask again",
                Icon = Icons.App,
                Details = details,
            }];
        }

        return [new ListItem(new NoOpCommand())
        {
            Title = "Ask Home Assistant Assist",
            Subtitle = "Type a question above, then press Enter on the \"Ask: …\" row.",
            Icon = Icons.App,
        }];
    }

    private void OnAssistResult(HaAssistResult result)
    {
        _lastQuery = SearchText?.Trim();
        if (string.IsNullOrEmpty(_lastQuery)) _lastQuery = null;
        _lastResult = result;
        RaiseItemsChanged(0);
    }

    private static Details BuildResponseDetails(string? query, HaAssistResult result)
    {
        var speech = result.Success
            ? (string.IsNullOrEmpty(result.Speech) ? "(no spoken response)" : result.Speech)
            : (result.Error ?? "Assist returned an error.");

        // Body is rendered as Markdown by CmdPal — long responses wrap
        // and stay readable, unlike a single-line subtitle.
        var body = result.Success ? speech : $"**Error**\n\n{speech}";

        var meta = new List<IDetailsElement>();
        if (!string.IsNullOrEmpty(query)) meta.Add(Row("Question", query));
        if (!string.IsNullOrEmpty(result.ResponseType)) meta.Add(Row("Type", result.ResponseType));

        return new Details
        {
            Title = result.Success ? "Assist" : "Assist error",
            Body = body,
            Metadata = meta.ToArray(),
        };
    }

    private static DetailsElement Row(string key, string value) => new()
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };
}
