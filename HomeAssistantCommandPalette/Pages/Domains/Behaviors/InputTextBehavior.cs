using HomeAssistantCommandPalette.Pages.Forms;
using Microsoft.CommandPalette.Extensions;

namespace HomeAssistantCommandPalette.Pages.Domains.Behaviors;

public sealed class InputTextBehavior : DomainBehavior
{
    public override string Domain => "input_text";

    public override ICommand BuildPrimary(in DomainCtx ctx)
        => new HelperFormPage(
            ctx.Entity,
            new InputTextFormContent(ctx.Entity, ctx.Client, ctx.OnSuccess),
            $"Set {ctx.Entity.FriendlyName}",
            Icons.InputText);
}
