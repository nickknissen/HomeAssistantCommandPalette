using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

internal sealed partial class WeatherForecastPage : ListPage
{
    private readonly IHaClient _client;
    private readonly HaSettings _settings;
    private readonly HaEntity _entity;

    public WeatherForecastPage(IHaClient client, HaSettings settings, HaEntity entity)
    {
        _client = client;
        _settings = settings;
        _entity = entity;
        Title = entity.FriendlyName;
        Name = "Show forecast";
        Icon = Icons.WeatherForCondition(entity.State, string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase));
        ShowDetails = true;
        PlaceholderText = $"Search {_entity.FriendlyName.ToLowerInvariant()} forecast";
    }

    public override IListItem[] GetItems()
    {
        var result = _client.GetWeatherForecast(_entity.EntityId);
        if (!result.Success)
        {
            return [new ListItem(new NoOpCommand()) { Title = "Forecast unavailable", Subtitle = result.Error ?? "Home Assistant did not return forecast data." }];
        }

        var items = new List<IListItem>();
        items.Add(BuildCurrentConditionsItem());

        foreach (var forecast in result.Hourly.Take(24))
        {
            items.Add(BuildForecastItem(forecast, daily: false));
        }
        foreach (var forecast in result.Daily.Take(7))
        {
            items.Add(BuildForecastItem(forecast, daily: true));
        }

        if (items.Count == 1)
        {
            items.Add(new ListItem(new NoOpCommand()) { Title = "No forecast rows returned", Subtitle = "The weather entity may not support hourly or daily forecasts." });
        }
        return items.ToArray();
    }

    private ListItem BuildCurrentConditionsItem()
    {
        var rows = new List<IDetailsElement> { DomainHelpers.Row("Condition", DomainHelpers.FormatStateWithUnit(_entity)) };
        var ctx = new DomainCtx(_entity, _client, _settings, () => { });
        new WeatherBehavior().AddDetailRows(in ctx, rows);

        return new ListItem(new NoOpCommand())
        {
            Title = "Current conditions",
            Subtitle = DomainHelpers.FormatStateWithUnit(_entity),
            Icon = Icons.WeatherForCondition(_entity.State, string.Equals(_entity.State, "unavailable", StringComparison.OrdinalIgnoreCase)),
            Details = new Details { Title = _entity.FriendlyName, Metadata = rows.ToArray() },
        };
    }

    private static ListItem BuildForecastItem(HaWeatherForecast forecast, bool daily)
    {
        var title = forecast.Time is { } time
            ? daily ? time.ToLocalTime().ToString("ddd, MMM d", CultureInfo.CurrentCulture) : time.ToLocalTime().ToString("ddd HH:mm", CultureInfo.CurrentCulture)
            : daily ? "Daily forecast" : "Hourly forecast";
        var subtitleParts = new List<string>();
        if (!string.IsNullOrEmpty(forecast.Condition)) subtitleParts.Add(FormatCondition(forecast.Condition));
        if (forecast.Temperature is { } temp) subtitleParts.Add($"{FormatNum(temp)}°");
        if (forecast.Templow is { } low) subtitleParts.Add($"low {FormatNum(low)}°");
        if (forecast.PrecipitationProbability is { } pop) subtitleParts.Add($"{FormatNum(pop)}% rain");

        var rows = new List<IDetailsElement>();
        if (!string.IsNullOrEmpty(forecast.Condition)) rows.Add(DomainHelpers.Row("Condition", FormatCondition(forecast.Condition)));
        if (forecast.Temperature is { } temp2) rows.Add(DomainHelpers.Row("Temperature", $"{FormatNum(temp2)}°"));
        if (forecast.Templow is { } low2) rows.Add(DomainHelpers.Row("Low", $"{FormatNum(low2)}°"));
        if (forecast.Precipitation is { } precip) rows.Add(DomainHelpers.Row("Precipitation", FormatNum(precip)));
        if (forecast.PrecipitationProbability is { } pop2) rows.Add(DomainHelpers.Row("Precipitation probability", $"{FormatNum(pop2)}%"));
        if (forecast.WindSpeed is { } wind) rows.Add(DomainHelpers.Row("Wind speed", FormatNum(wind)));
        if (forecast.WindBearing is { } bearing) rows.Add(DomainHelpers.Row("Wind bearing", $"{WeatherBehavior.CompassFromBearing(bearing)} ({(int)Math.Round(bearing)}°)"));
        if (forecast.Humidity is { } humidity) rows.Add(DomainHelpers.Row("Humidity", $"{FormatNum(humidity)}%"));

        return new ListItem(new NoOpCommand())
        {
            Title = title,
            Subtitle = string.Join(" · ", subtitleParts),
            Icon = Icons.WeatherForCondition(forecast.Condition, unavailable: false),
            Details = new Details { Title = title, Metadata = rows.ToArray() },
        };
    }

    private static string FormatCondition(string condition)
        => condition.Replace('-', ' ').Replace('_', ' ');

    private static string FormatNum(double v)
        => v == Math.Floor(v)
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.#", CultureInfo.InvariantCulture);
}
