using System.Collections.Generic;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HomeAssistantCommandPalette.Tests;

public class PersonBehaviorTests
{
    private static DomainCtx MakeCtx(IReadOnlyDictionary<string, object?>? attrs = null)
    {
        var entity = TestEntities.Make("person.alice", "home", friendlyName: "Alice", attributes: attrs);
        return new DomainCtx(entity, new RecordingHaClient(), new HaSettings(), OnSuccess: () => { });
    }

    [Fact]
    public void Maps_link_emitted_when_lat_lon_present()
    {
        var ctx = MakeCtx(new Dictionary<string, object?>
        {
            ["latitude"] = 55.6761,
            ["longitude"] = 12.5683,
        });
        var items = new List<IContextItem>();
        new PersonBehavior().AddContextItems(in ctx, items);
        Assert.Single(items);
        Assert.Equal("Open in Google Maps", ((CommandContextItem)items[0]).Command?.Name);
    }

    [Fact]
    public void Copy_user_id_emitted_only_when_user_id_present()
    {
        var ctx = MakeCtx(new Dictionary<string, object?>
        {
            ["user_id"] = "abc123",
        });
        var items = new List<IContextItem>();
        new PersonBehavior().AddContextItems(in ctx, items);
        Assert.Single(items);
        Assert.Equal("Copy user ID", ((CommandContextItem)items[0]).Command?.Name);
    }

    [Fact]
    public void No_extras_means_no_context_items()
    {
        var ctx = MakeCtx();
        var items = new List<IContextItem>();
        new PersonBehavior().AddContextItems(in ctx, items);
        Assert.Empty(items);
    }

    [Fact]
    public void Detail_rows_include_location_and_gps_accuracy()
    {
        var ctx = MakeCtx(new Dictionary<string, object?>
        {
            ["latitude"] = 55.0,
            ["longitude"] = 12.0,
            ["gps_accuracy"] = 12.0,
            ["source"] = "device_tracker.alice_phone",
        });
        var rows = new List<IDetailsElement>();
        new PersonBehavior().AddDetailRows(in ctx, rows);
        Assert.Equal(3, rows.Count);
    }
}
