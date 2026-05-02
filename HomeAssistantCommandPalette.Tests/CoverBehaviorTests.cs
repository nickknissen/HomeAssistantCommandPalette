using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class CoverBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state = "open",
        IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("cover.living_room", state, friendlyName: "Living Room", attributes: attrs);
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Primary_calls_toggle()
    {
        var (ctx, client) = MakeCtx();
        ((InvokableCommand)new CoverBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal(("cover", "toggle"), (client.Calls[0].Domain, client.Calls[0].Service));
    }

    [Fact]
    public void Always_includes_open_close_stop()
    {
        var (ctx, client) = MakeCtx();
        var items = new List<IContextItem>();
        new CoverBehavior().AddContextItems(in ctx, items);
        // First three are open/close/stop; supported_features missing →
        // both submenus also surface optimistically.
        var first3 = items.Take(3).Select(i =>
            ((InvokableCommand)((CommandContextItem)i).Command!)).ToArray();
        foreach (var c in first3) c.Invoke();
        Assert.Equal(
            new[] { "open_cover", "close_cover", "stop_cover" },
            client.Calls.Select(c => c.Service).ToArray());
    }

    [Fact]
    public void Position_submenu_only_when_set_position_supported()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            // OPEN + CLOSE + STOP, but no SET_POSITION (4) or SET_TILT (128).
            ["supported_features"] = 1L | 2L | 8L,
        });
        var items = new List<IContextItem>();
        new CoverBehavior().AddContextItems(in ctx, items);
        // Only open/close/stop; no submenus.
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void Tilt_submenu_only_when_set_tilt_position_supported()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 4L | 128L,  // SET_POSITION + SET_TILT_POSITION
        });
        var items = new List<IContextItem>();
        new CoverBehavior().AddContextItems(in ctx, items);
        // open/close/stop + position submenu + tilt submenu = 5
        Assert.Equal(5, items.Count);
        var positionSub = (CommandContextItem)items[3];
        var tiltSub = (CommandContextItem)items[4];
        Assert.Equal("Set position…", positionSub.Title);
        Assert.Equal("Set tilt…", tiltSub.Title);
        Assert.Equal(5, positionSub.MoreCommands!.Length);
        Assert.Equal(5, tiltSub.MoreCommands!.Length);
    }

    [Fact]
    public void Position_preset_passes_position_extra_data()
    {
        var (ctx, client) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 4L,
        });
        var items = new List<IContextItem>();
        new CoverBehavior().AddContextItems(in ctx, items);
        var positionSub = (CommandContextItem)items[3];
        ((InvokableCommand)((CommandContextItem)positionSub.MoreCommands![2]).Command!).Invoke();
        // 50% — third preset.
        Assert.Equal("set_cover_position", client.Calls[0].Service);
        Assert.Equal(50, client.Calls[0].ExtraData!["position"]);
    }
}
