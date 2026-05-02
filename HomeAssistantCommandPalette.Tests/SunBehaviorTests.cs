using HomeAssistantCommandPalette;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;
using HomeAssistantCommandPalette.Tests.Fakes;

namespace HomeAssistantCommandPalette.Tests;

public class SunBehaviorTests
{
    private static HaEntity MakeEntity(string state) => TestEntities.Make("sun.sun", state);

    [Fact]
    public void Above_horizon_uses_sun_day_icon()
    {
        IconAssert.Same(Icons.SunDay, new SunIconRule().Pick(MakeEntity("above_horizon")));
    }

    [Fact]
    public void Below_horizon_uses_sun_night_icon()
    {
        IconAssert.Same(Icons.SunNight, new SunIconRule().Pick(MakeEntity("below_horizon")));
    }

    [Fact]
    public void Unavailable_uses_sun_unavailable_icon()
    {
        IconAssert.Same(Icons.SunUnavailable, new SunIconRule().Pick(MakeEntity("unavailable")));
    }

    [Fact]
    public void Domain_is_sun()
    {
        Assert.Equal("sun", new SunBehavior().Domain);
    }

    [Fact]
    public void Registry_for_sun_dot_sun_returns_SunBehavior()
    {
        var found = DomainRegistry.TryGet("sun", "sun.sun", out var b);
        Assert.True(found);
        Assert.IsType<SunBehavior>(b);
    }

    [Fact]
    public void Registry_for_other_sun_entity_id_falls_through()
    {
        // No 'sun' domain entry → no match.
        var found = DomainRegistry.TryGet("sun", "sun.kitchen", out var b);
        Assert.False(found);
        Assert.Null(b);
    }
}
