using HomeAssistantCommandPalette.Models;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Composes the impure asset fetcher with the pure rule layer. Asset
/// fetch wins when it returns a path (today: person avatars), otherwise
/// the baked-SVG rule applies.
/// </summary>
internal sealed class CompositeIconResolver(IIconRules rules, IEntityAssetFetcher fetcher) : IEntityIconResolver
{
    public IconInfo Resolve(HaEntity entity)
    {
        var fetched = fetcher.TryFetch(entity);
        if (fetched is not null) return new IconInfo(fetched);
        return rules.Resolve(entity);
    }
}
