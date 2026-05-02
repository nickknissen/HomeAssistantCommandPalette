using System.Collections.Generic;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class InputSelectBehaviorTests
{
    private static DomainCtx MakeCtx(string state, List<object?>? options)
    {
        var attrs = new Dictionary<string, object?>();
        if (options is not null) attrs["options"] = options;
        var entity = TestEntities.Make("input_select.mode", state, attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    [Fact]
    public void Submenu_excludes_current_state()
    {
        var ctx = MakeCtx("home", new List<object?> { "home", "away", "vacation" });
        var items = new List<IContextItem>();
        new InputSelectBehavior().AddContextItems(in ctx, items);
        var submenu = Assert.IsType<CommandContextItem>(Assert.Single(items));
        Assert.NotNull(submenu.MoreCommands);
        Assert.Equal(2, submenu.MoreCommands!.Length);
    }

    [Fact]
    public void Unavailable_emits_no_context_items()
    {
        var ctx = MakeCtx("unavailable", new List<object?> { "a", "b" });
        var items = new List<IContextItem>();
        new InputSelectBehavior().AddContextItems(in ctx, items);
        Assert.Empty(items);
    }

    [Fact]
    public void No_options_emits_no_context_items()
    {
        var ctx = MakeCtx("home", options: null);
        var items = new List<IContextItem>();
        new InputSelectBehavior().AddContextItems(in ctx, items);
        Assert.Empty(items);
    }
}
