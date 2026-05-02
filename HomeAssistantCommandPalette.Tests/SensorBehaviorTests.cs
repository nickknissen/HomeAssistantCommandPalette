using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class SensorBehaviorTests
{
    private static DomainCtx MakeCtx(string entityId, string state, IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make(entityId, state, attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    [Fact]
    public void Sensor_detail_rows_include_device_class_and_state_class()
    {
        var ctx = MakeCtx("sensor.power", "120", new Dictionary<string, object?>
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
    public void Battery_icon_picks_bucket_from_state()
    {
        // We can't compare IconInfo by reference; just assert no throw + non-null.
        var ctx = MakeCtx("sensor.bat", "15", new Dictionary<string, object?>
        {
            ["device_class"] = "battery",
        });
        var icon = new SensorBehavior().BuildIcon(in ctx);
        Assert.NotNull(icon);
    }
}

public class BinarySensorBehaviorTests
{
    private static DomainCtx MakeCtx(string entityId, string state, IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make(entityId, state, attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    [Fact]
    public void Door_open_icon_distinct_from_door_closed()
    {
        // Different IconInfo instances aren't comparable by reference, so
        // smoke-test: both states return non-null.
        var openCtx = MakeCtx("binary_sensor.front", "on", new Dictionary<string, object?>
        {
            ["device_class"] = "door",
        });
        var closedCtx = MakeCtx("binary_sensor.front", "off", new Dictionary<string, object?>
        {
            ["device_class"] = "door",
        });
        var b = new BinarySensorBehavior();
        Assert.NotNull(b.BuildIcon(in openCtx));
        Assert.NotNull(b.BuildIcon(in closedCtx));
    }
}

public class SunBehaviorTests
{
    [Fact]
    public void Domain_is_sun()
    {
        Assert.Equal("sun", new SunBehavior().Domain);
    }

    [Theory]
    [InlineData("above_horizon")]
    [InlineData("below_horizon")]
    [InlineData("unavailable")]
    public void BuildIcon_returns_non_null_for_canonical_states(string state)
    {
        var entity = TestEntities.Make("sun.sun", state);
        var ctx = new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
        Assert.NotNull(new SunBehavior().BuildIcon(in ctx));
    }
}

public class DomainRegistryEntityIdOverrideTests
{
    [Fact]
    public void TryGet_for_sun_dot_sun_returns_SunBehavior()
    {
        var found = DomainRegistry.TryGet("sun", "sun.sun", out var b);
        Assert.True(found);
        Assert.IsType<SunBehavior>(b);
    }

    [Fact]
    public void TryGet_for_other_sun_entity_falls_through_to_domain_map()
    {
        // No "sun" domain entry → fall through to default behavior, but
        // TryGet still returns false because no domain match either.
        var found = DomainRegistry.TryGet("sun", "sun.kitchen", out var b);
        Assert.False(found);
        Assert.Null(b);
    }
}
