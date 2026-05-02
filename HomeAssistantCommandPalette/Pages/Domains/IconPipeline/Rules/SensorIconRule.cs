using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;

internal sealed class SensorIconRule : IDomainIconRule
{
    public IconInfo Pick(HaEntity entity)
        => Icons.ForSensorDeviceClass("sensor", entity.State, entity.Attributes);
}
