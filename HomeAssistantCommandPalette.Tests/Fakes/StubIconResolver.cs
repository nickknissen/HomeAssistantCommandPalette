using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests.Fakes;

/// <summary>
/// Icon resolver that answers with a fixed icon. Page-level tests care
/// about which rows and fetches a render produces, not which glyph each
/// row ends up with — <c>RegistryIconRules</c> has its own tests.
/// </summary>
internal sealed class StubIconResolver : IEntityIconResolver
{
    public static readonly IconInfo Icon = new("");

    public IconInfo Resolve(HaEntity entity) => Icon;
}
