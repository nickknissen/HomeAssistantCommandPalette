using System.Collections.Generic;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class ToggleBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(string entityId, string state = "on")
    {
        var entity = TestEntities.Make(entityId, state, friendlyName: "Test Switch");
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Toggle_factory_uses_supplied_domain()
    {
        var b = Domains.Toggle("switch");
        Assert.Equal("switch", b.Domain);
    }

    [Fact]
    public void BuildPrimary_invokes_toggle_service()
    {
        var b = Domains.Toggle("switch");
        var (ctx, client) = MakeCtx("switch.kitchen");

        var primary = b.BuildPrimary(in ctx);
        Assert.NotNull(primary);
        Assert.IsAssignableFrom<InvokableCommand>(primary);
        ((InvokableCommand)primary).Invoke();

        Assert.Single(client.Calls);
        var call = client.Calls[0];
        Assert.Equal("switch", call.Domain);
        Assert.Equal("toggle", call.Service);
        Assert.Equal("switch.kitchen", call.EntityId);
        Assert.Null(call.ExtraData);
    }

    [Fact]
    public void AddContextItems_adds_turn_on_and_turn_off()
    {
        var b = Domains.Toggle("switch");
        var (ctx, client) = MakeCtx("switch.kitchen");

        var items = new List<IContextItem>();
        b.AddContextItems(in ctx, items);

        Assert.Equal(2, items.Count);

        // Invoke each — sequence is turn_on, turn_off.
        Invoke(items[0]);
        Invoke(items[1]);

        Assert.Equal(2, client.Calls.Count);
        Assert.Equal(("switch", "turn_on"), (client.Calls[0].Domain, client.Calls[0].Service));
        Assert.Equal(("switch", "turn_off"), (client.Calls[1].Domain, client.Calls[1].Service));
    }

    [Fact]
    public void AddContextItems_uses_supplied_domain_for_service_calls()
    {
        // input_boolean is wired the same way as switch (PR 3) — assert
        // the factory threads its `domain` argument all the way through.
        var b = Domains.Toggle("input_boolean");
        var (ctx, client) = MakeCtx("input_boolean.guest_mode");

        var items = new List<IContextItem>();
        b.AddContextItems(in ctx, items);
        Invoke(items[0]);
        Invoke(items[1]);

        Assert.All(client.Calls, c => Assert.Equal("input_boolean", c.Domain));
    }

    [Fact]
    public void OnSuccess_fires_once_per_successful_invocation()
    {
        var b = Domains.Toggle("switch");
        var entity = TestEntities.Make("switch.kitchen", "on");
        var client = new RecordingHaClient();
        var calls = 0;
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => calls++);

        var primary = b.BuildPrimary(in ctx);
        ((InvokableCommand)primary).Invoke();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void AddDetailRows_emits_no_rows_when_no_extras_configured()
    {
        var b = Domains.Toggle("switch");
        var (ctx, _) = MakeCtx("switch.kitchen");

        var rows = new List<IDetailsElement>();
        b.AddDetailRows(in ctx, rows);

        Assert.Empty(rows);
    }

    [Fact]
    public void AddDetailRows_emits_extras_for_present_attributes_only()
    {
        // Toggle factory's variadic extras are the seam used by trivial
        // domains that surface a couple of read-only attributes (PR 3).
        var entity = TestEntities.Make(
            "switch.outlet",
            "on",
            attributes: new Dictionary<string, object?>
            {
                ["temperature"] = 21.5,
                ["humidity"] = "",  // blank → skipped
                // power omitted entirely → skipped
            });
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });

        var b = Domains.Toggle("switch",
            ("temperature", "Temperature"),
            ("humidity", "Humidity"),
            ("power", "Power"));

        var rows = new List<IDetailsElement>();
        b.AddDetailRows(in ctx, rows);

        Assert.Single(rows);
    }

    private static void Invoke(IContextItem item)
    {
        var cmd = ((CommandContextItem)item).Command;
        ((InvokableCommand)cmd!).Invoke();
    }
}
