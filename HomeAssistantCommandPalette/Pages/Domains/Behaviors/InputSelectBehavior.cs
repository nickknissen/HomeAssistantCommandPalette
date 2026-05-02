using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class InputSelectBehavior : DomainBehavior
{
    public override string Domain => "input_select";

    public override IconInfo BuildIcon(in DomainCtx ctx) => Icons.InputSelect;

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        // "Select option…" submenu — every option except the current
        // state. Hidden if the entity has no options or is unavailable.
        if (string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase)) return;
        if (!entity.Attributes.TryGetValue("options", out var opts) || opts is not List<object?> options) return;

        // `in` parameters can't be captured by lambdas — copy the fields
        // we close over into locals first.
        var client = ctx.Client;
        var onSuccess = ctx.OnSuccess;
        var entityId = entity.EntityId;
        var currentState = entity.State;

        var subItems = options
            .OfType<string>()
            .Where(o => !string.Equals(o, currentState, StringComparison.Ordinal))
            .Select(o => (IContextItem)new CommandContextItem(new CallServiceCommand(
                client, "input_select", "select_option", entityId,
                o, extraData: new Dictionary<string, object?> { ["option"] = o },
                onSuccess: onSuccess)))
            .ToArray();
        if (subItems.Length > 0)
        {
            items.Add(new CommandContextItem(new NoOpCommand())
            {
                Title = "Select option…",
                MoreCommands = subItems,
            });
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        if (ctx.Entity.Attributes.TryGetValue("options", out var opts) && opts is List<object?> options)
        {
            rows.Add(DomainHelpers.Row("Options", options.Count.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
