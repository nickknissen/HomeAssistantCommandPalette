using System.Collections.Generic;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class TimerBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state, bool editable = true)
    {
        var entity = TestEntities.Make("timer.kitchen", state, friendlyName: "Kitchen",
            attributes: new Dictionary<string, object?> { ["editable"] = editable });
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Primary_pauses_when_active()
    {
        var (ctx, client) = MakeCtx("active");
        ((InvokableCommand)new TimerBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal("pause", client.Calls[0].Service);
    }

    [Fact]
    public void Primary_starts_when_idle()
    {
        var (ctx, client) = MakeCtx("idle");
        ((InvokableCommand)new TimerBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal("start", client.Calls[0].Service);
    }

    [Fact]
    public void Editable_timers_emit_four_context_items()
    {
        var (ctx, _) = MakeCtx("idle", editable: true);
        var items = new List<IContextItem>();
        new TimerBehavior().AddContextItems(in ctx, items);
        Assert.Equal(4, items.Count);
    }

    [Fact]
    public void Non_editable_timers_emit_no_context_items()
    {
        var (ctx, _) = MakeCtx("idle", editable: false);
        var items = new List<IContextItem>();
        new TimerBehavior().AddContextItems(in ctx, items);
        Assert.Empty(items);
    }
}
