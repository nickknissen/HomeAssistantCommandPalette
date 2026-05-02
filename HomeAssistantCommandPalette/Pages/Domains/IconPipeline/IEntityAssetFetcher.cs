using HomeAssistantCommandPalette.Models;

namespace HomeAssistantCommandPalette.Pages.Domains.IconPipeline;

/// <summary>
/// Impure port for entities whose icon is a fetched binary asset rather
/// than a baked SVG (today: <c>person</c> avatars via
/// <c>entity_picture</c>). Returns <c>null</c> when the entity has no
/// fetchable asset, the fetch failed, or the integration didn't expose a
/// URL — callers fall back to <see cref="IIconRules"/>.
/// </summary>
internal interface IEntityAssetFetcher
{
    string? TryFetch(HaEntity entity);
}
