using System;
using System.Collections.Generic;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Static domain → behavior table. Lookup misses fall back to
/// <see cref="Default"/>, whose virtuals match the page's pre-refactor
/// fallback (Open dashboard primary, no extra rows / context items).
/// Entity-id overrides (e.g. <c>sun.sun</c>) take precedence over the
/// domain map and land in a later PR alongside the first override.
/// </summary>
internal static class DomainRegistry
{
    public static readonly DomainBehavior Default = new DefaultBehavior();

    private static readonly Dictionary<string, DomainBehavior> Map =
        new(StringComparer.Ordinal);

    public static DomainBehavior For(string domain)
        => Map.TryGetValue(domain, out var b) ? b : Default;

    private sealed class DefaultBehavior : DomainBehavior
    {
        public override string Domain => string.Empty;
    }
}
