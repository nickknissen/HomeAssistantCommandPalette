using System;
using System.Collections.Generic;
using System.Globalization;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class AutomationBehavior : DomainBehavior
{
    public override string Domain => "automation";

    public override ICommand BuildPrimary(in DomainCtx ctx)
        => new CallServiceCommand(
            ctx.Client, "automation", "toggle", ctx.Entity.EntityId,
            $"Toggle {ctx.Entity.FriendlyName}", icon: Icons.Toggle, onSuccess: ctx.OnSuccess);

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var name = ctx.Entity.FriendlyName;
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "automation", "turn_on", ctx.Entity.EntityId,
            $"Turn on {name}", icon: Icons.TurnOn, onSuccess: ctx.OnSuccess)));
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "automation", "turn_off", ctx.Entity.EntityId,
            $"Turn off {name}", icon: Icons.TurnOff, onSuccess: ctx.OnSuccess)));
        // Manual trigger — fire the automation regardless of trigger
        // conditions. Distinct from turn_on, which only enables it.
        items.Add(new CommandContextItem(new CallServiceCommand(
            ctx.Client, "automation", "trigger", ctx.Entity.EntityId,
            $"Trigger {name}", icon: Icons.Trigger, onSuccess: ctx.OnSuccess)));
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("last_triggered", out var lt) && lt is string lts && !string.IsNullOrEmpty(lts))
        {
            // ISO timestamp from HA. Show as the original ISO — clearer
            // than a fragile relative-time format, and tooling-friendly.
            rows.Add(DomainHelpers.Row("Last triggered", lts));
        }
        if (entity.Attributes.TryGetValue("mode", out var mode) && mode is string ms && !string.IsNullOrEmpty(ms))
        {
            rows.Add(DomainHelpers.Row("Mode", ms));
        }
        if (entity.Attributes.TryGetValue("current", out var current))
        {
            // Number of currently-running instances (relevant for parallel
            // / queued mode). 0 normally; >0 means the automation is mid-run.
            var v = current switch
            {
                long l => l.ToString(CultureInfo.InvariantCulture),
                double d => ((int)d).ToString(CultureInfo.InvariantCulture),
                _ => null,
            };
            if (v is not null) rows.Add(DomainHelpers.Row("Running", v));
        }
        if (entity.Attributes.TryGetValue("id", out var id) && id is string ids && !string.IsNullOrEmpty(ids))
        {
            rows.Add(DomainHelpers.Row("Automation ID", ids));
        }
    }
}
