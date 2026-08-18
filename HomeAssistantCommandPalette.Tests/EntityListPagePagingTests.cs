using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;

namespace HomeAssistantCommandPalette.Tests;

/// <summary>
/// Every item handed to CmdPal costs ~19 cross-process property reads
/// plus a PropChanged subscription, and a fresh navigation starts with an
/// empty ViewModel cache. So the page must hand over a bounded number of
/// items, and must hand back the *same* instances across renders — the
/// host's ViewModel cache is keyed on item reference identity.
/// </summary>
public sealed class EntityListPagePagingTests
{
    private static RecordingHaClient ClientWith(int entityCount)
    {
        var client = new RecordingHaClient();
        for (var i = 0; i < entityCount; i++)
        {
            client.Entities.Add(TestEntities.Make(
                $"light.lamp_{i:D4}",
                state: "on",
                friendlyName: $"Lamp {i:D4}"));
        }
        return client;
    }

    private static EntityListPage PageFor(RecordingHaClient client)
        => new(new HaSettings(), client, new StubIconResolver(), title: "All Entities", id: "ha.entities");

    [Fact]
    public void first_render_hands_over_at_most_one_page()
    {
        var client = ClientWith(950);

        var items = PageFor(client).GetItems();

        Assert.Equal(100, items.Length);
    }

    [Fact]
    public void load_more_extends_the_page_and_flags_when_exhausted()
    {
        var client = ClientWith(250);
        var page = PageFor(client);

        Assert.Equal(100, page.GetItems().Length);
        Assert.True(page.HasMoreItems);

        page.LoadMore();
        Assert.Equal(200, page.GetItems().Length);
        Assert.True(page.HasMoreItems);

        page.LoadMore();
        Assert.Equal(250, page.GetItems().Length);
        Assert.False(page.HasMoreItems);
    }

    [Fact]
    public void small_page_reports_no_more_items()
    {
        var client = ClientWith(12);
        var page = PageFor(client);

        Assert.Equal(12, page.GetItems().Length);
        Assert.False(page.HasMoreItems);
    }

    [Fact]
    public void unchanged_entities_keep_their_item_instance_across_renders()
    {
        var client = ClientWith(20);
        var page = PageFor(client);

        var first = page.GetItems();
        var second = page.GetItems();

        Assert.Equal(first.Length, second.Length);
        for (var i = 0; i < first.Length; i++)
        {
            Assert.Same(first[i], second[i]);
        }
    }

    [Fact]
    public void a_changed_entity_keeps_its_instance_but_updates_in_place()
    {
        var client = ClientWith(3);
        var page = PageFor(client);
        var before = page.GetItems();
        var target = before.Single(i => i.Title == "Lamp 0001");

        client.Entities[1] = TestEntities.Make("light.lamp_0001", state: "off", friendlyName: "Lamp renamed");

        var after = page.GetItems();

        var updated = after.Single(i => i.Title == "Lamp renamed");
        Assert.Same(target, updated);
        Assert.Contains(updated.Tags, t => t.Text == "OFF");
    }

    [Fact]
    public void search_text_filters_across_every_entity_not_just_the_first_page()
    {
        var client = ClientWith(950);
        var page = PageFor(client);
        _ = page.GetItems();

        // Lamp 0900 sorts past the first page, so it can only show up if
        // the filter ran over the whole snapshot.
        page.SearchText = "Lamp 0900";

        var items = page.GetItems();
        Assert.Contains(items, i => i.Title == "Lamp 0900");
    }

    [Fact]
    public void searching_resets_paging_to_the_first_page()
    {
        var client = ClientWith(950);
        var page = PageFor(client);
        page.LoadMore();
        Assert.Equal(200, page.GetItems().Length);

        page.SearchText = "Lamp";

        Assert.Equal(100, page.GetItems().Length);
    }

    [Fact]
    public void unfiltered_all_entities_lists_most_recently_changed_first()
    {
        var client = new RecordingHaClient();
        client.Entities.Add(Aged("light.oldest", "Oldest", TimeSpan.FromHours(5)));
        client.Entities.Add(Aged("light.newest", "Newest", TimeSpan.FromMinutes(1)));
        client.Entities.Add(Aged("light.middle", "Middle", TimeSpan.FromHours(1)));

        var items = PageFor(client).GetItems();

        Assert.Equal(["Newest", "Middle", "Oldest"], items.Select(i => i.Title));
    }

    private static HaEntity Aged(string entityId, string name, TimeSpan age)
    {
        var entity = TestEntities.Make(entityId, state: "on", friendlyName: name);
        return new HaEntity
        {
            EntityId = entity.EntityId,
            State = entity.State,
            Attributes = entity.Attributes,
            LastChanged = DateTimeOffset.UtcNow - age,
            LastUpdated = DateTimeOffset.UtcNow - age,
        };
    }
}
