using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Static domain → behavior table. Lookup misses fall back to
/// <see cref="Default"/>, whose virtuals match the page's pre-refactor
/// fallback (Open dashboard primary, no extra rows / context items).
/// Entity-id overrides (e.g. <c>sun.sun</c>) take precedence over the
/// domain map and land alongside the first override in a later PR.
/// </summary>
public static class DomainRegistry
{
    public static readonly DomainBehavior Default = new DefaultBehavior();

    private static readonly Dictionary<string, DomainBehavior> Map =
        new(StringComparer.Ordinal)
        {
            ["switch"]        = Domains.Toggle("switch"),
            ["input_boolean"] = Domains.Toggle("input_boolean"),
            ["group"]         = Domains.Toggle("group"),
            ["scene"]         = Domains.Activate("scene", "turn_on", "Activate"),
            ["script"]        = Domains.Activate("script", "turn_on", "Run"),
            ["button"]        = Domains.Activate("button", "press", "Press"),
            ["input_button"]  = Domains.Activate("input_button", "press", "Press"),
            ["counter"]       = Domains.Increment("counter"),
            ["automation"]    = new AutomationBehavior(),
            ["vacuum"]        = new VacuumBehavior(),
            ["timer"]         = new TimerBehavior(),
            ["update"]        = new UpdateBehavior(),
            ["person"]        = new PersonBehavior(),
            ["input_select"]  = new InputSelectBehavior(),
            ["input_number"]  = new InputNumberBehavior(),
            ["cover"]         = new CoverBehavior(),
            ["fan"]           = new FanBehavior(),
            ["light"]         = new LightBehavior(),
        };

    /// <summary>
    /// Returns the registered behavior for <paramref name="domain"/>, or
    /// <see cref="Default"/> when no entry exists.
    /// </summary>
    public static DomainBehavior For(string domain)
        => Map.TryGetValue(domain, out var b) ? b : Default;

    /// <summary>
    /// Tries to get a registered (non-default) behavior. Used by the page
    /// during incremental migration: only domains present in the map go
    /// through <see cref="DomainBehavior"/>; everything else still flows
    /// through the legacy dispatch sites until those branches are
    /// migrated.
    /// </summary>
    public static bool TryGet(string domain, [NotNullWhen(true)] out DomainBehavior? behavior)
        => Map.TryGetValue(domain, out behavior);

    private sealed class DefaultBehavior : DomainBehavior
    {
        public override string Domain => string.Empty;
    }
}
