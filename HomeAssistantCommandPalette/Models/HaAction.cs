using System.Collections.Generic;

namespace HomeAssistantCommandPalette.Models;

/// <summary>
/// A row from HA's services registry — what the UI now calls an
/// "action." HA renamed `service` to `action` in 2024.x; the REST API
/// endpoint and field names are still `service`, but everything the
/// user sees should say Action.
/// </summary>
/// <param name="Domain">e.g. <c>light</c>, <c>script</c>.</param>
/// <param name="Service">Underlying service name (HA API). For
/// scripts this is the object_id (e.g. <c>goodnight</c>).</param>
/// <param name="Name">Display name from the registry, falling back to
/// a humanized form of <paramref name="Service"/>.</param>
/// <param name="Description">Brief description from the registry.</param>
/// <param name="Fields">Field schema (<c>{field_key: {name, selector, …}}</c>),
/// empty when the action has no declared inputs.</param>
/// <param name="HasTarget">True when the registry includes a <c>target</c>
/// block — i.e. the action is expected to operate on one or more
/// entities, so the form should collect an entity_id.</param>
public sealed record HaAction(
    string Domain,
    string Service,
    string Name,
    string Description,
    IReadOnlyDictionary<string, object?> Fields,
    bool HasTarget);
