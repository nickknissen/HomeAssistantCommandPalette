using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Data-first registration surface for domain icon dispatch. Three
/// shapes cover the distribution: <see cref="Stateless"/> (one constant
/// + optional unavailable), <see cref="OnOff"/> (three-state palette
/// driven by <see cref="HaEntity.IsOn"/>), and <see cref="Rich"/> for
/// anything else.
/// </summary>
internal sealed class IconRegistry
{
    private readonly Dictionary<string, IDomainIconRule> _rules =
        new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, IDomainIconRule> Rules => _rules;

    public IconRegistry Stateless(string domain, IconInfo icon, IconInfo? unavailable = null)
    {
        _rules[domain] = new StatelessRule(icon, unavailable ?? icon);
        return this;
    }

    public IconRegistry OnOff(string domain, IconInfo on, IconInfo off, IconInfo unavailable)
    {
        _rules[domain] = new OnOffRule(on, off, unavailable);
        return this;
    }

    public IconRegistry Rich(string domain, IDomainIconRule rule)
    {
        _rules[domain] = rule;
        return this;
    }

    private sealed class StatelessRule(IconInfo icon, IconInfo unavailable) : IDomainIconRule
    {
        public IconInfo Pick(HaEntity entity)
            => string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase)
                ? unavailable
                : icon;
    }

    private sealed class OnOffRule(IconInfo on, IconInfo off, IconInfo unavailable) : IDomainIconRule
    {
        public IconInfo Pick(HaEntity entity)
        {
            if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
                return unavailable;
            return entity.IsOn ? on : off;
        }
    }
}
