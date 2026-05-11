using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Pages;

internal sealed partial class WeatherForecastPage : ContentPage
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
        Id = EntityListPage.EntityCommandId(entity.EntityId);
        Icon = Icons.WeatherForCondition(entity.State, string.Equals(entity.State, "unavailable", StringComparison.OrdinalIgnoreCase));
    }

    public override IContent[] GetContent()
    {
        var result = _client.GetWeatherForecast(_entity.EntityId);
        return [new MarkdownContent(BuildMarkdown(result))];
    }

    private string BuildMarkdown(HaWeatherForecastResult result)
    {
        var md = new StringBuilder();
        md.AppendLine("# " + EscapeMarkdown(_entity.FriendlyName));
        md.AppendLine();
        md.AppendLine("**Current:** " + EscapeMarkdown(DomainHelpers.FormatStateWithUnit(_entity)));
        md.AppendLine();

        AppendCurrentConditions(md);

        if (!result.Success)
        {
            md.AppendLine();
            md.AppendLine("> Forecast unavailable");
            md.AppendLine();
            md.AppendLine(EscapeMarkdown(result.Error ?? "Home Assistant did not return forecast data."));
            return md.ToString();
        }

        if (result.Hourly.Count == 0 && result.Daily.Count == 0)
        {
            md.AppendLine();
            md.AppendLine("> No forecast rows returned. The weather entity may not support hourly or daily forecasts.");
            return md.ToString();
        }

        AppendForecastTable(md, "Next 24 hours", result.Hourly.Take(24), daily: false);
        AppendForecastTable(md, "Next 7 days", result.Daily.Take(7), daily: true);
        return md.ToString();
    }

    private void AppendCurrentConditions(StringBuilder md)
    {
        var rows = new List<IDetailsElement> { DomainHelpers.Row("Condition", DomainHelpers.FormatStateWithUnit(_entity)) };
        var ctx = new DomainCtx(_entity, _client, _settings, () => { });
        new WeatherBehavior().AddDetailRows(in ctx, rows);

        md.AppendLine("## Current conditions");
        md.AppendLine();
        md.AppendLine("| Metric | Value |");
        md.AppendLine("| --- | --- |");
        foreach (var row in rows.OfType<DetailsElement>())
        {
            md.AppendLine("| " + EscapeTableCell(row.Key) + " | " + EscapeTableCell(DetailsText(row.Data)) + " |");
        }
        md.AppendLine();
    }

    private static void AppendForecastTable(StringBuilder md, string title, IEnumerable<HaWeatherForecast> forecasts, bool daily)
    {
        var rows = forecasts.ToArray();
        if (rows.Length == 0)
        {
            return;
        }

        md.AppendLine("## " + title);
        md.AppendLine();
        md.AppendLine("| Time | Condition | Temp | Rain | Wind | Humidity |");
        md.AppendLine("| --- | --- | ---: | ---: | --- | ---: |");
        foreach (var forecast in rows)
        {
            var time = forecast.Time is { } t
                ? daily ? t.ToLocalTime().ToString("ddd, MMM d", CultureInfo.CurrentCulture) : t.ToLocalTime().ToString("ddd HH:mm", CultureInfo.CurrentCulture)
                : daily ? "Daily" : "Hourly";
            var temp = FormatTemp(forecast);
            var rain = forecast.PrecipitationProbability is { } pop ? $"{FormatNum(pop)}%" : "";
            var wind = FormatWind(forecast);
            var humidity = forecast.Humidity is { } h ? $"{FormatNum(h)}%" : "";

            md.AppendLine("| " + EscapeTableCell(time) + " | " + EscapeTableCell(FormatCondition(forecast.Condition)) + " | " + EscapeTableCell(temp) + " | " + EscapeTableCell(rain) + " | " + EscapeTableCell(wind) + " | " + EscapeTableCell(humidity) + " |");
        }
        md.AppendLine();
    }

    private static string FormatTemp(HaWeatherForecast forecast)
    {
        var parts = new List<string>();
        if (forecast.Temperature is { } temp) parts.Add($"{FormatNum(temp)}°");
        if (forecast.Templow is { } low) parts.Add($"low {FormatNum(low)}°");
        return string.Join(" / ", parts);
    }

    private static string FormatWind(HaWeatherForecast forecast)
    {
        var parts = new List<string>();
        if (forecast.WindSpeed is { } wind) parts.Add(FormatNum(wind));
        if (forecast.WindBearing is { } bearing) parts.Add($"{WeatherBehavior.CompassFromBearing(bearing)} ({(int)Math.Round(bearing)}°)");
        return string.Join(" ", parts);
    }

    private static string DetailsText(object? data)
        => data switch
        {
            DetailsLink link => link.Text ?? string.Empty,
            null => string.Empty,
            _ => data.ToString() ?? string.Empty,
        };

    private static string FormatCondition(string condition)
        => condition.Replace('-', ' ').Replace('_', ' ');

    private static string FormatNum(double v)
        => v == Math.Floor(v)
            ? ((long)v).ToString(CultureInfo.InvariantCulture)
            : v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string EscapeTableCell(string value)
        => EscapeMarkdown(value).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    private static string EscapeMarkdown(string value)
        => value.Replace("\\", "\\\\").Replace("*", "\\*").Replace("_", "\\_").Replace("`", "\\`");
}
