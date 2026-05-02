using System;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Default <see cref="IIconRules"/> implementation: dispatches by
/// <see cref="HaEntity.Domain"/> to the rule registered in the supplied
/// <see cref="IconRegistry"/>. Unregistered domains fall back to
/// <see cref="Icons.ForDomain(string, string)"/>.
/// </summary>
internal sealed class RegistryIconRules : IIconRules
{
    private readonly IconRegistry _registry;

    public RegistryIconRules(Action<IconRegistry> configure)
    {
        _registry = new IconRegistry();
        configure(_registry);
    }

    public IconInfo Resolve(HaEntity entity)
    {
        if (_registry.Rules.TryGetValue(entity.Domain, out var rule))
        {
            return rule.Pick(entity);
        }
        return Icons.ForDomain(entity.Domain, entity.State);
    }
}
