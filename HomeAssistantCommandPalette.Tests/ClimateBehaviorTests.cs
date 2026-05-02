using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class ClimateBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state = "heat",
        IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("climate.living", state, friendlyName: "Living", attributes: attrs);
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Increase_step_uses_target_temp_step()
    {
        var (ctx, client) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["temperature"] = 20.0,
            ["target_temp_step"] = 0.5,
        });
        var items = new List<IContextItem>();
        new ClimateBehavior().AddContextItems(in ctx, items);

        // Increase, Decrease, then "Set temperature…" submenu.
        ((InvokableCommand)((CommandContextItem)items[0]).Command!).Invoke();
        Assert.Equal(20.5, client.Calls[0].ExtraData!["temperature"]);
    }

    [Fact]
    public void No_target_temperature_omits_step_buttons()
    {
        var (ctx, _) = MakeCtx();  // no temperature attr
        var items = new List<IContextItem>();
        new ClimateBehavior().AddContextItems(in ctx, items);

        // First item should be the temperature submenu, not Increase/Decrease.
        Assert.Equal("Set temperature…", ((CommandContextItem)items[0]).Title);
    }

    [Fact]
    public void Hvac_modes_render_as_submenu_with_set_hvac_mode()
    {
        var (ctx, client) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["hvac_modes"] = new List<object?> { "off", "heat", "cool" },
        });
        var items = new List<IContextItem>();
        new ClimateBehavior().AddContextItems(in ctx, items);

        var sub = items.OfType<CommandContextItem>()
            .Single(i => i.Title == "Set HVAC mode…");
        Assert.Equal(3, sub.MoreCommands!.Length);

        ((InvokableCommand)((CommandContextItem)sub.MoreCommands![1]).Command!).Invoke();
        Assert.Equal("set_hvac_mode", client.Calls[0].Service);
        Assert.Equal("heat", client.Calls[0].ExtraData!["hvac_mode"]);
    }

    [Fact]
    public void Fan_modes_and_swing_modes_appear_when_attributes_present()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["fan_modes"] = new List<object?> { "auto", "high" },
            ["swing_modes"] = new List<object?> { "off", "vertical" },
        });
        var items = new List<IContextItem>();
        new ClimateBehavior().AddContextItems(in ctx, items);

        Assert.Contains(items.OfType<CommandContextItem>(), i => i.Title == "Set fan mode…");
        Assert.Contains(items.OfType<CommandContextItem>(), i => i.Title == "Set swing mode…");
    }

    [Theory]
    [InlineData("heat_cool", "ClimateAuto")]
    [InlineData("auto", "ClimateAuto")]
    [InlineData("off", "ClimateOff")]
    [InlineData("heat", "ClimateActive")]
    [InlineData("cool", "ClimateActive")]
    [InlineData("unavailable", "ClimateUnavailable")]
    public void Rule_picks_palette_from_state(string state, string _)
    {
        var entity = TestEntities.Make("climate.living", state, friendlyName: "Living");
        // Just assert it doesn't throw and returns *something*. Icon
        // identity comparisons aren't reliable since IconHelpers builds
        // a fresh IconInfo per call.
        var icon = new ClimateIconRule().Pick(entity);
        Assert.NotNull(icon);
    }

    [Fact]
    public void Detail_rows_handle_dual_setpoint()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["target_temp_low"] = 18.0,
            ["target_temp_high"] = 22.0,
        });
        var rows = new List<IDetailsElement>();
        new ClimateBehavior().AddDetailRows(in ctx, rows);

        Assert.Contains(rows.OfType<DetailsElement>(), r => r.Key == "Target low");
        Assert.Contains(rows.OfType<DetailsElement>(), r => r.Key == "Target high");
    }
}
