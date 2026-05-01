using System;
using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

/// <summary>
/// Generic list page that shows entities, optionally filtered to a fixed
/// set of domains. The single class backs every per-domain top-level
/// command (Lights, Covers, Scenes, ...) plus the unfiltered "All Entities".
/// </summary>
internal sealed partial class EntityListPage : ListPage
{
    private readonly HaSettings _settings;
    private readonly HaApiClient _client;
    private readonly HashSet<string>? _domains;

    public EntityListPage(
        HaSettings settings,
        HaApiClient client,
        string title,
        string id,
        IReadOnlyCollection<string>? domains = null,
        IconInfo? icon = null)
    {
        _settings = settings;
        _client = client;
        _domains = domains is null ? null : new HashSet<string>(domains, StringComparer.Ordinal);

        Icon = icon ?? Icons.App;
        Title = title;
        Name = "Open";
        Id = id;
        ShowDetails = true;
        PlaceholderText = $"Search {title.ToLowerInvariant()}";
    }

    public override IListItem[] GetItems()
    {
        var result = _client.GetStates();
        if (result.HasError)
        {
            // For configuration errors, make the error item itself navigate
            // to the settings page so the user can fix it in one click.
            var openSettings = (ICommand)_settings.Settings.SettingsPage;
            ICommand errorCommand = result.ErrorKind switch
            {
                HaErrorKind.NotConfigured or HaErrorKind.Unauthorized or HaErrorKind.InvalidUrl => openSettings,
                _ => new NoOpCommand(),
            };
            var subtitle = result.ErrorKind switch
            {
                HaErrorKind.NotConfigured => "Press Enter to open settings and add your URL + access token.",
                HaErrorKind.Unauthorized => "Press Enter to open settings and update your access token.",
                HaErrorKind.InvalidUrl => "Press Enter to open settings and fix the URL.",
                _ => result.ErrorDescription,
            };
            return [
                new ListItem(errorCommand)
                {
                    Title = result.ErrorTitle,
                    Subtitle = subtitle,
                }
            ];
        }

        var items = _domains is null
            ? result.Items
            : result.Items.Where(e => _domains.Contains(e.Domain));

        return items.Select(CreateItem).ToArray();
    }

    // HA dispatches services asynchronously — even after a 200 response,
    // the entity state we'd refetch may still be stale for a few hundred ms.
    // Wait briefly before signalling the list to refresh.
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(250);

    private void OnServiceCallSucceeded()
    {
        // Fire-and-forget: invalidate cache + tell CmdPal to re-call GetItems
        // after HA has had a moment to propagate the new state.
        System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(RefreshDelay).ConfigureAwait(false);
            try { RaiseItemsChanged(0); } catch { /* page may have been closed */ }
        });
    }

    private ListItem CreateItem(HaEntity entity)
    {
        return new ListItem(BuildPrimaryCommand(entity))
        {
            Title = entity.FriendlyName,
            Subtitle = BuildSubtitle(entity),
            Tags = BuildTags(entity),
            Icon = IconForEntity(entity),
            MoreCommands = BuildContextCommands(entity),
            Details = BuildDetails(entity),
        };
    }

    private static Details BuildDetails(HaEntity entity)
    {
        var meta = new List<IDetailsElement>
        {
            Row("State", entity.State),
        };

        if (entity.Domain == "light")
        {
            AddLightRows(entity, meta);
        }
        else if (entity.Domain == "cover")
        {
            AddCoverRows(entity, meta);
        }
        else if (entity.Domain == "media_player")
        {
            AddMediaPlayerRows(entity, meta);
        }

        if (!string.IsNullOrEmpty(entity.AreaName))
        {
            meta.Add(Row("Area", entity.AreaName));
        }
        meta.Add(Row("Entity ID", entity.EntityId));

        return new Details
        {
            Title = entity.FriendlyName,
            Metadata = meta.ToArray(),
        };
    }

    private static void AddLightRows(HaEntity entity, List<IDetailsElement> meta)
    {
        // brightness in HA states is 0-255
        if (entity.Attributes.TryGetValue("brightness", out var b) && b is long br && br > 0)
        {
            meta.Add(Row("Brightness", $"{(int)System.Math.Round(br / 255.0 * 100)}%"));
        }
        if (entity.Attributes.TryGetValue("color_temp_kelvin", out var ctk) && ctk is long k && k > 0)
        {
            meta.Add(Row("Color temp", $"{k}K"));
        }
        if (entity.Attributes.TryGetValue("rgb_color", out var rgb) && rgb is not null)
        {
            meta.Add(Row("RGB", rgb.ToString() ?? string.Empty));
        }
        if (entity.Attributes.TryGetValue("color_mode", out var mode) && mode is string m && !string.IsNullOrEmpty(m))
        {
            meta.Add(Row("Color mode", m));
        }
        if (entity.Attributes.TryGetValue("effect", out var fx) && fx is string fxs && !string.IsNullOrEmpty(fxs) && !string.Equals(fxs, "none", System.StringComparison.OrdinalIgnoreCase))
        {
            meta.Add(Row("Effect", fxs));
        }
    }

    private static void AddMediaPlayerRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("media_title", out var title) && title is string ts && !string.IsNullOrEmpty(ts))
        {
            meta.Add(Row("Track", ts));
        }
        if (entity.Attributes.TryGetValue("media_artist", out var artist) && artist is string ars && !string.IsNullOrEmpty(ars))
        {
            meta.Add(Row("Artist", ars));
        }
        if (entity.Attributes.TryGetValue("media_album_name", out var album) && album is string als && !string.IsNullOrEmpty(als))
        {
            meta.Add(Row("Album", als));
        }
        if (entity.Attributes.TryGetValue("source", out var source) && source is string src && !string.IsNullOrEmpty(src))
        {
            meta.Add(Row("Source", src));
        }
        // volume_level is 0.0..1.0
        if (entity.Attributes.TryGetValue("volume_level", out var vol) && vol is double v)
        {
            meta.Add(Row("Volume", $"{(int)System.Math.Round(v * 100)}%"));
        }
        else if (entity.Attributes.TryGetValue("volume_level", out var vol2) && vol2 is long lv)
        {
            meta.Add(Row("Volume", $"{lv * 100}%"));
        }
        if (entity.Attributes.TryGetValue("is_volume_muted", out var muted) && muted is bool m)
        {
            meta.Add(Row("Muted", m ? "yes" : "no"));
        }
        if (entity.Attributes.TryGetValue("app_name", out var app) && app is string apps && !string.IsNullOrEmpty(apps))
        {
            meta.Add(Row("App", apps));
        }
    }

    private static void AddCoverRows(HaEntity entity, List<IDetailsElement> meta)
    {
        if (entity.Attributes.TryGetValue("current_position", out var pos) && pos is long p)
        {
            meta.Add(Row("Position", $"{p}%"));
        }
        if (entity.Attributes.TryGetValue("current_tilt_position", out var tilt) && tilt is long t)
        {
            meta.Add(Row("Tilt", $"{t}%"));
        }
        if (entity.Attributes.TryGetValue("device_class", out var dc) && dc is string dcs && !string.IsNullOrEmpty(dcs))
        {
            meta.Add(Row("Device class", dcs));
        }
    }

    private static DetailsElement Row(string key, string value) => new()
    {
        Key = key,
        Data = new DetailsLink { Text = value },
    };

    private static IconInfo IconForEntity(HaEntity entity)
    {
        var unavailable = string.Equals(entity.State, "unavailable", System.StringComparison.OrdinalIgnoreCase);

        if (entity.Domain == "light")
        {
            // Detect a lights group via the standard HA `mdi:lightbulb-group`
            // entity icon override. Falls back to a single bulb otherwise.
            var isGroup = entity.Attributes.TryGetValue("icon", out var ic)
                && ic is string s
                && string.Equals(s, "mdi:lightbulb-group", System.StringComparison.OrdinalIgnoreCase);

            if (unavailable) return isGroup ? Icons.LightGroupUnavailable : Icons.LightUnavailable;
            if (entity.IsOn) return isGroup ? Icons.LightGroupOn : Icons.LightOn;
            return isGroup ? Icons.LightGroupOff : Icons.LightOff;
        }

        if (entity.Domain == "cover")
        {
            if (unavailable) return Icons.CoverUnavailable;
            return entity.State.ToLowerInvariant() switch
            {
                "opening" => Icons.CoverOpening,
                "closing" => Icons.CoverClosing,
                "closed" => Icons.CoverClosed,
                _ => Icons.CoverOpen, // open + unknown → open
            };
        }

        if (entity.Domain == "media_player")
        {
            if (unavailable) return Icons.MediaPlayerUnavailable;
            return string.Equals(entity.State, "playing", System.StringComparison.OrdinalIgnoreCase)
                ? Icons.MediaPlayerPlaying
                : Icons.MediaPlayerIdle;
        }

        return Icons.App;
    }

    private ICommand BuildPrimaryCommand(HaEntity entity)
    {
        // Default action picked per-domain. Falls back to "open in dashboard"
        // for read-only or unsupported domains.
        return entity.Domain switch
        {
            "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "cover" or "media_player"
                => new CallServiceCommand(_client, entity.Domain, "toggle", entity.EntityId, $"Toggle {entity.FriendlyName}", icon: Icons.Toggle, onSuccess: OnServiceCallSucceeded),
            "scene"
                => new CallServiceCommand(_client, "scene", "turn_on", entity.EntityId, $"Activate {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "script"
                => new CallServiceCommand(_client, "script", "turn_on", entity.EntityId, $"Run {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            "button" or "input_button"
                => new CallServiceCommand(_client, entity.Domain, "press", entity.EntityId, $"Press {entity.FriendlyName}", onSuccess: OnServiceCallSucceeded),
            _ => new OpenDashboardCommand(_settings, entity.EntityId),
        };
    }

    private CommandContextItem BrightnessPreset(HaEntity entity, int pct) =>
        new(new CallServiceCommand(
            _client,
            domain: "light",
            service: "turn_on",
            entityId: entity.EntityId,
            displayName: $"{pct}%",
            icon: Icons.Brightness,
            extraData: new Dictionary<string, object?> { ["brightness_pct"] = pct },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem CoverPositionPreset(HaEntity entity, int position) =>
        new(new CallServiceCommand(
            _client,
            domain: "cover",
            service: "set_cover_position",
            entityId: entity.EntityId,
            displayName: $"{position}%",
            icon: position == 0 ? Icons.Close : (position == 100 ? Icons.Open : Icons.Stop),
            extraData: new Dictionary<string, object?> { ["position"] = position },
            onSuccess: OnServiceCallSucceeded));

    private CommandContextItem VolumePreset(HaEntity entity, int pct) =>
        new(new CallServiceCommand(
            _client,
            domain: "media_player",
            service: "volume_set",
            entityId: entity.EntityId,
            displayName: $"{pct}%",
            icon: Icons.Volume,
            // volume_level wants 0.0..1.0 — convert from percentage.
            extraData: new Dictionary<string, object?> { ["volume_level"] = pct / 100.0 },
            onSuccess: OnServiceCallSucceeded));

    private IContextItem[] BuildContextCommands(HaEntity entity)
    {
        var items = new List<IContextItem>(8);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "group" or "media_player")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_on", entity.EntityId, $"Turn on {entity.FriendlyName}", icon: Icons.TurnOn, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, entity.Domain, "turn_off", entity.EntityId, $"Turn off {entity.FriendlyName}", icon: Icons.TurnOff, onSuccess: OnServiceCallSucceeded)));
        }

        // Light brightness presets — nested under a single "Set brightness"
        // parent so the top-level context menu stays short. Each preset
        // calls light.turn_on with brightness_pct (HA turns the light on
        // at that level whether it was on or off).
        if (entity.Domain == "light")
        {
            var presets = new IContextItem[]
            {
                BrightnessPreset(entity, 25),
                BrightnessPreset(entity, 50),
                BrightnessPreset(entity, 75),
                BrightnessPreset(entity, 100),
            };
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set brightness…",
                Icon = Icons.Brightness,
                MoreCommands = presets,
            });
        }

        if (entity.Domain == "media_player")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_play_pause", entity.EntityId, $"Play / Pause {entity.FriendlyName}", icon: Icons.PlayPause, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_play", entity.EntityId, $"Play {entity.FriendlyName}", icon: Icons.Play, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_pause", entity.EntityId, $"Pause {entity.FriendlyName}", icon: Icons.Pause, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_stop", entity.EntityId, $"Stop {entity.FriendlyName}", icon: Icons.Stop, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_next_track", entity.EntityId, $"Next track on {entity.FriendlyName}", icon: Icons.Next, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "media_previous_track", entity.EntityId, $"Previous track on {entity.FriendlyName}", icon: Icons.Previous, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "volume_up", entity.EntityId, $"Volume up on {entity.FriendlyName}", icon: Icons.VolumeUp, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "media_player", "volume_down", entity.EntityId, $"Volume down on {entity.FriendlyName}", icon: Icons.VolumeDown, onSuccess: OnServiceCallSucceeded)));

            // Mute toggle — read current is_volume_muted to flip it. Skip if
            // unknown (not all media_players publish the attribute).
            if (entity.Attributes.TryGetValue("is_volume_muted", out var muted) && muted is bool isMuted)
            {
                items.Add(new CommandContextItem(
                    new CallServiceCommand(
                        _client,
                        domain: "media_player",
                        service: "volume_mute",
                        entityId: entity.EntityId,
                        displayName: isMuted ? $"Unmute {entity.FriendlyName}" : $"Mute {entity.FriendlyName}",
                        icon: Icons.VolumeMute,
                        extraData: new Dictionary<string, object?> { ["is_volume_muted"] = !isMuted },
                        onSuccess: OnServiceCallSucceeded)));
            }

            // Volume presets — only when the entity supports volume_set.
            // supported_features bit 4 (value 4) = VOLUME_SET on media_player.
            if (entity.Attributes.TryGetValue("supported_features", out var sf) && sf is long bits && (bits & 4) == 4)
            {
                var presets = new IContextItem[]
                {
                    VolumePreset(entity, 25),
                    VolumePreset(entity, 50),
                    VolumePreset(entity, 75),
                    VolumePreset(entity, 100),
                };
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set volume…",
                    Icon = Icons.Volume,
                    MoreCommands = presets,
                });
            }
        }

        if (entity.Domain == "cover")
        {
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "cover", "open_cover", entity.EntityId, $"Open {entity.FriendlyName}", icon: Icons.Open, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "cover", "close_cover", entity.EntityId, $"Close {entity.FriendlyName}", icon: Icons.Close, onSuccess: OnServiceCallSucceeded)));
            items.Add(new CommandContextItem(
                new CallServiceCommand(_client, "cover", "stop_cover", entity.EntityId, $"Stop {entity.FriendlyName}", icon: Icons.Stop, onSuccess: OnServiceCallSucceeded)));

            // Position presets only when the cover supports set_cover_position
            // (HA exposes this via the supported_features bitmask; bit 2 = 4
            // means SET_POSITION). Falls back to skipping the menu silently.
            if (entity.Attributes.TryGetValue("supported_features", out var sf) && sf is long bits && (bits & 4) == 4)
            {
                var presets = new IContextItem[]
                {
                    CoverPositionPreset(entity, 0),
                    CoverPositionPreset(entity, 25),
                    CoverPositionPreset(entity, 50),
                    CoverPositionPreset(entity, 75),
                    CoverPositionPreset(entity, 100),
                };
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set position…",
                    Icon = Icons.Stop,
                    MoreCommands = presets,
                });
            }
        }

        items.Add(new CommandContextItem(new OpenDashboardCommand(_settings, entity.EntityId)));
        items.Add(new CommandContextItem(new CopyTextCommand(entity.EntityId)
        {
            Name = "Copy entity ID",
        }));

        return items.ToArray();
    }

    private static string BuildSubtitle(HaEntity entity)
    {
        // Mirror the Raycast subtitle: area (room) name only. State stays
        // in the tags. Empty when the entity has no area — a clean list
        // beats noisy fallbacks.
        return entity.AreaName ?? string.Empty;
    }

    private Tag[] BuildTags(HaEntity entity)
    {
        // Hide the domain tag on single-domain pages (Lights, Covers, ...) —
        // it's redundant. Keep it on All Entities and multi-domain pages
        // (Buttons, Helpers) so users can tell entities apart.
        var showDomainTag = _domains is null || _domains.Count > 1;
        var tags = new List<Tag>(2);

        if (entity.Domain is "light" or "switch" or "fan" or "input_boolean" or "automation" or "media_player" or "binary_sensor" or "cover")
        {
            tags.Add(entity.IsOn
                ? new Tag("ON")
                {
                    Background = ColorHelpers.FromArgb(255, 76, 161, 222),
                    Foreground = ColorHelpers.FromRgb(255, 255, 255),
                }
                : new Tag("OFF")
                {
                    Background = ColorHelpers.FromRgb(120, 120, 120),
                    Foreground = ColorHelpers.FromRgb(255, 255, 255),
                });
        }

        if (showDomainTag)
        {
            tags.Add(new Tag(entity.Domain) { ToolTip = "Domain" });
        }

        return tags.ToArray();
    }
}
