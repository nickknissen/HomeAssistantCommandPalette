using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class AutomationBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(string state = "on")
    {
        var entity = TestEntities.Make("automation.morning", state, friendlyName: "Morning Routine");
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Domain_is_automation()
    {
        Assert.Equal("automation", new AutomationBehavior().Domain);
    }

    [Fact]
    public void BuildPrimary_calls_toggle()
    {
        var (ctx, client) = MakeCtx();
        ((InvokableCommand)new AutomationBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal(("automation", "toggle"), (client.Calls[0].Domain, client.Calls[0].Service));
    }

    [Fact]
    public void Context_items_include_turn_on_turn_off_and_trigger()
    {
        var (ctx, client) = MakeCtx();
        var items = new List<IContextItem>();
        new AutomationBehavior().AddContextItems(in ctx, items);
        Assert.Equal(3, items.Count);

        foreach (var item in items)
        {
            ((InvokableCommand)((CommandContextItem)item).Command!).Invoke();
        }
        Assert.Equal(
            new[] { "turn_on", "turn_off", "trigger" },
            client.Calls.Select(c => c.Service).ToArray());
    }

    [Fact]
    public void AddDetailRows_only_emits_present_attributes()
    {
        var entity = TestEntities.Make("automation.x", "on", attributes: new Dictionary<string, object?>
        {
            ["last_triggered"] = "2026-01-01T08:00:00Z",
            ["mode"] = "single",
            ["current"] = 0L,
            // id deliberately omitted
        });
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });

        var rows = new List<IDetailsElement>();
        new AutomationBehavior().AddDetailRows(in ctx, rows);

        Assert.Equal(3, rows.Count);
    }
}
