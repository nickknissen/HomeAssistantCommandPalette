using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class MediaPlayerBehavior : DomainBehavior
{
    // HA MediaPlayerEntityFeature bits.
    private const long Pause = 1;
    private const long VolumeSet = 4;
    private const long SelectSource = 2048;
    private const long ShuffleSet = 32768;
    private const long SelectSoundMode = 65536;
    private const long RepeatSet = 262144;

    private static readonly string[] RepeatOptions = ["off", "one", "all"];

    public override string Domain => "media_player";

    public override ICommand BuildPrimary(in DomainCtx ctx)
        => new CallServiceCommand(
            ctx.Client, "media_player", "toggle", ctx.Entity.EntityId,
            $"Toggle {ctx.Entity.FriendlyName}",
            icon: Icons.Toggle, onSuccess: ctx.OnSuccess);

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;
        var client = ctx.Client;
        var onSuccess = ctx.OnSuccess;
        var entityId = entity.EntityId;

        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "turn_on", entityId,
            $"Turn on {name}", icon: Icons.TurnOn, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "turn_off", entityId,
            $"Turn off {name}", icon: Icons.TurnOff, onSuccess: onSuccess)));

        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "media_play_pause", entityId,
            $"Play / Pause {name}", icon: Icons.PlayPause, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "media_play", entityId,
            $"Play {name}", icon: Icons.Play, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "media_pause", entityId,
            $"Pause {name}", icon: Icons.Pause, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "media_stop", entityId,
            $"Stop {name}", icon: Icons.Stop, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "media_next_track", entityId,
            $"Next track on {name}", icon: Icons.Next, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "media_previous_track", entityId,
            $"Previous track on {name}", icon: Icons.Previous, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "volume_up", entityId,
            $"Volume up on {name}", icon: Icons.VolumeUp, onSuccess: onSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            client, "media_player", "volume_down", entityId,
            $"Volume down on {name}", icon: Icons.VolumeDown, onSuccess: onSuccess)));

        // Mute toggle — flip is_volume_muted. Skip when the attribute is
        // missing (some players publish it only when supported).
        if (entity.Attributes.TryGetValue("is_volume_muted", out var muted) && muted is bool isMuted)
        {
            items.Add(new CommandContextItem(new CallServiceCommand(
                client, "media_player", "volume_mute", entityId,
                isMuted ? $"Unmute {name}" : $"Mute {name}",
                icon: Icons.VolumeMute,
                extraData: new Dictionary<string, object?> { ["is_volume_muted"] = !isMuted },
                onSuccess: onSuccess)));
        }

        // supported_features matrix. -1 means the attribute was missing —
        // optimistically allow every action in that case.
        var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
        bool Has(long bit) => sf < 0 || (sf & bit) == bit;

        if (Has(VolumeSet))
        {
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set volume…",
                Icon = Icons.Volume,
                MoreCommands = new IContextItem[]
                {
                    VolumePreset(client, entityId, onSuccess, 25),
                    VolumePreset(client, entityId, onSuccess, 50),
                    VolumePreset(client, entityId, onSuccess, 75),
                    VolumePreset(client, entityId, onSuccess, 100),
                },
            });
        }

        // Shuffle toggle — flip the current `shuffle` bool. Only emitted
        // when both the bit and the attribute are present.
        if (Has(ShuffleSet) && entity.Attributes.TryGetValue("shuffle", out var sh) && sh is bool isShuffling)
        {
            items.Add(new CommandContextItem(new CallServiceCommand(
                client, "media_player", "shuffle_set", entityId,
                isShuffling ? $"Disable shuffle on {name}" : $"Enable shuffle on {name}",
                icon: Icons.PlayPause,
                extraData: new Dictionary<string, object?> { ["shuffle"] = !isShuffling },
                onSuccess: onSuccess)));
        }

        if (Has(RepeatSet))
        {
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set repeat…",
                Icon = Icons.PlayPause,
                MoreCommands = BuildEnumSubmenu(client, entityId, onSuccess,
                    "repeat_set", "repeat", RepeatOptions),
            });
        }

        // Source submenu — populated from the entity's `source_list`.
        if (Has(SelectSource) && entity.Attributes.TryGetValue("source_list", out var sl) && sl is List<object?> sources)
        {
            var sub = BuildAttrListSubmenu(client, entityId, onSuccess,
                "select_source", "source", sources);
            if (sub.Length > 0)
            {
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Select source…",
                    Icon = Icons.Volume,
                    MoreCommands = sub,
                });
            }
        }

        // Sound mode submenu — same shape, gated on SELECT_SOUND_MODE.
        if (Has(SelectSoundMode) && entity.Attributes.TryGetValue("sound_mode_list", out var sml) && sml is List<object?> soundModes)
        {
            var sub = BuildAttrListSubmenu(client, entityId, onSuccess,
                "select_sound_mode", "sound_mode", soundModes);
            if (sub.Length > 0)
            {
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Select sound mode…",
                    Icon = Icons.Volume,
                    MoreCommands = sub,
                });
            }
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;

        if (entity.Attributes.TryGetValue("media_title", out var title) && title is string ts && !string.IsNullOrEmpty(ts))
            rows.Add(DomainHelpers.Row("Track", ts));
        if (entity.Attributes.TryGetValue("media_artist", out var artist) && artist is string ars && !string.IsNullOrEmpty(ars))
            rows.Add(DomainHelpers.Row("Artist", ars));
        if (entity.Attributes.TryGetValue("media_album_name", out var album) && album is string als && !string.IsNullOrEmpty(als))
            rows.Add(DomainHelpers.Row("Album", als));

        // Position / Duration as MM:SS — when state="playing", advance the
        // reported position by (now - media_position_updated_at) so the
        // row doesn't lag the actual playback by the full list-cache window.
        var posSeconds = TryGetSeconds(entity.Attributes, "media_position");
        var durSeconds = TryGetSeconds(entity.Attributes, "media_duration");
        if (posSeconds is double pos && durSeconds is double dur && dur > 0)
        {
            if (string.Equals(entity.State, "playing", StringComparison.OrdinalIgnoreCase)
                && entity.Attributes.TryGetValue("media_position_updated_at", out var puat)
                && puat is string puatS
                && DateTimeOffset.TryParse(puatS, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var updated))
            {
                var elapsed = (DateTimeOffset.UtcNow - updated).TotalSeconds;
                if (elapsed > 0) pos += elapsed;
            }
            if (pos < 0) pos = 0;
            if (pos > dur) pos = dur;
            rows.Add(DomainHelpers.Row("Position", $"{FormatTimecode(pos)} / {FormatTimecode(dur)}"));
        }

        if (entity.Attributes.TryGetValue("source", out var source) && source is string src && !string.IsNullOrEmpty(src))
            rows.Add(DomainHelpers.Row("Source", src));
        if (entity.Attributes.TryGetValue("sound_mode", out var sm) && sm is string sms && !string.IsNullOrEmpty(sms))
            rows.Add(DomainHelpers.Row("Sound mode", sms));
        if (entity.Attributes.TryGetValue("shuffle", out var shuf) && shuf is bool sb)
            rows.Add(DomainHelpers.Row("Shuffle", sb ? "on" : "off"));
        if (entity.Attributes.TryGetValue("repeat", out var rep) && rep is string reps && !string.IsNullOrEmpty(reps))
            rows.Add(DomainHelpers.Row("Repeat", reps));
        // volume_level is 0.0..1.0
        if (entity.Attributes.TryGetValue("volume_level", out var vol) && vol is double v)
            rows.Add(DomainHelpers.Row("Volume", $"{(int)Math.Round(v * 100)}%"));
        else if (entity.Attributes.TryGetValue("volume_level", out var vol2) && vol2 is long lv)
            rows.Add(DomainHelpers.Row("Volume", $"{lv * 100}%"));
        if (entity.Attributes.TryGetValue("is_volume_muted", out var muted) && muted is bool m)
            rows.Add(DomainHelpers.Row("Muted", m ? "yes" : "no"));
        if (entity.Attributes.TryGetValue("app_name", out var app) && app is string apps && !string.IsNullOrEmpty(apps))
            rows.Add(DomainHelpers.Row("App", apps));
    }

    private static CommandContextItem VolumePreset(IHaClient client, string entityId, Action onSuccess, int pct)
        => new(new CallServiceCommand(
            client, "media_player", "volume_set", entityId,
            $"{pct}%", icon: Icons.Volume,
            // volume_level wants 0.0..1.0 — convert from percentage.
            extraData: new Dictionary<string, object?> { ["volume_level"] = pct / 100.0 },
            onSuccess: onSuccess));

    private static IContextItem[] BuildEnumSubmenu(
        IHaClient client, string entityId, Action onSuccess,
        string service, string argName, string[] options)
        => options
            .Select(o => (IContextItem)new CommandContextItem(new CallServiceCommand(
                client, "media_player", service, entityId,
                o, extraData: new Dictionary<string, object?> { [argName] = o },
                onSuccess: onSuccess)))
            .ToArray();

    private static IContextItem[] BuildAttrListSubmenu(
        IHaClient client, string entityId, Action onSuccess,
        string service, string argName, List<object?> options)
        => options
            .OfType<string>()
            .Select(o => (IContextItem)new CommandContextItem(new CallServiceCommand(
                client, "media_player", service, entityId,
                o, extraData: new Dictionary<string, object?> { [argName] = o },
                onSuccess: onSuccess)))
            .ToArray();

    private static double? TryGetSeconds(IReadOnlyDictionary<string, object?> attrs, string key) =>
        attrs.TryGetValue(key, out var v)
            ? v switch { double d => d, long l => (double)l, _ => (double?)null }
            : null;

    /// <summary>
    /// Renders a duration in seconds as MM:SS, or H:MM:SS when ≥ 1 hour.
    /// </summary>
    private static string FormatTimecode(double totalSeconds)
    {
        var s = (long)Math.Round(totalSeconds);
        var hours = s / 3600;
        var minutes = (s % 3600) / 60;
        var seconds = s % 60;
        return hours > 0
            ? $"{hours}:{minutes:D2}:{seconds:D2}"
            : $"{minutes}:{seconds:D2}";
    }
}
