using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class TimerIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.TimerUnavailable;
        // Yellow when active; blue for idle / paused. Mirrors Raycast.
        return string.Equals(entity.State, "active", StringComparison.OrdinalIgnoreCase)
            ? Icons.TimerOn
            : Icons.TimerOff;
    }
}
