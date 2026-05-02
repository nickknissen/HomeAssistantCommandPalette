using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class InputDateTimeBehavior : DomainBehavior
{
    public override string Domain => "input_datetime";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        // Pick calendar / clock / both based on the entity's published
        // has_date / has_time flags.
        var hasDate = ctx.Entity.Attributes.TryGetValue("has_date", out var hd) && hd is bool hdb && hdb;
        var hasTime = ctx.Entity.Attributes.TryGetValue("has_time", out var ht) && ht is bool htb && htb;
        if (hasDate && !hasTime) return Icons.InputDate;
        if (hasTime && !hasDate) return Icons.InputTime;
        return Icons.InputDateTime;
    }
}
