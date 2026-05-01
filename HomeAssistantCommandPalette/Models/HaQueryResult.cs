using System;
using System.Collections.Generic;

namespace HomeAssistantCommandPalette.Models;

public enum HaErrorKind
{
    None,
    NotConfigured,
    InvalidUrl,
    Unauthorized,
    NetworkError,
    ParseFailed,
    Unknown,
}

public sealed class HaQueryResult
{
    public IReadOnlyList<HaEntity> Items { get; init; } = Array.Empty<HaEntity>();

    public HaErrorKind ErrorKind { get; init; }
    public string ErrorTitle { get; init; } = string.Empty;
    public string ErrorDescription { get; init; } = string.Empty;

    public bool HasError => ErrorKind != HaErrorKind.None;
}

/// <summary>
/// One-shot probe result from <c>GET /api/config</c>. Surfaces HA version,
/// location, time zone and round-trip latency so the Connection Check page
/// can render a usable diagnostic without a second round-trip.
/// </summary>
public sealed record HaConfigProbe(
    bool Success,
    HaErrorKind ErrorKind,
    string? ErrorMessage,
    string? Version,
    string? LocationName,
    string? TimeZone,
    string? State,
    long LatencyMs);

/// <summary>
/// Result of <c>POST /api/conversation/process</c>. <see cref="Success"/>
/// reflects HA's <c>response_type</c> — "error" maps to false; anything
/// else (including "action_done" and "query_answer") is success.
/// <see cref="Speech"/> is the human-readable answer / status message;
/// <see cref="Error"/> carries the error message when Success is false.
/// </summary>
public sealed record HaAssistResult(
    bool Success,
    string Speech,
    string? ResponseType,
    string? Error);
