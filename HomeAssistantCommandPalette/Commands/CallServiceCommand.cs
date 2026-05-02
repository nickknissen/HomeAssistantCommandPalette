using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Commands;

internal sealed partial class CallServiceCommand : InvokableCommand
{
    private readonly IHaClient _client;
    private readonly string _domain;
    private readonly string _service;
    private readonly string _entityId;
    private readonly string _displayName;
    private readonly IReadOnlyDictionary<string, object?>? _extraData;
    private readonly Action? _onSuccess;

    public CallServiceCommand(
        IHaClient client,
        string domain,
        string service,
        string entityId,
        string displayName,
        IconInfo? icon = null,
        IReadOnlyDictionary<string, object?>? extraData = null,
        Action? onSuccess = null)
    {
        _client = client;
        _domain = domain;
        _service = service;
        _entityId = entityId;
        _displayName = displayName;
        _extraData = extraData;
        _onSuccess = onSuccess;
        Icon = icon ?? Icons.App;
    }

    public override string Name => _displayName;

    public override CommandResult Invoke()
    {
        if (_client.TryCallService(_domain, _service, _entityId, _extraData, out var error))
        {
            // No success toast: the list refresh + icon/state update is the
            // confirmation. A toast on top is noise.
            _onSuccess?.Invoke();
            return CommandResult.KeepOpen();
        }

        return CommandResult.ShowToast(new ToastArgs
        {
            Message = $"Failed: {error}",
            Result = CommandResult.KeepOpen(),
        });
    }
}
