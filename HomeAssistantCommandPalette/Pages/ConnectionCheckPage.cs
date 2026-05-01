using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Diagnostic page that pings <c>/api/config</c> and surfaces the
/// information needed to debug a misconfigured instance: configured URL,
/// HA version, location, time zone, run state, round-trip latency, and
/// the active TLS option. Each row's primary action copies the value
/// to the clipboard so users can paste into bug reports.
/// </summary>
internal sealed partial class ConnectionCheckPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly HaApiClient _client;

    public ConnectionCheckPage(HaSettings settings, HaApiClient client)
    {
        _settings = settings;
        _client = client;
        Icon = Icons.App;
        Title = "Connection Check";
        Name = "Open";
        Id = "ha.connection-check";
        PlaceholderText = "Connection diagnostics";
    }

    public override IListItem[] GetItems()
    {
        var probe = _client.ProbeConfig();
        var items = new List<IListItem>(8);

        items.Add(Row("Status", FormatStatus(probe), Icons.App));
        items.Add(Row("URL", string.IsNullOrEmpty(_settings.Url) ? "(not set)" : _settings.Url, Icons.Home));
        if (probe.Success)
        {
            if (!string.IsNullOrEmpty(probe.Version)) items.Add(Row("Version", probe.Version!, Icons.App));
            if (!string.IsNullOrEmpty(probe.LocationName)) items.Add(Row("Location", probe.LocationName!, Icons.Home));
            if (!string.IsNullOrEmpty(probe.TimeZone)) items.Add(Row("Time zone", probe.TimeZone!, Icons.App));
            if (!string.IsNullOrEmpty(probe.State)) items.Add(Row("Run state", probe.State!, Icons.App));
        }
        items.Add(Row("Latency", probe.LatencyMs > 0 ? $"{probe.LatencyMs} ms" : "—", Icons.App));
        // Never expose the token. The length alone is enough to tell users
        // their token didn't get truncated when they pasted it in.
        items.Add(Row("Token length", _settings.Token.Length > 0 ? $"{_settings.Token.Length} chars" : "(not set)", Icons.App));
        items.Add(Row("Ignore TLS errors", _settings.IgnoreCertificateErrors ? "yes" : "no", Icons.App));

        // Touching GetStates exercises the area-map fetch as a side effect,
        // so the diagnostic below reflects the latest attempt.
        if (probe.Success)
        {
            _ = _client.GetStates();
            items.Add(Row("Areas resolved", FormatAreaCount(_client.LastAreaCount, _client.LastAreaError), Icons.Home));
            if (!string.IsNullOrEmpty(_client.LastAreaError))
            {
                items.Add(Row("Areas error", _client.LastAreaError, Icons.App));
            }
        }

        if (!probe.Success && probe.ErrorKind is HaErrorKind.NotConfigured or HaErrorKind.Unauthorized or HaErrorKind.InvalidUrl)
        {
            items.Add(new ListItem((ICommand)_settings.Settings.SettingsPage)
            {
                Title = "Open extension settings",
                Subtitle = "Fix the URL or token here",
                Icon = Icons.App,
            });
        }

        return items.ToArray();
    }

    private static ListItem Row(string title, string value, IconInfo icon) =>
        new(new CopyTextCommand(value) { Name = "Copy value" })
        {
            Title = title,
            Subtitle = value,
            Icon = icon,
        };

    private static string FormatStatus(HaConfigProbe probe)
    {
        if (probe.Success) return "Connected";
        return probe.ErrorKind switch
        {
            HaErrorKind.NotConfigured => "Not configured",
            HaErrorKind.Unauthorized => "Unauthorized — bad token",
            HaErrorKind.InvalidUrl => "Invalid URL",
            HaErrorKind.NetworkError => $"Unreachable — {probe.ErrorMessage}",
            HaErrorKind.ParseFailed => $"Bad response — {probe.ErrorMessage}",
            _ => probe.ErrorMessage ?? "Failed",
        };
    }

    private static string FormatAreaCount(int count, string error)
    {
        return count switch
        {
            -1 => string.IsNullOrEmpty(error) ? "Failed" : "Failed (see error below)",
            0 => "0 — no entities have areas assigned",
            _ => $"{count} entit{(count == 1 ? "y" : "ies")} mapped",
        };
    }
}
