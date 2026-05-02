using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class CoverIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.CoverUnavailable;
        return entity.State.ToLowerInvariant() switch
        {
            "opening" => Icons.CoverOpening,
            "closing" => Icons.CoverClosing,
            "closed" => Icons.CoverClosed,
            // open + unknown → open
            _ => Icons.CoverOpen,
        };
    }
}
