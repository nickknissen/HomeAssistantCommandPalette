using System.Collections.Generic;
using System.Globalization;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class InputNumberBehavior : DomainBehavior
{
    public override string Domain => "input_number";

    public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
    {
        var entity = ctx.Entity;
        // input_number.increment / decrement use the entity's own step;
        // gate the action when the result would clamp out of range.
        var hasValue = double.TryParse(entity.State,
            NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
        var hasMin = DomainHelpers.TryGetDouble(entity.Attributes, "min", out var min);
        var hasMax = DomainHelpers.TryGetDouble(entity.Attributes, "max", out var max);
        var hasStep = DomainHelpers.TryGetDouble(entity.Attributes, "step", out var step);

        if (hasValue && hasStep && (!hasMax || v + step <= max))
        {
            items.Add(new CommandContextItem(new CallServiceCommand(
                ctx.Client, "input_number", "increment", entity.EntityId,
                $"Increase {entity.FriendlyName}", onSuccess: ctx.OnSuccess)));
        }
        if (hasValue && hasStep && (!hasMin || v - step >= min))
        {
            items.Add(new CommandContextItem(new CallServiceCommand(
                ctx.Client, "input_number", "decrement", entity.EntityId,
                $"Decrease {entity.FriendlyName}", onSuccess: ctx.OnSuccess)));
        }
    }

    public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
    {
        var entity = ctx.Entity;
        if (DomainHelpers.TryGetDouble(entity.Attributes, "min", out var min)) rows.Add(DomainHelpers.Row("Min", FormatNum(min)));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "max", out var max)) rows.Add(DomainHelpers.Row("Max", FormatNum(max)));
        if (DomainHelpers.TryGetDouble(entity.Attributes, "step", out var step)) rows.Add(DomainHelpers.Row("Step", FormatNum(step)));
        if (entity.Attributes.TryGetValue("mode", out var mode) && mode is string ms && !string.IsNullOrEmpty(ms))
        {
            rows.Add(DomainHelpers.Row("Mode", ms));
        }
    }

    private static string FormatNum(double v) =>
        v == System.Math.Floor(v)
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.###", CultureInfo.InvariantCulture);
}
