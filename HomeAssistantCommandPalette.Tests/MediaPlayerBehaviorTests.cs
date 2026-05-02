using System.Collections.Generic;
using System.Linq;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class MediaPlayerBehaviorTests
{
    private static (DomainCtx ctx, RecordingHaClient client) MakeCtx(
        string state = "playing",
        IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("media_player.living", state, friendlyName: "Living", attributes: attrs);
        var client = new RecordingHaClient();
        var ctx = new DomainCtx(entity, client, new HaSettings(), OnSuccess: () => { });
        return (ctx, client);
    }

    [Fact]
    public void Primary_calls_toggle()
    {
        var (ctx, client) = MakeCtx();
        ((InvokableCommand)new MediaPlayerBehavior().BuildPrimary(in ctx)).Invoke();
        Assert.Equal(("media_player", "toggle"), (client.Calls[0].Domain, client.Calls[0].Service));
    }

    [Fact]
    public void Volume_preset_converts_percent_to_volume_level()
    {
        var (ctx, client) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 4L,  // VOLUME_SET
        });
        var items = new List<IContextItem>();
        new MediaPlayerBehavior().AddContextItems(in ctx, items);
        var sub = items.OfType<CommandContextItem>().Single(i => i.Title == "Set volume…");
        // 50% preset is index 1.
        ((InvokableCommand)((CommandContextItem)sub.MoreCommands![1]).Command!).Invoke();
        Assert.Equal(0.5, (double)client.Calls[0].ExtraData!["volume_level"]!);
    }

    [Fact]
    public void Volume_submenu_omitted_without_volume_set_bit()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 0L,
        });
        var items = new List<IContextItem>();
        new MediaPlayerBehavior().AddContextItems(in ctx, items);
        Assert.DoesNotContain(items.OfType<CommandContextItem>(), i => i.Title == "Set volume…");
    }

    [Fact]
    public void Shuffle_toggle_only_when_attribute_present()
    {
        // SHUFFLE_SET=32768 set, but no shuffle attr → no shuffle item.
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 32768L,
        });
        var items = new List<IContextItem>();
        new MediaPlayerBehavior().AddContextItems(in ctx, items);
        var labels = items.OfType<CommandContextItem>().Select(i => i.Command?.Name ?? i.Title).ToArray();
        Assert.DoesNotContain(labels, l => l != null && l.Contains("shuffle", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Shuffle_toggle_flips_current_value()
    {
        var (ctx, client) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 32768L,
            ["shuffle"] = true,
        });
        var items = new List<IContextItem>();
        new MediaPlayerBehavior().AddContextItems(in ctx, items);
        var shuffle = items.OfType<CommandContextItem>()
            .Single(i => i.Command?.Name?.StartsWith("Disable shuffle", System.StringComparison.Ordinal) == true);
        ((InvokableCommand)shuffle.Command!).Invoke();
        Assert.Equal(false, client.Calls[0].ExtraData!["shuffle"]);
    }

    [Fact]
    public void Mute_toggle_emitted_only_when_attribute_present()
    {
        var (ctx, client) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["is_volume_muted"] = false,
        });
        var items = new List<IContextItem>();
        new MediaPlayerBehavior().AddContextItems(in ctx, items);
        var mute = items.OfType<CommandContextItem>()
            .Single(i => i.Command?.Name?.StartsWith("Mute", System.StringComparison.Ordinal) == true);
        ((InvokableCommand)mute.Command!).Invoke();
        Assert.Equal(true, client.Calls[0].ExtraData!["is_volume_muted"]);
    }

    [Fact]
    public void Repeat_submenu_emits_three_options_in_order()
    {
        var (ctx, _) = MakeCtx(attrs: new Dictionary<string, object?>
        {
            ["supported_features"] = 262144L,  // REPEAT_SET
        });
        var items = new List<IContextItem>();
        new MediaPlayerBehavior().AddContextItems(in ctx, items);
        var sub = items.OfType<CommandContextItem>().Single(i => i.Title == "Set repeat…");
        var labels = sub.MoreCommands!
            .OfType<CommandContextItem>()
            .Select(i => i.Command?.Name)
            .ToArray();
        Assert.Equal(new[] { "off", "one", "all" }, labels);
    }
}
