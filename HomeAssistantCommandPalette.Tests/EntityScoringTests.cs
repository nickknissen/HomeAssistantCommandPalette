using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages;
using HomeAssistantCommandPalette.Tests.Fakes;

namespace HomeAssistantCommandPalette.Tests;

/// <summary>
/// The page owns entity matching now, so the ranking is ours to justify.
/// </summary>
public sealed class EntityScoringTests
{
    private static int Score(string query, string entityId, string friendlyName, string? area = null)
    {
        var entity = TestEntities.Make(entityId, friendlyName: friendlyName);
        if (area is null)
        {
            return EntityListPage.ScoreEntity(query, entity);
        }

        return EntityListPage.ScoreEntity(query, new HaEntity
        {
            EntityId = entity.EntityId,
            State = entity.State,
            Attributes = entity.Attributes,
            AreaName = area,
        });
    }

    [Fact]
    public void an_exact_name_beats_a_prefix_beats_a_word_start_beats_a_substring()
    {
        var exact = Score("kitchen", "light.a", "Kitchen");
        var prefix = Score("kitchen", "light.b", "Kitchen ceiling");
        var wordStart = Score("kitchen", "light.c", "Main kitchen light");
        var substring = Score("kitchen", "light.d", "Subkitchenette lamp");

        Assert.True(exact > prefix, $"exact {exact} should beat prefix {prefix}");
        Assert.True(prefix > wordStart, $"prefix {prefix} should beat word start {wordStart}");
        Assert.True(wordStart > substring, $"word start {wordStart} should beat substring {substring}");
        Assert.True(substring > 0);
    }

    [Fact]
    public void entity_id_and_area_are_searchable_when_the_name_misses()
    {
        Assert.True(Score("bathroom", "light.bathroom_spots", "Spots") > 0);
        Assert.True(Score("garage", "light.x", "Spots", area: "Garage") > 0);
    }

    [Fact]
    public void a_name_hit_outranks_an_entity_id_hit()
    {
        var byName = Score("lamp", "light.x", "Lamp");
        var byId = Score("lamp", "light.lamp_x", "Spots");

        Assert.True(byName > byId, $"name {byName} should beat entity_id {byId}");
    }

    [Fact]
    public void every_token_has_to_match_somewhere()
    {
        // "kitchen lamp" must not match a bedroom lamp just because "lamp" hit.
        Assert.Equal(0, Score("kitchen lamp", "light.bedroom_lamp", "Bedroom lamp"));
        Assert.True(Score("kitchen lamp", "light.kitchen_lamp", "Kitchen lamp") > 0);
    }

    [Fact]
    public void matching_is_case_insensitive_and_ignores_padding()
    {
        Assert.True(Score("KITCHEN", "light.x", "Kitchen ceiling") > 0);
        Assert.True(Score("kitchen   ceiling", "light.x", "Kitchen ceiling") > 0);
    }

    [Fact]
    public void an_empty_query_keeps_everything()
    {
        Assert.True(Score("   ", "light.x", "Anything") > 0);
    }

    [Fact]
    public void a_miss_scores_zero()
    {
        Assert.Equal(0, Score("zigbee", "light.kitchen", "Kitchen ceiling"));
    }
}
