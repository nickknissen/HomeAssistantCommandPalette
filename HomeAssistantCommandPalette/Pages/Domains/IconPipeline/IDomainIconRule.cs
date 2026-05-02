using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Per-domain rule for the rich (~6 of 22) cases that don't fit the
/// <c>Stateless</c> or <c>OnOff</c> registry sugar — light groups,
/// climate HVAC modes, cover 5-state palette, sensor / binary_sensor
/// device_class, weather conditions, etc. Owns its own unavailable
/// branch.
/// </summary>
internal interface IDomainIconRule
{
    IconInfo Pick(HaEntity entity);
}
