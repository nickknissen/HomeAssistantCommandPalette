using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class UpdateBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state, IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("update.core", state, friendlyName: "Core",
            attributes: attrs);
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Up_to_date_emits_no_context_items()
    {
        var (ctx, _) = MakeCtx("off");
        var items = new List<IContextItem>();
        new UpdateBehavior().AddContextItems(in ctx, items);
        Assert.Empty(items);
    }

    [Fact]
    public void Available_emits_install_and_skip_when_install_supported()
    {
        var (ctx, _) = MakeCtx("on", new Dictionary<string, object?>
        {
            ["supported_features"] = 1L,  // INSTALL only
        });
        var items = new List<IContextItem>();
        new UpdateBehavior().AddContextItems(in ctx, items);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Install_passes_backup_when_backup_supported()
    {
        var (ctx, client) = MakeCtx("on", new Dictionary<string, object?>
        {
            ["supported_features"] = 1L | 8L,  // INSTALL + BACKUP
        });
        var items = new List<IContextItem>();
        new UpdateBehavior().AddContextItems(in ctx, items);

        ((InvokableCommand)((CommandContextItem)items[0]).Command!).Invoke();
        Assert.Equal("install", client.Calls[0].Service);
        Assert.NotNull(client.Calls[0].ExtraData);
        Assert.True((bool)client.Calls[0].ExtraData!["backup"]!);
    }

    [Fact]
    public void In_progress_install_hides_install_action()
    {
        var (ctx, _) = MakeCtx("on", new Dictionary<string, object?>
        {
            ["supported_features"] = 1L,
            ["in_progress"] = 50L,  // 50%
        });
        var items = new List<IContextItem>();
        new UpdateBehavior().AddContextItems(in ctx, items);

        // Skip remains; Install is hidden.
        Assert.Single(items);
    }

    [Fact]
    public void Release_url_appends_open_release_notes()
    {
        var (ctx, _) = MakeCtx("on", new Dictionary<string, object?>
        {
            ["supported_features"] = 1L,
            ["release_url"] = "https://example.com/release",
        });
        var items = new List<IContextItem>();
        new UpdateBehavior().AddContextItems(in ctx, items);
        Assert.Equal(3, items.Count);
        Assert.Equal("Open release notes", ((CommandContextItem)items.Last()).Command?.Name);
    }
}
