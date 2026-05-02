using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class FanBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state = "on",
        IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("fan.bedroom", state, friendlyName: "Bedroom", attributes: attrs);
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Primary_calls_toggle()
    {
        var (ctx, client) = MakeCtx();
        ((InvokableCommand)new FanBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal(("fan", "toggle"), (client.Calls[0].Domain, client.Calls[0].Service));
    }

    [Fact]
    public void Set_speed_off_omits_speed_actions_and_submenu()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 0L,  // explicit zero — SET_SPEED off
        });
        var items = new List<IContextItem>();
        new FanBehavior().AddContextItems(in ctx, items);
        // Just turn_on / turn_off.
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Set_speed_supported_emits_presets_submenu()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 1L,  // SET_SPEED
        });
        var items = new List<IContextItem>();
        new FanBehavior().AddContextItems(in ctx, items);
        // turn_on, turn_off, presets submenu.
        Assert.Equal(3, items.Count);
        var sub = (CommandContextItem)items[2];
        Assert.Equal("Set speed…", sub.Title);
        Assert.Equal(4, sub.MoreCommands!.Length);
    }

    [Fact]
    public void Speed_up_and_down_emitted_when_step_present()
    {
        var (ctx, client) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 1L,
            ["percentage"] = 50L,
            ["percentage_step"] = 25L,
        });
        var items = new List<IContextItem>();
        new FanBehavior().AddContextItems(in ctx, items);
        // turn_on, turn_off, speed up, speed down, presets submenu = 5.
        Assert.Equal(5, items.Count);
        ((InvokableCommand)((CommandContextItem)items[2]).Command!).Invoke();
        Assert.Equal(75, client.Calls[0].ExtraData!["percentage"]);
    }

    [Fact]
    public void Speed_up_clamped_at_100()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 1L,
            ["percentage"] = 90L,
            ["percentage_step"] = 25L,  // 90 + 25 = 115 → drop the up action
        });
        var items = new List<IContextItem>();
        new FanBehavior().AddContextItems(in ctx, items);
        // turn_on, turn_off, speed down, presets — no speed-up.
        var hasSpeedUp = items
            .OfType<CommandContextItem>()
            .Any(i => i.Command?.Name?.StartsWith("Speed up", System.StringComparison.Ordinal) == true);
        Assert.False(hasSpeedUp);
    }
}
