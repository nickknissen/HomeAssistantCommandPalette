using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class FanBehavior : DomainBehavior
{
    // HA FanEntityFeature: bit 1 SET_SPEED. Higher bits (oscillation,
    // direction, presets) aren't gated on context items here — direction
    // and oscillation are read-only rows; preset_mode is read-only too.
    private const long SetSpeed = 1;

    public override string Domain => "fan";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        if (string.Equals(ctx.Entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.FanUnavailable;
        return ctx.Entity.IsOn ? Icons.FanOn : Icons.FanOff;
    }

    public override ICommand BuildPrimary(in DomainCtx ctx)
        => new CallServiceCommand(
            ctx.Client, "fan", "toggle", ctx.Entity.EntityId,
            $"Toggle {ctx.Entity.FriendlyName}",
            icon: Icons.Toggle, onSuccess: ctx.OnSuccess);

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;

        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "fan", "turn_on", entity.EntityId,
            $"Turn on {name}", icon: Icons.TurnOn, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "fan", "turn_off", entity.EntityId,
            $"Turn off {name}", icon: Icons.TurnOff, onSuccess: ctx.OnSuccess)));

        var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
        var supportsSpeed = sf < 0 || (sf & SetSpeed) == SetSpeed;
        if (!supportsSpeed) return;

        // Speed up / down — single step from current percentage. Skip
        // when the device doesn't publish percentage_step, or when the
        // resulting value would clamp out of range.
        var currentPct = entity.Attributes.TryGetValue("percentage", out var pv)
            ? pv switch { long l => (double)l, double d => d, _ => double.NaN }
            : double.NaN;
        var step = entity.Attributes.TryGetValue("percentage_step", out var sv)
            ? sv switch { long l => (double)l, double d => d, _ => double.NaN }
            : double.NaN;

        if (!double.IsNaN(currentPct) && !double.IsNaN(step) && step > 0)
        {
            var up = (int)Math.Round(currentPct + step);
            var down = (int)Math.Round(currentPct - step);
            if (up <= 100)
            {
                items.Add(new CommandContextItem(new CallServiceCommand(
                    ctx.Client, "fan", "turn_on", entity.EntityId,
                    $"Speed up to {up}%", icon: Icons.Fan,
                    extraData: new Dictionary<string, object?> { ["percentage"] = up },
                    onSuccess: ctx.OnSuccess)));
            }
            if (down >= 0)
            {
                items.Add(new CommandContextItem(new CallServiceCommand(
                    ctx.Client, "fan", "turn_on", entity.EntityId,
                    $"Speed down to {down}%", icon: Icons.Fan,
                    extraData: new Dictionary<string, object?> { ["percentage"] = down },
                    onSuccess: ctx.OnSuccess)));
            }
        }

        // Speed presets — mirrors lights' brightness shape. 0% is
        // intentionally omitted because Turn off already exists above.
        var client = ctx.Client;
        var onSuccess = ctx.OnSuccess;
        var entityId = entity.EntityId;
        items.Add(new CommandContextItem(new NoOpCommand())
        {
            Title = "Set speed…",
            Icon = Icons.Fan,
            MoreCommands = new IContextItem[]
            {
                SpeedPreset(client, entityId, onSuccess, 25),
                SpeedPreset(client, entityId, onSuccess, 50),
                SpeedPreset(client, entityId, onSuccess, 75),
                SpeedPreset(client, entityId, onSuccess, 100),
            },
        });
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("percentage", out var pct))
        {
            var v = pct switch { long l => $"{l}%", double d => $"{(int)d}%", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Speed", v));
        }
        if (entity.Attributes.TryGetValue("preset_mode", out var pm) && pm is string pms && !string.IsNullOrEmpty(pms))
            rows.Add(DomainHelpers.Row("Preset", pms));
        if (entity.Attributes.TryGetValue("oscillating", out var osc) && osc is bool b)
            rows.Add(DomainHelpers.Row("Oscillating", b ? "yes" : "no"));
        if (entity.Attributes.TryGetValue("direction", out var dir) && dir is string dirs && !string.IsNullOrEmpty(dirs))
            rows.Add(DomainHelpers.Row("Direction", dirs));
    }

    private static CommandContextItem SpeedPreset(IHaClient client, string entityId, Action onSuccess, int pct)
        => new(new CallServiceCommand(
            client, "fan",
            // turn_on with percentage starts the fan if it was off
            // (matches Raycast and avoids a no-op when state="off").
            "turn_on", entityId,
            $"{pct}%", icon: Icons.Fan,
            extraData: new Dictionary<string, object?> { ["percentage"] = pct },
            onSuccess: onSuccess));
}
