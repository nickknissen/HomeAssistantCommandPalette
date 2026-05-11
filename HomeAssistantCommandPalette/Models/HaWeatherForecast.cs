using System;
using System.Collections.Generic;

namespace HomeAssistantCommandPalette.Models;

public sealed class HaWeatherForecast
{
    public DateTimeOffset? Time { get; init; }
    public string Condition { get; init; } = string.Empty;
    public double? Temperature { get; init; }
    public double? Templow { get; init; }
    public double? Precipitation { get; init; }
    public double? PrecipitationProbability { get; init; }
    public double? WindSpeed { get; init; }
    public double? WindBearing { get; init; }
    public double? Humidity { get; init; }
}

public sealed class HaWeatherForecastResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<HaWeatherForecast> Hourly { get; init; } = Array.Empty<HaWeatherForecast>();
    public IReadOnlyList<HaWeatherForecast> Daily { get; init; } = Array.Empty<HaWeatherForecast>();
}
