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
