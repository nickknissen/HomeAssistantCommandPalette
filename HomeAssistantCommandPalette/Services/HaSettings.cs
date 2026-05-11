using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Services;

/// <summary>
/// Persisted user settings: Home Assistant base URL + long-lived access token.
/// Token is stored as Multiline TextSetting (CmdPal toolkit has no
/// password-style setting today — note in README that the file is local-user
/// only).
/// </summary>
public sealed class HaSettings : JsonSettingsManager
{
    private const string Namespaced = "HomeAssistantCommandPalette";

    private readonly TextSetting _urlSetting = new(
        "ha-url",
        "Home Assistant URL",
        "Base URL of your Home Assistant instance (e.g. http://homeassistant.local:8123)",
        string.Empty)
    {
        Placeholder = "http://homeassistant.local:8123",
        IsRequired = true,
    };

    private readonly TextSetting _tokenSetting = new(
        "ha-token",
        "Long-Lived Access Token",
        "Create one in Home Assistant under Profile → Long-Lived Access Tokens.",
        string.Empty)
    {
        Placeholder = "eyJhbGciOi...",
        IsRequired = true,
    };

    private readonly ToggleSetting _ignoreCertSetting = new(
        "ha-ignore-cert",
        "Ignore TLS certificate errors",
        "Useful for self-signed certs on a LAN-only instance. Leave off if your HA is on the public internet.",
        false);

    private readonly TextSetting _dashboardPathSetting = new(
        "ha-dashboard-path",
        "Dashboard path (optional)",
        "Path opened by 'Open Dashboard'. Leave empty to open the HA root.",
        string.Empty)
    {
        Placeholder = "/my-dashboard",
    };

    private readonly ToggleSetting _showEntityIdSetting = new(
        "ha-show-entity-id",
        "Show entity IDs as subtitle",
        "Replace the area name in the list subtitle with the raw entity_id (e.g. light.kitchen). Useful when wiring up automations.",
        false);

    private readonly ToggleSetting _hideUnavailableSetting = new(
        "ha-hide-unavailable",
        "Hide unavailable entities",
        "Filter out entities whose state is 'unavailable' from all list pages.",
        false);

    private readonly TextSetting _cameraRefreshIntervalSetting = new(
        "ha-camera-refresh-interval-ms",
        "Camera refresh interval (ms)",
        "How often the Cameras page refreshes snapshots. Default: 3000. Set to 0 to disable auto-refresh.",
        "3000")
    {
        Placeholder = "3000",
    };

    private readonly TextSetting _customHeadersSetting = new(
        "ha-custom-headers",
        "Custom HTTP headers (optional)",
        "One 'Header: Value' pair per line for proxies such as Cloudflare Access. Values may be sensitive.",
        string.Empty)
    {
        Placeholder = "CF-Access-Client-Id: ...\nCF-Access-Client-Secret: ...",
    };

    public HaSettings()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_urlSetting);
        Settings.Add(_tokenSetting);
        Settings.Add(_ignoreCertSetting);
        Settings.Add(_dashboardPathSetting);
        Settings.Add(_showEntityIdSetting);
        Settings.Add(_hideUnavailableSetting);
        Settings.Add(_cameraRefreshIntervalSetting);
        Settings.Add(_customHeadersSetting);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public string Url => (_urlSetting.Value ?? string.Empty).Trim().TrimEnd('/');

    public string Token => (_tokenSetting.Value ?? string.Empty).Trim();

    public bool IgnoreCertificateErrors => _ignoreCertSetting.Value;

    public string DashboardPath
    {
        get
        {
            var raw = (_dashboardPathSetting.Value ?? string.Empty).Trim();
            if (raw.Length == 0)
            {
                return string.Empty;
            }
            return raw.StartsWith('/') ? raw : "/" + raw;
        }
    }

    public bool ShowEntityId => _showEntityIdSetting.Value;

    public bool HideUnavailable => _hideUnavailableSetting.Value;

    public int CameraRefreshIntervalMs
    {
        get
        {
            var raw = (_cameraRefreshIntervalSetting.Value ?? string.Empty).Trim();
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0
                ? value
                : 3000;
        }
    }

    public IReadOnlyDictionary<string, string> CustomHeaders => ParseCustomHeaders(_customHeadersSetting.Value ?? string.Empty);

    public string CustomHeadersFingerprint => string.Join("\n", CustomHeaders.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + ":" + kv.Value));

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Token);

    private static Dictionary<string, string> ParseCustomHeaders(string raw)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in raw.Replace("\r", string.Empty).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var colon = trimmed.IndexOf(':');
            if (colon <= 0) continue;

            var name = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim();
            if (name.Length == 0 || value.Length == 0) continue;
            if (string.Equals(name, "Authorization", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Any(char.IsWhiteSpace)) continue;

            headers[name] = value;
        }
        return headers;
    }

    private static string SettingsJsonPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, Namespaced);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }
}
