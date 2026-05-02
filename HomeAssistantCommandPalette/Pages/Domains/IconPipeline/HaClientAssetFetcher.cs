using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Real <see cref="IEntityAssetFetcher"/> adapter. Today's only fetched
/// asset is the <c>person</c> <c>entity_picture</c> avatar (Gravatar /
/// HA-served). The fetch goes through <see cref="IHaClient"/> so the
/// authenticated bytes can be cached to a temp file and handed back as
/// a local path.
/// </summary>
internal sealed class HaClientAssetFetcher(IHaClient client) : IEntityAssetFetcher
{
    public string? TryFetch(HaEntity entity)
    {
        if (entity.Domain == "person"
            && entity.Attributes.TryGetValue("entity_picture", out var pic)
            && pic is string picUrl
            && !string.IsNullOrEmpty(picUrl))
        {
            return client.GetEntityPicturePath(entity.EntityId, picUrl);
        }
        return null;
    }
}
