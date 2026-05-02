using System;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Commands;

/// <summary>
/// Ships <paramref name="query"/> off to <c>POST /api/conversation/process</c>
/// and hands the parsed result back to the page via <c>onResult</c> so it
/// can re-render. Keeps the palette open after invocation — Assist results
/// are usually a single line of text the user wants to read in-place.
/// </summary>
internal sealed partial class AskAssistCommand : InvokableCommand
{
    private readonly IHaClient _client;
    private readonly string _query;
    private readonly Action<HaAssistResult> _onResult;

    public AskAssistCommand(IHaClient client, string query, Action<HaAssistResult> onResult)
    {
        _client = client;
        _query = query;
        _onResult = onResult;
        Icon = Icons.App;
    }

    public override string Name => "Ask";

    public override CommandResult Invoke()
    {
        var result = _client.AskAssist(_query);
        _onResult(result);
        return CommandResult.KeepOpen();
    }
}
