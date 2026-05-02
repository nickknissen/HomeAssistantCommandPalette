using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Composes the impure <see cref="IEntityAssetFetcher"/> with the pure
/// <see cref="IIconRules"/>. <see cref="EntityListPage"/> calls this for
/// every row; behaviors no longer make icon decisions.
/// </summary>
internal interface IEntityIconResolver
{
    IconInfo Resolve(HaEntity entity);
}
