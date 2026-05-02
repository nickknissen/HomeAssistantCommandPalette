using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class WeatherIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        var unavailable = string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase);
        return Icons.WeatherForCondition(entity.State, unavailable);
    }
}
