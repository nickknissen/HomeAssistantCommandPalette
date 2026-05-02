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
/// domain map.
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
            ["climate"]       = new ClimateBehavior(),
            ["media_player"]  = new MediaPlayerBehavior(),
            ["camera"]        = new CameraBehavior(),
            ["weather"]       = new WeatherBehavior(),
            ["sensor"]        = new SensorBehavior(),
            ["binary_sensor"] = new BinarySensorBehavior(),
        };

    /// <summary>
    /// Entity-id specific overrides. Wins over the domain map. Used for
    /// well-known singletons whose icon / behavior diverges from their
    /// domain's defaults — currently just <c>sun.sun</c>.
    /// </summary>
    private static readonly Dictionary<string, DomainBehavior> EntityIdMap =
        new(StringComparer.Ordinal)
        {
            ["sun.sun"] = new SunBehavior(),
        };

    /// <summary>
    /// Returns the registered behavior for <paramref name="entityId"/>
    /// or <paramref name="domain"/>, or <see cref="Default"/> when no
    /// entry exists. Entity-id matches take precedence.
    /// </summary>
    public static DomainBehavior For(string domain, string entityId)
    {
        if (EntityIdMap.TryGetValue(entityId, out var b)) return b;
        return Map.TryGetValue(domain, out b) ? b : Default;
    }

    /// <summary>
    /// Tries to resolve a registered (non-default) behavior for the
    /// given entity. Used by the page to route through
    /// <see cref="DomainBehavior"/> only for migrated domains; misses
    /// flow through the legacy dispatch sites.
    /// </summary>
    public static bool TryGet(string domain, string entityId, [NotNullWhen(true)] out DomainBehavior? behavior)
    {
        if (EntityIdMap.TryGetValue(entityId, out behavior)) return true;
        return Map.TryGetValue(domain, out behavior);
    }

    private sealed class DefaultBehavior : DomainBehavior
    {
        public override string Domain => string.Empty;
    }
}
