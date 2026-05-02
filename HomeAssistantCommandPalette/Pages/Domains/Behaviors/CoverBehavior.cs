using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class CoverBehavior : DomainBehavior
{
    // HA CoverEntityFeature bits.
    //   1 OPEN, 2 CLOSE, 4 SET_POSITION, 8 STOP, 16 OPEN_TILT,
    //   32 CLOSE_TILT, 64 STOP_TILT, 128 SET_TILT_POSITION.
    private const long SetPosition = 4;
    private const long SetTiltPosition = 128;

    public override string Domain => "cover";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        if (string.Equals(ctx.Entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.CoverUnavailable;
        return ctx.Entity.State.ToLowerInvariant() switch
        {
            "opening" => Icons.CoverOpening,
            "closing" => Icons.CoverClosing,
            "closed" => Icons.CoverClosed,
            // open + unknown → open
            _ => Icons.CoverOpen,
        };
    }

    public override ICommand BuildPrimary(in DomainCtx ctx)
        => new CallServiceCommand(
            ctx.Client, "cover", "toggle", ctx.Entity.EntityId,
            $"Toggle {ctx.Entity.FriendlyName}",
            icon: Icons.Toggle, onSuccess: ctx.OnSuccess);

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;

        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "cover", "open_cover", entity.EntityId,
            $"Open {name}", icon: Icons.Open, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "cover", "close_cover", entity.EntityId,
            $"Close {name}", icon: Icons.Close, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "cover", "stop_cover", entity.EntityId,
            $"Stop {name}", icon: Icons.Stop, onSuccess: ctx.OnSuccess)));

        // Optimistic when supported_features is missing — same fallback
        // the legacy dispatcher used.
        var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
        bool Has(long bit) => sf < 0 || (sf & bit) == bit;

        // `in` parameters can't be captured by lambdas — bind locally.
        var client = ctx.Client;
        var onSuccess = ctx.OnSuccess;
        var entityId = entity.EntityId;

        if (Has(SetPosition))
        {
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set position…",
                Icon = Icons.Stop,
                MoreCommands = new IContextItem[]
                {
                    PositionPreset(client, entityId, onSuccess, 0),
                    PositionPreset(client, entityId, onSuccess, 25),
                    PositionPreset(client, entityId, onSuccess, 50),
                    PositionPreset(client, entityId, onSuccess, 75),
                    PositionPreset(client, entityId, onSuccess, 100),
                },
            });
        }

        if (Has(SetTiltPosition))
        {
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Set tilt…",
                Icon = Icons.Stop,
                MoreCommands = new IContextItem[]
                {
                    TiltPreset(client, entityId, onSuccess, 0),
                    TiltPreset(client, entityId, onSuccess, 25),
                    TiltPreset(client, entityId, onSuccess, 50),
                    TiltPreset(client, entityId, onSuccess, 75),
                    TiltPreset(client, entityId, onSuccess, 100),
                },
            });
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("current_position", out var pos) && pos is long p)
            rows.Add(DomainHelpers.Row("Position", $"{p}%"));
        if (entity.Attributes.TryGetValue("current_tilt_position", out var tilt) && tilt is long t)
            rows.Add(DomainHelpers.Row("Tilt", $"{t}%"));
        // `working` is true while the cover is actively moving — distinct
        // from `state == opening|closing` because some integrations report
        // it independently of the discrete state machine.
        if (entity.Attributes.TryGetValue("working", out var working) && working is bool wb)
            rows.Add(DomainHelpers.Row("Working", wb ? "yes" : "no"));
        if (entity.Attributes.TryGetValue("device_class", out var dc) && dc is string dcs && !string.IsNullOrEmpty(dcs))
            rows.Add(DomainHelpers.Row("Device class", dcs));
    }

    private static CommandContextItem PositionPreset(IHaClient client, string entityId, Action onSuccess, int position)
        => new(new CallServiceCommand(
            client, "cover", "set_cover_position", entityId,
            $"{position}%",
            icon: position == 0 ? Icons.Close : (position == 100 ? Icons.Open : Icons.Stop),
            extraData: new Dictionary<string, object?> { ["position"] = position },
            onSuccess: onSuccess));

    private static CommandContextItem TiltPreset(IHaClient client, string entityId, Action onSuccess, int position)
        => new(new CallServiceCommand(
            client, "cover", "set_cover_tilt_position", entityId,
            $"{position}%",
            icon: position == 0 ? Icons.Close : (position == 100 ? Icons.Open : Icons.Stop),
            extraData: new Dictionary<string, object?> { ["tilt_position"] = position },
            onSuccess: onSuccess));
}
