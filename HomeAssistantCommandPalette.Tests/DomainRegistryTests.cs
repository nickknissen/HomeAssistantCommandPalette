using HomeAssistantCommandPalette.Pages.Domains;

namespace HomeAssistantCommandPalette.Tests;

public class DomainRegistryTests
{
    [Fact]
    public void For_unknown_domain_returns_default()
    {
        var b = DomainRegistry.For("definitely_not_a_real_domain");
        Assert.Same(DomainRegistry.Default, b);
    }

    [Fact]
    public void TryGet_unknown_domain_returns_false()
    {
        var found = DomainRegistry.TryGet("definitely_not_a_real_domain", out var b);
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
    public void TryGet_returns_a_behavior_carrying_the_requested_domain(string domain)
    {
        var found = DomainRegistry.TryGet(domain, out var b);
        Assert.True(found);
        Assert.NotNull(b);
        Assert.Equal(domain, b!.Domain);
    }
}
