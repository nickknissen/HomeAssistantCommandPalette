using System.Collections.Generic;
using HomeAssistantCommandPalette;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class InputDateTimeBehaviorTests
{
    private static DomainCtx MakeCtx(IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("input_datetime.alarm", "2026-01-01", attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    [Theory]
    [InlineData(true,  false, IconKind.InputDate)]      // calendar only
    [InlineData(false, true,  IconKind.InputTime)]      // clock only
    [InlineData(true,  true,  IconKind.InputDateTime)]  // both → composite
    [InlineData(false, false, IconKind.InputDateTime)]  // neither → composite default
    public void BuildIcon_dispatches_on_has_date_and_has_time(bool hasDate, bool hasTime, IconKind expected)
    {
        var ctx = MakeCtx(new Dictionary<string, object?>
        {
            ["has_date"] = hasDate,
            ["has_time"] = hasTime,
        });

        var icon = new InputDateTimeBehavior().BuildIcon(in ctx);

        IconAssert.Same(Resolve(expected), icon);
    }

    [Fact]
    public void Missing_attributes_default_to_composite_icon()
    {
        // Both attributes absent (not just false). The behavior treats
        // missing the same as false.
        var ctx = MakeCtx();
        IconAssert.Same(Icons.InputDateTime, new InputDateTimeBehavior().BuildIcon(in ctx));
    }

    [Fact]
    public void Domain_is_input_datetime()
    {
        Assert.Equal("input_datetime", new InputDateTimeBehavior().Domain);
    }

    public enum IconKind { InputDate, InputTime, InputDateTime }

    private static IconInfo Resolve(IconKind kind) => kind switch
    {
        IconKind.InputDate => Icons.InputDate,
        IconKind.InputTime => Icons.InputTime,
        IconKind.InputDateTime => Icons.InputDateTime,
        _ => throw new System.NotImplementedException(),
    };
}
