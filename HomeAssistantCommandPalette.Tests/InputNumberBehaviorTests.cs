using System.Collections.Generic;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class InputNumberBehaviorTests
{
    private static DomainCtx MakeCtx(string state, IReadOnlyDictionary<string, object?>? attrs)
    {
        var entity = TestEntities.Make("input_number.target", state, attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    [Fact]
    public void Increment_gated_off_when_at_max()
    {
        var ctx = MakeCtx("100", new Dictionary<string, object?>
        {
            ["min"] = 0L,
            ["max"] = 100L,
            ["step"] = 1L,
        });
        var items = new List<IContextItem>();
        new InputNumberBehavior().AddContextItems(in ctx, items);
        // Only Decrease.
        Assert.Single(items);
    }

    [Fact]
    public void Decrement_gated_off_when_at_min()
    {
        var ctx = MakeCtx("0", new Dictionary<string, object?>
        {
            ["min"] = 0L,
            ["max"] = 100L,
            ["step"] = 1L,
        });
        var items = new List<IContextItem>();
        new InputNumberBehavior().AddContextItems(in ctx, items);
        Assert.Single(items);
    }

    [Fact]
    public void Both_emitted_in_middle_of_range()
    {
        var ctx = MakeCtx("50", new Dictionary<string, object?>
        {
            ["min"] = 0L,
            ["max"] = 100L,
            ["step"] = 1L,
        });
        var items = new List<IContextItem>();
        new InputNumberBehavior().AddContextItems(in ctx, items);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void Missing_step_emits_no_items()
    {
        var ctx = MakeCtx("50", new Dictionary<string, object?>
        {
            ["min"] = 0L,
            ["max"] = 100L,
            // step deliberately omitted
        });
        var items = new List<IContextItem>();
        new InputNumberBehavior().AddContextItems(in ctx, items);
        Assert.Empty(items);
    }
}
