using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class WeatherBehaviorTests
{
    private static DomainCtx MakeCtx(IReadOnlyDictionary<string, object?> attrs)
    {
        var entity = TestEntities.Make("weather.home", "sunny", attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    [Theory]
    [InlineData(0, "N")]
    [InlineData(45, "NE")]
    [InlineData(90, "E")]
    [InlineData(180, "S")]
    [InlineData(270, "W")]
    [InlineData(315, "NW")]
    [InlineData(360, "N")]
    [InlineData(-90, "W")]
    public void CompassFromBearing_handles_full_circle(double degrees, string expected)
    {
        Assert.Equal(expected, WeatherBehavior.CompassFromBearing(degrees));
    }

    [Fact]
    public void Wind_row_combines_speed_and_compass_and_bearing()
    {
        var ctx = MakeCtx(new Dictionary<string, object?>
        {
            ["wind_speed"] = 12.0,
            ["wind_speed_unit"] = "km/h",
            ["wind_bearing"] = 315.0,
        });
        var rows = new List<IDetailsElement>();
        new WeatherBehavior().AddDetailRows(in ctx, rows);

        var row = rows.OfType<DetailsElement>().Single(r => r.Key == "Wind");
        Assert.Equal("12 km/h NW (315°)", ((DetailsLink)row.Data!).Text);
    }

    [Fact]
    public void Temperature_row_uses_entity_unit()
    {
        var ctx = MakeCtx(new Dictionary<string, object?>
        {
            ["temperature"] = 21.5,
            ["temperature_unit"] = "°C",
        });
        var rows = new List<IDetailsElement>();
        new WeatherBehavior().AddDetailRows(in ctx, rows);
        var row = rows.OfType<DetailsElement>().Single(r => r.Key == "Temperature");
        Assert.Equal("21,5 °C", ((DetailsLink)row.Data!).Text.Replace('.', ','));  // tolerate culture
    }

    [Fact]
    public void Missing_attributes_omit_their_rows()
    {
        var ctx = MakeCtx(new Dictionary<string, object?> { ["temperature"] = 18.0 });
        var rows = new List<IDetailsElement>();
        new WeatherBehavior().AddDetailRows(in ctx, rows);
        Assert.Single(rows);  // Only Temperature.
    }
}
