using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class VacuumIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.VacuumUnavailable;
        return string.Equals(entity.State, "cleaning", StringComparison.OrdinalIgnoreCase)
            ? Icons.VacuumCleaning
            : Icons.VacuumIdle;
    }
}
