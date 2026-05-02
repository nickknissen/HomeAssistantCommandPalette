using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class PersonIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.PersonUnavailable;
        // state is the zone the person is in. "home" → yellow; any
        // other zone (including "not_home") → blue.
        return string.Equals(entity.State, "home", StringComparison.OrdinalIgnoreCase)
            ? Icons.PersonOn
            : Icons.PersonOff;
    }
}
