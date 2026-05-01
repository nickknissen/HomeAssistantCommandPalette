using System;
using System.Diagnostics;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Commands;

internal sealed partial class OpenDashboardCommand : InvokableCommand
{
    private readonly HaSettings _settings;
    private readonly string? _entityId;

    public OpenDashboardCommand(HaSettings settings, string? entityId = null)
    {
        _settings = settings;
        _entityId = entityId;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.scale-200.png");
    }

    public override string Name => _entityId is null ? "Open dashboard" : "Open in dashboard";

    public override CommandResult Invoke()
    {
        if (string.IsNullOrEmpty(_settings.Url))
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = "Set the Home Assistant URL in extension settings first.",
                Result = CommandResult.KeepOpen(),
            });
        }

        try
        {
            var url = _entityId is null
                ? _settings.Url
                : $"{_settings.Url}/_my_redirect/more_info?entity_id={Uri.EscapeDataString(_entityId)}";
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(new ToastArgs
            {
                Message = $"Couldn't open browser: {ex.Message}",
                Result = CommandResult.KeepOpen(),
            });
        }
    }
}
