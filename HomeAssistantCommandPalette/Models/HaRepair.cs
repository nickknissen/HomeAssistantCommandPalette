using System;
using System.Collections.Generic;

namespace HomeAssistantCommandPalette.Models;

/// <summary>
/// One entry from Home Assistant's issue registry (the "Repairs" surface
/// in the HA UI). Fetched via the WebSocket command
/// <c>repairs/list_issues</c>; REST has no equivalent endpoint.
/// </summary>
public sealed record HaRepair(
    string IssueId,
    string Domain,
    string Severity,
    string Summary,
    string? LearnMoreUrl,
    string? BreaksInHaVersion,
    DateTimeOffset? Created,
    bool Ignored,
    bool IsFixable);

/// <summary>
/// Result wrapper for <see cref="Services.IHaClient.GetRepairs"/>. Mirrors
/// <see cref="HaCalendarsResult"/> so the page can render auth/network
/// errors the same way.
/// </summary>
public sealed class HaRepairsResult
{
    public IReadOnlyList<HaRepair> Issues { get; init; } = Array.Empty<HaRepair>();
    public HaErrorKind ErrorKind { get; init; }
    public string ErrorTitle { get; init; } = string.Empty;
    public string ErrorDescription { get; init; } = string.Empty;
    public bool HasError => ErrorKind != HaErrorKind.None;
}
