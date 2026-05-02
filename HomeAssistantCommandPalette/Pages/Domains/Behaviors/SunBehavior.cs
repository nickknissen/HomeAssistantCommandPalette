using System;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

/// <summary>
/// Entity-id override for <c>sun.sun</c>. Renders a sun-or-moon icon
/// driven off the published <c>above_horizon</c> / <c>below_horizon</c>
/// state. Registered against the entity-id seam in
/// <see cref="DomainRegistry"/>, not the domain map.
/// </summary>
public sealed class SunBehavior : DomainBehavior
{
    public override string Domain => "sun";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        if (string.Equals(ctx.Entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.SunUnavailable;
        return string.Equals(ctx.Entity.State, "below_horizon", StringComparison.OrdinalIgnoreCase)
            ? Icons.SunNight
            : Icons.SunDay;
    }
}
