using System;
using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class VacuumBehavior : DomainBehavior
{
    // HA VacuumEntityFeature bits.
    private const long Pause = 4;
    private const long Stop = 8;
    private const long ReturnHome = 16;
    private const long FanSpeed = 32;
    private const long Locate = 512;
    private const long CleanSpot = 1024;
    private const long Start = 8192;

    public override string Domain => "vacuum";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        if (string.Equals(ctx.Entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.VacuumUnavailable;
        return string.Equals(ctx.Entity.State, "cleaning", StringComparison.OrdinalIgnoreCase)
            ? Icons.VacuumCleaning
            : Icons.VacuumIdle;
    }

    public override ICommand BuildPrimary(in DomainCtx ctx)
    {
        var name = ctx.Entity.FriendlyName;
        return string.Equals(ctx.Entity.State, "cleaning", StringComparison.OrdinalIgnoreCase)
            ? new CallServiceCommand(ctx.Client, "vacuum", "pause", ctx.Entity.EntityId, $"Pause {name}", icon: Icons.Pause, onSuccess: ctx.OnSuccess)
            : new CallServiceCommand(ctx.Client, "vacuum", "start", ctx.Entity.EntityId, $"Start {name}", icon: Icons.Play, onSuccess: ctx.OnSuccess);
    }

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;
        // Only surface actions the device declares it can do. If the
        // attribute is missing, optimistically allow every action.
        var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
        bool Has(long bit) => sf < 0 || (sf & bit) == bit;

        if (Has(Start))
            items.Add(new CommandContextItem(new CallServiceCommand(ctx.Client, "vacuum", "start", entity.EntityId, $"Start {name}", icon: Icons.Play, onSuccess: ctx.OnSuccess)));
        if (Has(Pause))
            items.Add(new CommandContextItem(new CallServiceCommand(ctx.Client, "vacuum", "pause", entity.EntityId, $"Pause {name}", icon: Icons.Pause, onSuccess: ctx.OnSuccess)));
        if (Has(Stop))
            items.Add(new CommandContextItem(new CallServiceCommand(ctx.Client, "vacuum", "stop", entity.EntityId, $"Stop {name}", icon: Icons.Stop, onSuccess: ctx.OnSuccess)));
        if (Has(ReturnHome))
            items.Add(new CommandContextItem(new CallServiceCommand(ctx.Client, "vacuum", "return_to_base", entity.EntityId, $"Send {name} home", icon: Icons.Home, onSuccess: ctx.OnSuccess)));
        if (Has(Locate))
            items.Add(new CommandContextItem(new CallServiceCommand(ctx.Client, "vacuum", "locate", entity.EntityId, $"Locate {name}", onSuccess: ctx.OnSuccess)));
        if (Has(CleanSpot))
            items.Add(new CommandContextItem(new CallServiceCommand(ctx.Client, "vacuum", "clean_spot", entity.EntityId, $"Clean spot with {name}", onSuccess: ctx.OnSuccess)));

        // Fan speed submenu when the vacuum reports fan_speed_list and
        // supports FAN_SPEED.
        if (Has(FanSpeed) && entity.Attributes.TryGetValue("fan_speed_list", out var fsl) && fsl is List<object?> speeds)
        {
            // `in` parameters can't be captured by lambdas.
            var client = ctx.Client;
            var onSuccess = ctx.OnSuccess;
            var entityId = entity.EntityId;
            var speedItems = speeds
                .OfType<string>()
                .Select(s => (IContextItem)new CommandContextItem(new CallServiceCommand(
                    client, "vacuum", "set_fan_speed", entityId,
                    s, extraData: new Dictionary<string, object?> { ["fan_speed"] = s },
                    onSuccess: onSuccess)))
                .ToArray();
            if (speedItems.Length > 0)
            {
                items.Add(new CommandContextItem(new NoOpCommand())
                {
                    Title = "Set fan speed…",
                    Icon = Icons.Fan,
                    MoreCommands = speedItems,
                });
            }
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("status", out var st) && st is string sts && !string.IsNullOrEmpty(sts))
            rows.Add(DomainHelpers.Row("Status", sts));
        if (entity.Attributes.TryGetValue("battery_level", out var bat))
        {
            var v = bat switch { long l => $"{l}%", double d => $"{(int)d}%", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Battery", v));
        }
        if (entity.Attributes.TryGetValue("fan_speed", out var fs) && fs is string fss && !string.IsNullOrEmpty(fss))
            rows.Add(DomainHelpers.Row("Fan speed", fss));
        if (entity.Attributes.TryGetValue("cleaned_area", out var area))
        {
            var v = area switch { long l => $"{l} m²", double d => $"{d} m²", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Cleaned", v));
        }
        // `cleaning_time` is integration-specific: most expose minutes as
        // an int, a few expose a pre-formatted "HH:MM:SS" string.
        if (entity.Attributes.TryGetValue("cleaning_time", out var ct))
        {
            var v = ct switch
            {
                string s when !string.IsNullOrEmpty(s) => s,
                long l => DomainHelpers.FormatMinutes(l),
                double d => DomainHelpers.FormatMinutes((long)d),
                _ => null,
            };
            if (v is not null) rows.Add(DomainHelpers.Row("Cleaning time", v));
        }
        if (entity.Attributes.TryGetValue("error", out var err) && err is string errs && !string.IsNullOrEmpty(errs))
            rows.Add(DomainHelpers.Row("Last error", errs));
    }
}
