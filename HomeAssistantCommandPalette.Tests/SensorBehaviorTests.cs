using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class SensorBehaviorTests
{
    private static DomainCtx MakeCtx(string state, IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("sensor.x", state, attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    // -- Detail rows ---------------------------------------------------

    [Fact]
    public void Detail_rows_include_device_class_and_state_class()
    {
        var ctx = MakeCtx("120", new Dictionary<string, object?>
        {
            ["device_class"] = "power",
            ["state_class"] = "measurement",
        });
        var rows = new List<IDetailsElement>();
        new SensorBehavior().AddDetailRows(in ctx, rows);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows.OfType<DetailsElement>(), r => r.Key == "Device class");
        Assert.Contains(rows.OfType<DetailsElement>(), r => r.Key == "State class");
    }

    [Fact]
    public void Detail_rows_omit_missing_attributes()
    {
        var ctx = MakeCtx("on", attrs: null);
        var rows = new List<IDetailsElement>();
        new SensorBehavior().AddDetailRows(in ctx, rows);
        Assert.Empty(rows);
    }

    // -- Battery bucket dispatch (the load-bearing logic) ---------------

    [Theory]
    [InlineData("0",   "battery-outline-low")]   // empty outline + red
    [InlineData("5",   "battery-outline-low")]   // bucket 0
    [InlineData("15",  "battery-10-low")]        // ≤20 → red
    [InlineData("20",  "battery-20-low")]        // boundary: still red
    [InlineData("25",  "battery-20-on")]         // ≤30 → yellow
    [InlineData("30",  "battery-30-on")]         // boundary: still yellow
    [InlineData("50",  "battery-50-off")]        // >30 → blue
    [InlineData("100", "battery-full-off")]
    public void Battery_state_picks_bucket_and_tint(string state, string expectedStem)
    {
        var ctx = MakeCtx(state, new Dictionary<string, object?>
        {
            ["device_class"] = "battery",
        });
        var icon = new SensorBehavior().BuildIcon(in ctx);
        Assert.Contains(expectedStem, IconAssert.PathOf(icon));
    }

    [Fact]
    public void Unparsable_battery_state_falls_back_to_static_icon()
    {
        var ctx = MakeCtx("not-a-number", new Dictionary<string, object?>
        {
            ["device_class"] = "battery",
        });
        IconAssert.Same(Icons.Battery, new SensorBehavior().BuildIcon(in ctx));
    }

    [Fact]
    public void Unavailable_battery_uses_unavailable_icon()
    {
        var ctx = MakeCtx("unavailable", new Dictionary<string, object?>
        {
            ["device_class"] = "battery",
        });
        IconAssert.Same(Icons.BatteryUnavailable, new SensorBehavior().BuildIcon(in ctx));
    }

    // -- Numeric sensor device_class fallthrough ------------------------

    public static IEnumerable<object[]> NumericDeviceClasses() => new[]
    {
        new object[] { "temperature",         nameof(Icons.Temperature) },
        new object[] { "humidity",            nameof(Icons.Humidity) },
        new object[] { "moisture",            nameof(Icons.Humidity) },          // aliased
        new object[] { "pressure",            nameof(Icons.Pressure) },
        new object[] { "atmospheric_pressure", nameof(Icons.Pressure) },          // aliased
        new object[] { "energy",              nameof(Icons.Energy) },
        new object[] { "power",               nameof(Icons.Energy) },             // aliased to energy on sensor (not binary_sensor)
        new object[] { "current",             nameof(Icons.Energy) },             // aliased
        new object[] { "voltage",             nameof(Icons.Energy) },             // aliased
        new object[] { "apparent_power",      nameof(Icons.Energy) },             // aliased
        new object[] { "power_factor",        nameof(Icons.PowerFactor) },
        new object[] { "carbon_dioxide",      nameof(Icons.CarbonDioxide) },
    };

    [Theory]
    [MemberData(nameof(NumericDeviceClasses))]
    public void Sensor_device_class_dispatches_to_expected_icon(string deviceClass, string expectedIconName)
    {
        var ctx = MakeCtx("123", new Dictionary<string, object?>
        {
            ["device_class"] = deviceClass,
        });
        var actual = new SensorBehavior().BuildIcon(in ctx);
        IconAssert.Same(IconByName(expectedIconName), actual);
    }

    [Fact]
    public void Missing_device_class_falls_back_to_shape()
    {
        var ctx = MakeCtx("123", attrs: null);
        IconAssert.Same(Icons.Shape, new SensorBehavior().BuildIcon(in ctx));
    }

    [Fact]
    public void Unknown_device_class_falls_back_to_shape()
    {
        var ctx = MakeCtx("123", new Dictionary<string, object?>
        {
            ["device_class"] = "wibble",
        });
        IconAssert.Same(Icons.Shape, new SensorBehavior().BuildIcon(in ctx));
    }

    [Fact]
    public void Unavailable_unknown_device_class_falls_back_to_shape_unavailable()
    {
        var ctx = MakeCtx("unavailable", new Dictionary<string, object?>
        {
            ["device_class"] = "wibble",
        });
        IconAssert.Same(Icons.ShapeUnavailable, new SensorBehavior().BuildIcon(in ctx));
    }

    [Theory]
    [InlineData("temperature",   nameof(Icons.TemperatureUnavailable))]
    [InlineData("humidity",      nameof(Icons.HumidityUnavailable))]
    [InlineData("pressure",      nameof(Icons.PressureUnavailable))]
    [InlineData("energy",        nameof(Icons.EnergyUnavailable))]
    [InlineData("power_factor",  nameof(Icons.PowerFactorUnavailable))]
    [InlineData("carbon_dioxide", nameof(Icons.CarbonDioxideUnavailable))]
    public void Unavailable_known_device_class_uses_unavailable_variant(string deviceClass, string expectedIconName)
    {
        var ctx = MakeCtx("unavailable", new Dictionary<string, object?>
        {
            ["device_class"] = deviceClass,
        });
        IconAssert.Same(IconByName(expectedIconName), new SensorBehavior().BuildIcon(in ctx));
    }

    private static IconInfo IconByName(string name)
        => (IconInfo)typeof(Icons).GetProperty(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
}
