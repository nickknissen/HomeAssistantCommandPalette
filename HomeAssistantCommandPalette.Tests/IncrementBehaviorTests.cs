using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class IncrementBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx()
    {
        var entity = TestEntities.Make("counter.coffee", state: "5", friendlyName: "Coffee");
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void BuildPrimary_calls_increment()
    {
        var b = Domains.Increment("counter");
        var (ctx, client) = MakeCtx();

        ((InvokableCommand)b.BuildPrimary(in ctx)).Invoke();

        Assert.Equal("counter", client.Calls[0].Domain);
        Assert.Equal("increment", client.Calls[0].Service);
    }

    [Fact]
    public void Default_AddContextItems_emits_increment_decrement_reset()
    {
        var b = Domains.Increment("counter");
        var (ctx, client) = MakeCtx();

        var items = new List<IContextItem>();
        b.AddContextItems(in ctx, items);

        Assert.Equal(3, items.Count);
        InvokeAll(items, client);
        Assert.Equal(
            new[] { "increment", "decrement", "reset" },
            client.Calls.Select(c => c.Service).ToArray());
    }

    [Fact]
    public void Disabling_decrement_drops_that_item_only()
    {
        var b = Domains.Increment("counter", addDecrement: false, addReset: true);
        var (ctx, client) = MakeCtx();

        var items = new List<IContextItem>();
        b.AddContextItems(in ctx, items);
        InvokeAll(items, client);

        Assert.Equal(
            new[] { "increment", "reset" },
            client.Calls.Select(c => c.Service).ToArray());
    }

    [Fact]
    public void Disabling_both_extras_leaves_only_increment_in_context()
    {
        var b = Domains.Increment("counter", addDecrement: false, addReset: false);
        var (ctx, _) = MakeCtx();

        var items = new List<IContextItem>();
        b.AddContextItems(in ctx, items);

        Assert.Single(items);
    }

    private static void InvokeAll(List<IContextItem> items, RecordingHaClient _)
    {
        foreach (var item in items)
        {
            ((InvokableCommand)((CommandContextItem)item).Command!).Invoke();
        }
    }
}
