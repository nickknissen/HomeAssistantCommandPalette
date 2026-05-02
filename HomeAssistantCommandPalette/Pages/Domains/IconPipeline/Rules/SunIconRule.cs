using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class SunIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.SunUnavailable;
        return string.Equals(entity.State, "below_horizon", StringComparison.OrdinalIgnoreCase)
            ? Icons.SunNight
            : Icons.SunDay;
    }
}
