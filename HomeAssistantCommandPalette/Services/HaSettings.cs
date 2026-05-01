using System;
using System.IO;
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

    public HaSettings()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_urlSetting);
        Settings.Add(_tokenSetting);
        Settings.Add(_ignoreCertSetting);

        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }

    public string Url => (_urlSetting.Value ?? string.Empty).Trim().TrimEnd('/');

    public string Token => (_tokenSetting.Value ?? string.Empty).Trim();

    public bool IgnoreCertificateErrors => _ignoreCertSetting.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Token);

    private static string SettingsJsonPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, Namespaced);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "settings.json");
    }
}
