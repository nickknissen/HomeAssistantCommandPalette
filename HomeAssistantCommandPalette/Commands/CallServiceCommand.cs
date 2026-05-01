using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Commands;

internal sealed partial class CallServiceCommand : InvokableCommand
{
    private readonly HaApiClient _client;
    private readonly string _domain;
    private readonly string _service;
    private readonly string _entityId;
    private readonly string _displayName;

    public CallServiceCommand(
        HaApiClient client,
        string domain,
        string service,
        string entityId,
        string displayName,
        string? iconRelativePath = null)
    {
        _client = client;
        _domain = domain;
        _service = service;
        _entityId = entityId;
        _displayName = displayName;
        Icon = IconHelpers.FromRelativePath(iconRelativePath ?? "Assets\\Square44x44Logo.scale-200.png");
    }

    public override string Name => _displayName;

    public override CommandResult Invoke()
    {
        if (_client.TryCallService(_domain, _service, _entityId, out var error))
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = $"{_displayName}: {_entityId}",
                Result = CommandResult.KeepOpen(),
            });
        }

        return CommandResult.ShowToast(new ToastArgs
        {
            Message = $"Failed: {error}",
            Result = CommandResult.KeepOpen(),
        });
    }
}
