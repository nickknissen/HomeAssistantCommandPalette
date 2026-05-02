using System.Collections.Generic;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class VacuumBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state = "docked",
        IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("vacuum.roomba", state, friendlyName: "Roomba", attributes: attrs);
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Primary_pauses_when_cleaning()
    {
        var (ctx, client) = MakeCtx(state: "cleaning");
        ((InvokableCommand)new VacuumBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal("pause", client.Calls[0].Service);
    }

    [Fact]
    public void Primary_starts_when_not_cleaning()
    {
        var (ctx, client) = MakeCtx(state: "docked");
        ((InvokableCommand)new VacuumBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal("start", client.Calls[0].Service);
    }

    [Fact]
    public void Context_items_gated_by_supported_features()
    {
        // Only START (8192) and STOP (8) supported.
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 8192L | 8L,
        });
        var items = new List<IContextItem>();
        new VacuumBehavior().AddContextItems(in ctx, items);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Context_items_optimistic_when_supported_features_missing()
    {
        // No supported_features attribute → all six base actions surface.
        var (ctx, _) = MakeCtx();
        var items = new List<IContextItem>();
        new VacuumBehavior().AddContextItems(in ctx, items);
        Assert.Equal(6, items.Count);
    }

    [Fact]
    public void Fan_speed_submenu_appears_when_supported_and_list_present()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 32L,  // FAN_SPEED only
            ["fan_speed_list"] = new List<object?> { "min", "med", "max" },
        });
        var items = new List<IContextItem>();
        new VacuumBehavior().AddContextItems(in ctx, items);

        // Submenu is the only thing emitted — base actions are gated out.
        var submenu = Assert.IsType<CommandContextItem>(Assert.Single(items));
        Assert.Equal("Set fan speed…", submenu.Title);
        Assert.NotNull(submenu.MoreCommands);
        Assert.Equal(3, submenu.MoreCommands!.Length);
    }
}
