using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class LightBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state = "on",
        IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("light.kitchen", state, friendlyName: "Kitchen", attributes: attrs);
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Primary_calls_toggle()
    {
        var (ctx, client) = MakeCtx();
        ((InvokableCommand)new LightBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal(("light", "toggle"), (client.Calls[0].Domain, client.Calls[0].Service));
    }

    [Fact]
    public void Brightness_submenu_always_present()
    {
        var (ctx, _) = MakeCtx();
        var items = new List<IContextItem>();
        new LightBehavior().AddContextItems(in ctx, items);
        // turn_on, turn_off, brightness submenu (no color modes set).
        Assert.Equal(3, items.Count);
        var sub = (CommandContextItem)items[2];
        Assert.Equal("Set brightness…", sub.Title);
        Assert.Equal(4, sub.MoreCommands!.Length);
    }

    [Fact]
    public void Brightness_preset_passes_brightness_pct_extra_data()
    {
        var (ctx, client) = MakeCtx();
        var items = new List<IContextItem>();
        new LightBehavior().AddContextItems(in ctx, items);
        var sub = (CommandContextItem)items[2];
        ((InvokableCommand)((CommandContextItem)sub.MoreCommands![1]).Command!).Invoke();
        Assert.Equal(50, client.Calls[0].ExtraData!["brightness_pct"]);
    }

    [Fact]
    public void Color_submenu_only_when_supported_color_modes_includes_rgb()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_color_modes"] = new List<object?> { "rgb", "color_temp" },
        });
        var items = new List<IContextItem>();
        new LightBehavior().AddContextItems(in ctx, items);
        // turn_on, turn_off, brightness submenu, color submenu.
        Assert.Equal(4, items.Count);
        var colorSub = (CommandContextItem)items[3];
        Assert.Equal("Set color…", colorSub.Title);
        Assert.Equal(9, colorSub.MoreCommands!.Length);
    }

    [Fact]
    public void Color_submenu_skipped_for_brightness_only_bulbs()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_color_modes"] = new List<object?> { "brightness" },
        });
        var items = new List<IContextItem>();
        new LightBehavior().AddContextItems(in ctx, items);
        Assert.Equal(3, items.Count);  // brightness submenu only, no color submenu
    }

    [Theory]
    [InlineData(255, 100)]
    [InlineData(128, 50)]
    [InlineData(64, 25)]
    public void Brightness_row_renders_percent_from_0_to_255(long raw, int expectedPct)
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?> { ["brightness"] = raw });
        var rows = new List<IDetailsElement>();
        new LightBehavior().AddDetailRows(in ctx, rows);

        var row = rows.OfType<DetailsElement>().Single(r => r.Key == "Brightness");
        Assert.Equal($"{expectedPct}%", ((DetailsLink)row.Data!).Text);
    }

    [Fact]
    public void Mireds_fallback_when_kelvin_attributes_missing()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["min_mireds"] = 154L,  // → 1_000_000 / 154 ≈ 6493K (max)
            ["max_mireds"] = 500L,  // → 1_000_000 / 500  =   2000K (min)
        });
        var rows = new List<IDetailsElement>();
        new LightBehavior().AddDetailRows(in ctx, rows);

        var row = rows.OfType<DetailsElement>().Single(r => r.Key == "Color temp range");
        Assert.Equal("2000K – 6493K", ((DetailsLink)row.Data!).Text);
    }
}
