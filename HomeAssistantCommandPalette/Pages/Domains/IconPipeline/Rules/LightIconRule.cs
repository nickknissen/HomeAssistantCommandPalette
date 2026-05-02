using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class LightIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        var unavailable = string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase);
        // Detect a lights group via the standard HA `mdi:lightbulb-group`
        // entity icon override. Falls back to a single bulb otherwise.
        var isGroup = entity.Attributes.TryGetValue("icon", out var ic)
            && ic is string s
            && string.Equals(s, "mdi:lightbulb-group", StringComparison.OrdinalIgnoreCase);

        if (unavailable) return isGroup ? Icons.LightGroupUnavailable : Icons.LightUnavailable;
        if (entity.IsOn) return isGroup ? Icons.LightGroupOn : Icons.LightOn;
        return isGroup ? Icons.LightGroupOff : Icons.LightOff;
    }
}
