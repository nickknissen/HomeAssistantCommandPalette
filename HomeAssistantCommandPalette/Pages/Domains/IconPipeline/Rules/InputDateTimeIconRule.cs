using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class InputDateTimeIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
    {
        // Pick calendar / clock / both based on the entity's published
        // has_date / has_time flags.
        var hasDate = entity.Attributes.TryGetValue("has_date", out var hd) && hd is bool hdb && hdb;
        var hasTime = entity.Attributes.TryGetValue("has_time", out var ht) && ht is bool htb && htb;
        if (hasDate && !hasTime) return Icons.InputDate;
        if (hasTime && !hasDate) return Icons.InputTime;
        return Icons.InputDateTime;
    }
}
