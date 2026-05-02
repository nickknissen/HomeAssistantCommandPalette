using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class UpdateBehavior : DomainBehavior
{
    // HA UpdateEntityFeature bits.
    private const long Install = 1;
    private const long Backup = 8;

    public override string Domain => "update";

    public override IconInfo BuildIcon(in DomainCtx ctx)
    {
        if (string.Equals(ctx.Entity.State, "unavailable", StringComparison.OrdinalIgnoreCase))
            return Icons.UpdateUnavailable;
        // state="on" means an update is available — yellow draws the eye.
        return ctx.Entity.IsOn ? Icons.UpdateOn : Icons.UpdateOff;
    }

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        var name = entity.FriendlyName;
        var sf = entity.Attributes.TryGetValue("supported_features", out var sfo) && sfo is long b ? b : -1;
        bool Has(long bit) => sf < 0 || (sf & bit) == bit;

        var available = entity.IsOn;
        // in_progress is either a bool or a numeric percent while
        // installing; either non-false form means an install is already
        // running and Install should be hidden.
        var inProgress = entity.Attributes.TryGetValue("in_progress", out var ipo)
            && ipo is not (null or false);

        if (available && !inProgress && Has(Install))
        {
            // Always request a backup when supported — matches Raycast's
            // default. Without BACKUP support the param is dropped (HA
            // would 400 on it).
            IReadOnlyDictionary<string, object?>? extra = Has(Backup)
                ? new Dictionary<string, object?> { ["backup"] = true }
                : null;
            items.Add(new CommandContextItem(new CallServiceCommand(
                ctx.Client, "update", "install", entity.EntityId,
                $"Install {name}", icon: Icons.Play,
                extraData: extra, onSuccess: ctx.OnSuccess)));
        }

        if (available)
        {
            items.Add(new CommandContextItem(new CallServiceCommand(
                ctx.Client, "update", "skip", entity.EntityId,
                $"Skip {name}", icon: Icons.Next, onSuccess: ctx.OnSuccess)));
        }

        if (entity.Attributes.TryGetValue("release_url", out var ru) && ru is string rus && !string.IsNullOrEmpty(rus))
        {
            items.Add(new CommandContextItem(new OpenUrlCommand(rus) { Name = "Open release notes" }));
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (entity.Attributes.TryGetValue("title", out var t) && t is string ts && !string.IsNullOrEmpty(ts))
            rows.Add(DomainHelpers.Row("Title", ts));
        if (entity.Attributes.TryGetValue("installed_version", out var iv) && iv is string ivs && !string.IsNullOrEmpty(ivs))
            rows.Add(DomainHelpers.Row("Installed", ivs));
        if (entity.Attributes.TryGetValue("latest_version", out var lv) && lv is string lvs && !string.IsNullOrEmpty(lvs))
            rows.Add(DomainHelpers.Row("Latest", lvs));
        // in_progress can be a bool (false) or a numeric percent during install.
        if (entity.Attributes.TryGetValue("in_progress", out var ip))
        {
            switch (ip)
            {
                case bool b when b: rows.Add(DomainHelpers.Row("In progress", "yes")); break;
                case long l: rows.Add(DomainHelpers.Row("In progress", $"{l}%")); break;
                case double d: rows.Add(DomainHelpers.Row("In progress", $"{(int)d}%")); break;
            }
        }
        if (entity.Attributes.TryGetValue("auto_update", out var au) && au is bool aub)
            rows.Add(DomainHelpers.Row("Auto update", aub ? "yes" : "no"));
        if (entity.Attributes.TryGetValue("release_url", out var ru) && ru is string rus && !string.IsNullOrEmpty(rus))
            rows.Add(DomainHelpers.Row("Release notes", rus));
    }
}
