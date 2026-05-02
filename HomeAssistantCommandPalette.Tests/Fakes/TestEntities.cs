using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;

namespace HomeAssistantCommandPalette.Tests.Fakes;

/// <summary>
/// Convenience factory for synthetic <see cref="HaEntity"/> fixtures.
/// </summary>
internal static class TestEntities
{
    public static HaEntity Make(
        string entityId,
        string state = "on",
        string? friendlyName = null,
        IReadOnlyDictionary<string, object?>? attributes = null)
    {
        var attrs = new Dictionary<string, object?>(System.StringComparer.Ordinal);
        if (friendlyName is not null) attrs["friendly_name"] = friendlyName;
        if (attributes is not null)
        {
            foreach (var kv in attributes) attrs[kv.Key] = kv.Value;
        }
        return new HaEntity
        {
            EntityId = entityId,
            State = state,
            Attributes = attrs,
        };
    }
}
