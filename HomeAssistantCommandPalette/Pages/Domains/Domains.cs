using System.Collections.Generic;
using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains;

/// <summary>
/// Declarative factories for the "boring 80%" of HA domains — the ones
/// whose entire surface is a service-call shape (toggle / activate /
/// increment) plus a list of attribute-driven detail rows.
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

    /// <summary>
    /// Stateless action domain: primary action is <c>{domain}.{service}</c>
    /// with a verb-prefixed display name (e.g. "Activate Movie Time" for
    /// <c>scene.turn_on</c>). No context items by default. Used by
    /// <c>scene</c>, <c>script</c>, <c>button</c>, <c>input_button</c>.
    /// </summary>
    public static DomainBehavior Activate(
        string domain,
        string service,
        string verb,
        params (string Attr, string Label)[] extraRows)
        => new ActivateBehavior(domain, service, verb, extraRows);

    /// <summary>
    /// Numeric domain: primary action is <c>{domain}.increment</c>;
    /// context menu exposes increment plus optional decrement / reset.
    /// Used by <c>counter</c>.
    /// </summary>
    public static DomainBehavior Increment(
        string domain,
        bool addDecrement = true,
        bool addReset = true,
        params (string Attr, string Label)[] extraRows)
        => new IncrementBehavior(domain, addDecrement, addReset, extraRows);

    private static void AppendExtraRows(HaEntity entity, List<IDetailsElement> rows, (string Attr, string Label)[] extras)
    {
        if (extras.Length == 0) return;
        foreach (var (attr, label) in extras)
        {
            if (entity.Attributes.TryGetValue(attr, out var v) && v is not null)
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
            => AppendExtraRows(ctx.Entity, rows, _extraRows);
    }

    private sealed class ActivateBehavior : DomainBehavior
    {
        private readonly string _service;
        private readonly string _verb;
        private readonly (string Attr, string Label)[] _extraRows;

        public ActivateBehavior(string domain, string service, string verb, (string Attr, string Label)[] extraRows)
        {
            Domain = domain;
            _service = service;
            _verb = verb;
            _extraRows = extraRows;
        }

        public override string Domain { get; }

        public override ICommand BuildPrimary(in DomainCtx ctx)
            => new CallServiceCommand(
                ctx.Client,
                Domain,
                _service,
                ctx.Entity.EntityId,
                $"{_verb} {ctx.Entity.FriendlyName}",
                onSuccess: ctx.OnSuccess);

        public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
            => AppendExtraRows(ctx.Entity, rows, _extraRows);
    }

    private sealed class IncrementBehavior : DomainBehavior
    {
        private readonly bool _addDecrement;
        private readonly bool _addReset;
        private readonly (string Attr, string Label)[] _extraRows;

        public IncrementBehavior(string domain, bool addDecrement, bool addReset, (string Attr, string Label)[] extraRows)
        {
            Domain = domain;
            _addDecrement = addDecrement;
            _addReset = addReset;
            _extraRows = extraRows;
        }

        public override string Domain { get; }

        public override ICommand BuildPrimary(in DomainCtx ctx)
            => new CallServiceCommand(
                ctx.Client,
                Domain,
                "increment",
                ctx.Entity.EntityId,
                $"Increment {ctx.Entity.FriendlyName}",
                onSuccess: ctx.OnSuccess);

        public override void AddContextItems(in DomainCtx ctx, List<IContextItem> items)
        {
            // Mirrors the legacy BuildContextCommands behavior: increment
            // appears in both the primary slot and the context menu.
            items.Add(new CommandContextItem(new CallServiceCommand(
                ctx.Client,
                Domain,
                "increment",
                ctx.Entity.EntityId,
                $"Increment {ctx.Entity.FriendlyName}",
                onSuccess: ctx.OnSuccess)));
            if (_addDecrement)
            {
                items.Add(new CommandContextItem(new CallServiceCommand(
                    ctx.Client,
                    Domain,
                    "decrement",
                    ctx.Entity.EntityId,
                    $"Decrement {ctx.Entity.FriendlyName}",
                    onSuccess: ctx.OnSuccess)));
            }
            if (_addReset)
            {
                items.Add(new CommandContextItem(new CallServiceCommand(
                    ctx.Client,
                    Domain,
                    "reset",
                    ctx.Entity.EntityId,
                    $"Reset {ctx.Entity.FriendlyName}",
                    onSuccess: ctx.OnSuccess)));
            }
        }

        public override void AddDetailRows(in DomainCtx ctx, List<IDetailsElement> rows)
            => AppendExtraRows(ctx.Entity, rows, _extraRows);
    }
}
