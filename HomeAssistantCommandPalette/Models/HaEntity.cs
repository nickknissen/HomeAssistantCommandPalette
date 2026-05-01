using System;
using System.Collections.Generic;

namespace HomeAssistantCommandPalette.Models;

public sealed class HaEntity
{
    public string EntityId { get; init; } = string.Empty;

    public string State { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, object?> Attributes { get; init; }
        = new Dictionary<string, object?>(StringComparer.Ordinal);

    public DateTimeOffset? LastChanged { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }

    public string Domain
    {
        get
        {
            var dot = EntityId.IndexOf('.', StringComparison.Ordinal);
            return dot > 0 ? EntityId[..dot] : string.Empty;
        }
    }

    public string FriendlyName
    {
        get
        {
            if (Attributes.TryGetValue("friendly_name", out var raw) && raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
            return EntityId;
        }
    }

    public bool IsOn => string.Equals(State, "on", StringComparison.OrdinalIgnoreCase)
        || string.Equals(State, "open", StringComparison.OrdinalIgnoreCase)
        || string.Equals(State, "playing", StringComparison.OrdinalIgnoreCase);
}
