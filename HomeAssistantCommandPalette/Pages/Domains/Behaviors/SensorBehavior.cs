using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class SensorBehavior : DomainBehavior
{
    public override string Domain => "sensor";

    public override IconInfo BuildIcon(in DomainCtx ctx)
        => Icons.ForSensorDeviceClass("sensor", ctx.Entity.State, ctx.Entity.Attributes);

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("device_class", out var dc) && dc is string dcs && !string.IsNullOrEmpty(dcs))
            rows.Add(DomainHelpers.Row("Device class", dcs));
        if (entity.Attributes.TryGetValue("state_class", out var sc) && sc is string scs && !string.IsNullOrEmpty(scs))
            rows.Add(DomainHelpers.Row("State class", scs));
    }
}
