using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class TimerBehavior : DomainBehavior
{
    public override string Domain => "timer";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        if (string.Equals(ctx.Entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.TimerUnavailable;
        // Yellow when active; blue for idle / paused. Mirrors Raycast.
        return string.Equals(ctx.Entity.State, "active", StringComparison.OrdinalIgnoreCase)
            ? Icons.TimerOn
            : Icons.TimerOff;
    }

    public override ICommand BuildPrimary(in DomainCtx ctx)
    {
        var name = ctx.Entity.FriendlyName;
        return string.Equals(ctx.Entity.State, "active", StringComparison.OrdinalIgnoreCase)
            ? new CallServiceCommand(ctx.Client, "timer", "pause", ctx.Entity.EntityId, $"Pause {name}", icon: Icons.Pause, onSuccess: ctx.OnSuccess)
            : new CallServiceCommand(ctx.Client, "timer", "start", ctx.Entity.EntityId, $"Start {name}", icon: Icons.Play, onSuccess: ctx.OnSuccess);
    }

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;

        // Mirror Raycast: only act on `editable` timers. HA exposes
        // start/pause/cancel for code-defined timers too, but those
        // round-trip via the YAML config — Raycast hides them.
        var editable = entity.Attributes.TryGetValue("editable", out var ed) && ed is bool eb && eb;
        if (!editable) return;

        var isActive = string.Equals(entity.State, "active", StringComparison.OrdinalIgnoreCase);
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "timer", "start", entity.EntityId,
            isActive ? $"Restart {name}" : $"Start {name}",
            icon: Icons.Play, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "timer", "pause", entity.EntityId,
            $"Pause {name}", icon: Icons.Pause, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "timer", "cancel", entity.EntityId,
            $"Cancel {name}", icon: Icons.Stop, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "timer", "finish", entity.EntityId,
            $"Finish {name}", onSuccess: ctx.OnSuccess)));
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("duration", out var d) && d is string ds && !string.IsNullOrEmpty(ds))
            rows.Add(DomainHelpers.Row("Duration", ds));
        if (entity.Attributes.TryGetValue("remaining", out var r) && r is string rs && !string.IsNullOrEmpty(rs))
            rows.Add(DomainHelpers.Row("Remaining", rs));
        if (entity.Attributes.TryGetValue("finishes_at", out var f) && f is string fs && !string.IsNullOrEmpty(fs))
            rows.Add(DomainHelpers.Row("Finishes at", fs));
    }
}
