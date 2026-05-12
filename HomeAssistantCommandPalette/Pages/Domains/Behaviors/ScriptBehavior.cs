using HomeAssistantCommandPalette.Commands;
using HomeAssistantCommandPalette.Pages.Forms;
using Microsoft.CommandPalette.Extensions;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class ScriptBehavior : DomainBehavior
{
    public override string Domain => "script";

    public override ICommand BuildPrimary(in DomainCtx ctx)
    {
        // HA does not expose script `fields` on the entity's state
        // attributes — it lives on the services registry (/api/services).
        // Query the client; if any fields are declared, open a form.
        // Otherwise fall back to one-tap script.turn_on.
        var objectId = ObjectIdOf(ctx.Entity.EntityId);
        var fields = ctx.Client.GetServiceFields("script", objectId);
        if (fields is { Count: > 0 })
        {
            return new HelperFormPage(
                ctx.Entity,
                new ScriptFormContent(ctx.Entity, ctx.Client, ctx.OnSuccess, fields),
                $"Run {ctx.Entity.FriendlyName}",
                Icons.ScriptOff);
        }

        return new CallServiceCommand(
            ctx.Client,
            "script",
            "turn_on",
            ctx.Entity.EntityId,
            $"Run {ctx.Entity.FriendlyName}",
            onSuccess: ctx.OnSuccess);
    }

    private static string ObjectIdOf(string entityId)
    {
        var dot = entityId.IndexOf('.');
        return dot < 0 ? entityId : entityId[(dot + 1)..];
    }
}
