using System.Collections.Generic;
using HomeAssistantCommandPalette;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class BinarySensorBehaviorTests
{
    private static HaEntity MakeEntity(string state, IReadOnlyDictionary<string, object?>? attrs = null)
        => TestEntities.Make("binary_sensor.x", state, attributes: attrs);

    private static DomainCtx MakeCtx(string state, IReadOnlyDictionary<string, object?>? attrs = null)
        => new(MakeEntity(state, attrs), new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });

    public static IEnumerable<object[]> StateBearingDispatch() => new[]
    {
        // device_class, state, expected icon name (matches Icons.X property name)
        new object[] { "door",         "on",  nameof(Icons.DoorOpen) },
        new object[] { "door",         "off", nameof(Icons.DoorClosed) },
        new object[] { "garage_door",  "on",  nameof(Icons.DoorOpen) },          // alias
        new object[] { "garage_door",  "off", nameof(Icons.DoorClosed) },
        new object[] { "window",       "on",  nameof(Icons.WindowOpen) },
        new object[] { "window",       "off", nameof(Icons.WindowClosed) },
        new object[] { "opening",      "on",  nameof(Icons.WindowOpen) },        // alias
        new object[] { "motion",       "on",  nameof(Icons.MotionDetected) },
        new object[] { "motion",       "off", nameof(Icons.MotionClear) },
        new object[] { "occupancy",    "on",  nameof(Icons.MotionDetected) },    // alias
        new object[] { "presence",     "on",  nameof(Icons.MotionDetected) },    // alias
        new object[] { "moving",       "on",  nameof(Icons.MotionDetected) },    // alias
        new object[] { "connectivity", "on",  nameof(Icons.ConnectivityOn) },
        new object[] { "connectivity", "off", nameof(Icons.ConnectivityOff) },
        new object[] { "plug",         "on",  nameof(Icons.PlugOn) },
        new object[] { "plug",         "off", nameof(Icons.PlugOff) },
        new object[] { "power",        "on",  nameof(Icons.PlugOn) },            // alias
        new object[] { "update",       "on",  nameof(Icons.UpdateOn) },
        new object[] { "update",       "off", nameof(Icons.UpdateOff) },
    };

    [Theory]
    [MemberData(nameof(StateBearingDispatch))]
    public void State_bearing_device_class_dispatches_correctly(string deviceClass, string state, string expectedIconName)
    {
        var entity = MakeEntity(state, new Dictionary<string, object?>
        {
            ["device_class"] = deviceClass,
        });
        var actual = new BinarySensorIconRule().Pick(entity);
        IconAssert.Same(IconByName(expectedIconName), actual);
    }

    [Theory]
    [InlineData("door",         nameof(Icons.DoorUnavailable))]
    [InlineData("garage_door",  nameof(Icons.DoorUnavailable))]
    [InlineData("window",       nameof(Icons.WindowUnavailable))]
    [InlineData("opening",      nameof(Icons.WindowUnavailable))]
    [InlineData("motion",       nameof(Icons.MotionUnavailable))]
    [InlineData("connectivity", nameof(Icons.ConnectivityUnavailable))]
    [InlineData("plug",         nameof(Icons.PlugUnavailable))]
    [InlineData("power",        nameof(Icons.PlugUnavailable))]
    [InlineData("update",       nameof(Icons.UpdateUnavailable))]
    public void Unavailable_state_uses_unavailable_variant(string deviceClass, string expectedIconName)
    {
        var entity = MakeEntity("unavailable", new Dictionary<string, object?>
        {
            ["device_class"] = deviceClass,
        });
        IconAssert.Same(IconByName(expectedIconName), new BinarySensorIconRule().Pick(entity));
    }

    [Fact]
    public void Unknown_device_class_falls_back_to_shape()
    {
        var entity = MakeEntity("on", new Dictionary<string, object?>
        {
            ["device_class"] = "wibble",
        });
        IconAssert.Same(Icons.Shape, new BinarySensorIconRule().Pick(entity));
    }

    [Fact]
    public void Missing_device_class_falls_back_to_shape()
    {
        var entity = MakeEntity("on", attrs: null);
        IconAssert.Same(Icons.Shape, new BinarySensorIconRule().Pick(entity));
    }

    [Fact]
    public void Detail_rows_include_device_class_when_present()
    {
        var ctx = MakeCtx("on", new Dictionary<string, object?>
        {
            ["device_class"] = "door",
        });
        var rows = new List<Microsoft.CommandPalette.Extensions.IDetailsElement>();
        new BinarySensorBehavior().AddDetailRows(in ctx, rows);
        Assert.Single(rows);
    }

    private static IconInfo IconByName(string name)
        => (IconInfo)typeof(Icons).GetProperty(name,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
}
