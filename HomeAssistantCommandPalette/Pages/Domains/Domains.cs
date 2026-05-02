using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Declarative factories for the "boring 80%" of HA domains — the ones
/// whose entire surface is a service-call shape (toggle / activate /
/// increment / active-vs-idle) plus a list of attribute-driven detail
/// rows.
/// </summary>
public static class Domains
{
    /// <summary>
    /// On/off domain: primary action is <c>{domain}.toggle</c>; context
    /// menu exposes <c>turn_on</c> / <c>turn_off</c>. Used by
    /// <c>switch</c>, <c>input_boolean</c>, <c>group</c>, …
    /// </summary>
    public static DomainBehavior Toggle(
        string domain,
        params (string Attr, string Label)[] extraRows)
        => new ToggleBehavior(domain, extraRows);

    private sealed class ToggleBehavior : DomainBehavior
    {
        private readonly (string Attr, string Label)[] _extraRows;

        public ToggleBehavior(string domain, (string Attr, string Label)[] extraRows)
        {
            Domain = domain;
            _extraRows = extraRows;
        }

        public override string Domain { get; }

        public override ICommand BuildPrimary(in DomainCtx ctx)
            => new CallServiceCommand(
                ctx.Client,
                Domain,
                "toggle",
                ctx.Entity.EntityId,
                $"Toggle {ctx.Entity.FriendlyName}",
                icon: Icons.Toggle,
                onSuccess: ctx.OnSuccess);

        public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
        {
            items.Add(new CommandContextItem(new CallServiceCommand(
                ctx.Client,
                Domain,
                "turn_on",
                ctx.Entity.EntityId,
                $"Turn on {ctx.Entity.FriendlyName}",
                icon: Icons.TurnOn,
                onSuccess: ctx.OnSuccess)));
            items.Add(new CommandContextItem(new CallServiceCommand(
                ctx.Client,
                Domain,
                "turn_off",
                ctx.Entity.EntityId,
                $"Turn off {ctx.Entity.FriendlyName}",
                icon: Icons.TurnOff,
                onSuccess: ctx.OnSuccess)));
        }

        public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
        {
            if (_extraRows.Length == 0) return;
            foreach (var (attr, label) in _extraRows)
            {
                if (ctx.Entity.Attributes.TryGetValue(attr, out var v)
                    && v is not null)
                {
                    var s = v.ToString();
                    if (!string.IsNullOrEmpty(s))
                    {
                        rows.Add(new DetailsElement
                        {
                            Key = label,
                            Data = new DetailsLink { Text = s },
                        });
                    }
                }
            }
        }
    }
}
