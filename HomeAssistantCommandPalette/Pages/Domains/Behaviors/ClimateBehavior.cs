using System;
using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class ClimateBehavior : DomainBehavior
{
    public override string Domain => "climate";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        var entity = ctx.Entity;
        if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.ClimateUnavailable;
        return entity.State.ToLowerInvariant() switch
        {
            "off" => Icons.ClimateOff,
            "auto" or "heat_cool" => Icons.ClimateAuto,
            // heat / cool / dry / fan_only
            _ => Icons.ClimateActive,
        };
    }

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;
        var client = ctx.Client;
        var onSuccess = ctx.OnSuccess;
        var entityId = entity.EntityId;

        // Target temperature ± buttons (clamped to min/max if HA reports them).
        var current = GetCurrentTargetTemp(entity);
        var step = GetTempStep(entity);
        if (!double.IsNaN(current))
        {
            var increased = Math.Round(current + step, 1);
            var decreased = Math.Round(current - step, 1);
            items.Add(new CommandContextItem(new CallServiceCommand(
                client, "climate", "set_temperature", entityId,
                $"Increase to {increased}°", icon: Icons.Thermometer,
                extraData: new Dictionary<string, object?> { ["temperature"] = increased },
                onSuccess: onSuccess)));
            items.Add(new CommandContextItem(new CallServiceCommand(
                client, "climate", "set_temperature", entityId,
                $"Decrease to {decreased}°", icon: Icons.Thermometer,
                extraData: new Dictionary<string, object?> { ["temperature"] = decreased },
                onSuccess: onSuccess)));
        }

        // Temperature presets — handy when the climate isn't currently
        // running (no current target to ±-step from).
        items.Add(new CommandContextItem(new NoOpCommand())
        {
            Title = "Set temperature…",
            Icon = Icons.Thermometer,
            MoreCommands = new IContextItem[]
            {
                TemperaturePreset(client, entityId, onSuccess, 18),
                TemperaturePreset(client, entityId, onSuccess, 20),
                TemperaturePreset(client, entityId, onSuccess, 21),
                TemperaturePreset(client, entityId, onSuccess, 22),
                TemperaturePreset(client, entityId, onSuccess, 24),
            },
        });

        AddModeSubmenu(items, entity, client, entityId, onSuccess,
            attribute: "hvac_modes", service: "set_hvac_mode", argName: "hvac_mode",
            title: "Set HVAC mode…", icon: Icons.Thermostat);
        AddModeSubmenu(items, entity, client, entityId, onSuccess,
            attribute: "fan_modes", service: "set_fan_mode", argName: "fan_mode",
            title: "Set fan mode…", icon: Icons.Fan);
        AddModeSubmenu(items, entity, client, entityId, onSuccess,
            attribute: "swing_modes", service: "set_swing_mode", argName: "swing_mode",
            title: "Set swing mode…", icon: Icons.Fan);
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("current_temperature", out var ct))
        {
            var v = ct switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Current temp", v));
        }
        if (entity.Attributes.TryGetValue("temperature", out var tt))
        {
            var v = tt switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Target temp", v));
        }
        // Dual setpoint (heat_cool / auto): the integration reports
        // target_temp_low / target_temp_high instead of `temperature`.
        if (entity.Attributes.TryGetValue("target_temp_low", out var tlo))
        {
            var v = tlo switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Target low", v));
        }
        if (entity.Attributes.TryGetValue("target_temp_high", out var thi))
        {
            var v = thi switch { double d => $"{d}°", long l => $"{l}°", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Target high", v));
        }
        // Min / max range — only if both are reported.
        var min = entity.Attributes.TryGetValue("min_temp", out var mn) ? mn : null;
        var max = entity.Attributes.TryGetValue("max_temp", out var mx) ? mx : null;
        var minStr = FormatTemp(min);
        var maxStr = FormatTemp(max);
        if (minStr is not null && maxStr is not null)
        {
            rows.Add(DomainHelpers.Row("Range", $"{minStr} – {maxStr}"));
        }
        if (entity.Attributes.TryGetValue("current_humidity", out var ch))
        {
            var v = ch switch { double d => $"{(int)d}%", long l => $"{l}%", _ => null };
            if (v is not null) rows.Add(DomainHelpers.Row("Humidity", v));
        }
        if (entity.Attributes.TryGetValue("hvac_action", out var ha) && ha is string has && !string.IsNullOrEmpty(has))
            rows.Add(DomainHelpers.Row("Action", has));
        if (entity.Attributes.TryGetValue("fan_mode", out var fm) && fm is string fms && !string.IsNullOrEmpty(fms))
            rows.Add(DomainHelpers.Row("Fan", fms));
        if (entity.Attributes.TryGetValue("swing_mode", out var swm) && swm is string swms && !string.IsNullOrEmpty(swms))
            rows.Add(DomainHelpers.Row("Swing", swms));
        if (entity.Attributes.TryGetValue("preset_mode", out var pm) && pm is string pms && !string.IsNullOrEmpty(pms))
            rows.Add(DomainHelpers.Row("Preset", pms));
    }

    private static double GetCurrentTargetTemp(HaEntity entity)
    {
        if (!entity.Attributes.TryGetValue("temperature", out var t)) return double.NaN;
        return t switch
        {
            double d => d,
            long l => l,
            _ => double.NaN,
        };
    }

    private static double GetTempStep(HaEntity entity)
    {
        if (!entity.Attributes.TryGetValue("target_temp_step", out var s)) return 0.5;
        return s switch
        {
            double d => d,
            long l => l,
            _ => 0.5,
        };
    }

    private static string? FormatTemp(object? o) => o switch
    {
        double d => $"{d}°",
        long l => $"{l}°",
        _ => null,
    };

    private static CommandContextItem TemperaturePreset(IHaClient client, string entityId, Action onSuccess, double temp)
        => new(new CallServiceCommand(
            client, "climate", "set_temperature", entityId,
            $"{temp}°", icon: Icons.Thermometer,
            extraData: new Dictionary<string, object?> { ["temperature"] = temp },
            onSuccess: onSuccess));

    private static void AddModeSubmenu(
        List<IContextItem> items, HaEntity entity,
        IHaClient client, string entityId, Action onSuccess,
        string attribute, string service, string argName, string title, IconInfo icon)
    {
        if (!entity.Attributes.TryGetValue(attribute, out var m) || m is not List<object?> modes) return;

        var modeItems = modes
            .OfType<string>()
            .Select(mode => (IContextItem)new CommandContextItem(new CallServiceCommand(
                client, "climate", service, entityId,
                mode, extraData: new Dictionary<string, object?> { [argName] = mode },
                onSuccess: onSuccess)))
            .ToArray();
        if (modeItems.Length == 0) return;

        items.Add(new CommandContextItem(new NoOpCommand())
        {
            Title = title,
            Icon = icon,
            MoreCommands = modeItems,
        });
    }
}
