using System.Collections.Generic;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class ActivateBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(string entityId)
    {
        var entity = TestEntities.Make(entityId, state: "off", friendlyName: "Movie Time");
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void BuildPrimary_for_scene_calls_turn_on()
    {
        var b = Domains.Activate("scene", "turn_on", "Activate");
        var (ctx, client) = MakeCtx("scene.movie_time");

        ((InvokableCommand)b.BuildPrimary(in ctx)).Invoke();

        Assert.Single(client.Calls);
        Assert.Equal("scene", client.Calls[0].Domain);
        Assert.Equal("turn_on", client.Calls[0].Service);
        Assert.Equal("scene.movie_time", client.Calls[0].EntityId);
    }

    [Fact]
    public void BuildPrimary_for_button_calls_press()
    {
        var b = Domains.Activate("button", "press", "Press");
        var (ctx, client) = MakeCtx("button.doorbell");

        ((InvokableCommand)b.BuildPrimary(in ctx)).Invoke();

        Assert.Equal("press", client.Calls[0].Service);
    }

    [Fact]
    public void AddContextItems_emits_no_items_by_default()
    {
        var b = Domains.Activate("scene", "turn_on", "Activate");
        var (ctx, _) = MakeCtx("scene.x");

        var items = new List<IContextItem>();
        b.AddContextItems(in ctx, items);

        Assert.Empty(items);
    }
}
