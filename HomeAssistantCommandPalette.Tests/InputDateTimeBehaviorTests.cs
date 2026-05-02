using System.Collections.Generic;
using HomeAssistantCommandPalette;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline.Rules;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class InputDateTimeBehaviorTests
{
    private static HaEntity MakeEntity(IReadOnlyDictionary<string, object?>? attrs = null)
        => TestEntities.Make("input_datetime.alarm", "2026-01-01", attributes: attrs);

    [Theory]
    [InlineData(true,  false, IconKind.InputDate)]      // calendar only
    [InlineData(false, true,  IconKind.InputTime)]      // clock only
    [InlineData(true,  true,  IconKind.InputDateTime)]  // both → composite
    [InlineData(false, false, IconKind.InputDateTime)]  // neither → composite default
    public void Rule_dispatches_on_has_date_and_has_time(bool hasDate, bool hasTime, IconKind expected)
    {
        var entity = MakeEntity(new Dictionary<string, object?>
        {
            ["has_date"] = hasDate,
            ["has_time"] = hasTime,
        });

        var icon = new InputDateTimeIconRule().Pick(entity);

        IconAssert.Same(Resolve(expected), icon);
    }

    [Fact]
    public void Missing_attributes_default_to_composite_icon()
    {
        // Both attributes absent (not just false). The rule treats
        // missing the same as false.
        IconAssert.Same(Icons.InputDateTime, new InputDateTimeIconRule().Pick(MakeEntity()));
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
