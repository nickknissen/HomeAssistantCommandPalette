using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class PersonBehavior : DomainBehavior
{
    public override string Domain => "person";

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        // Open in Google Maps — universal across platforms (Apple Maps
        // is macOS-only, so we don't ship it on Windows).
        if (DomainHelpers.TryGetDouble(entity.Attributes, "latitude", out var lat) &&
            DomainHelpers.TryGetDouble(entity.Attributes, "longitude", out var lon))
        {
            var url = $"https://www.google.com/maps/search/?api=1&query={lat.ToString(CultureInfo.InvariantCulture)},{lon.ToString(CultureInfo.InvariantCulture)}";
            items.Add(new CommandContextItem(new OpenUrlCommand(url) { Name = "Open in Google Maps" }));
        }
        // user_id is the HA user UUID this person is linked to — handy
        // when wiring up automations or template conditions.
        if (entity.Attributes.TryGetValue("user_id", out var uid) && uid is string uids && !string.IsNullOrEmpty(uids))
        {
            items.Add(new CommandContextItem(new CopyTextCommand(uids) { Name = "Copy user ID" }));
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        // Person `state` is the zone name ("home" / "not_home" / a custom
        // zone). Lat/lon come from whichever device_tracker reports the
        // freshest data; HA exposes that tracker via the `source` attribute.
        if (DomainHelpers.TryGetDouble(entity.Attributes, "latitude", out var lat) &&
            DomainHelpers.TryGetDouble(entity.Attributes, "longitude", out var lon))
        {
            rows.Add(DomainHelpers.Row("Location", $"{lat}, {lon}"));
        }
        if (DomainHelpers.TryGetDouble(entity.Attributes, "gps_accuracy", out var acc))
        {
            rows.Add(DomainHelpers.Row("GPS accuracy", $"{(int)acc} m"));
        }
        if (entity.Attributes.TryGetValue("source", out var src) && src is string srcs && !string.IsNullOrEmpty(srcs))
        {
            rows.Add(DomainHelpers.Row("Source", srcs));
        }
    }
}
