using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Pure mapping from entity to baked icon. No I/O, no async — given the
/// same <see cref="HaEntity"/> snapshot, returns the same icon every
/// time. Property-testable in isolation: <c>new RegistryIconRules(...).
/// Resolve(entity)</c> is enough to assert the dispatch.
/// </summary>
internal interface IIconRules
{
    IconInfo Resolve(HaEntity entity);
}
