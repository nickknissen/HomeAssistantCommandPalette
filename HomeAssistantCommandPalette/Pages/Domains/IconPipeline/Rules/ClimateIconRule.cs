using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class ClimateIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
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
}
