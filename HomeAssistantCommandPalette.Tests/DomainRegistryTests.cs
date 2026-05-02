using HomeAssistantCommandPalette.Pages.Domains;

namespace HomeAssistantCommandPalette.Tests;

public class DomainRegistryTests
{
    [Fact]
    public void For_unknown_domain_and_entity_returns_default()
    {
        var b = DomainRegistry.For("definitely_not_a_real_domain", "x.y");
        Assert.Same(DomainRegistry.Default, b);
    }

    [Fact]
    public void TryGet_unknown_returns_false()
    {
        var found = DomainRegistry.TryGet("definitely_not_a_real_domain", "x.y", out var b);
        Assert.False(found);
        Assert.Null(b);
    }

    [Theory]
    [InlineData("switch")]
    [InlineData("input_boolean")]
    [InlineData("group")]
    [InlineData("scene")]
    [InlineData("script")]
    [InlineData("button")]
    [InlineData("input_button")]
    [InlineData("counter")]
    [InlineData("automation")]
    [InlineData("vacuum")]
    [InlineData("timer")]
    [InlineData("update")]
    [InlineData("person")]
    [InlineData("input_select")]
    [InlineData("input_number")]
    [InlineData("cover")]
    [InlineData("fan")]
    [InlineData("light")]
    [InlineData("climate")]
    [InlineData("media_player")]
    [InlineData("camera")]
    [InlineData("weather")]
    [InlineData("sensor")]
    [InlineData("binary_sensor")]
    public void TryGet_returns_a_behavior_carrying_the_requested_domain(string domain)
    {
        var found = DomainRegistry.TryGet(domain, $"{domain}.test", out var b);
        Assert.True(found);
        Assert.NotNull(b);
        Assert.Equal(domain, b!.Domain);
    }
}
