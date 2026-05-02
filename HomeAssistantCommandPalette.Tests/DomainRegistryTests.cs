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

    [Fact]
    public void TryGet_switch_returns_registered_behavior()
    {
        var found = DomainRegistry.TryGet("switch", out var b);
        Assert.True(found);
        Assert.NotNull(b);
        Assert.Equal("switch", b!.Domain);
    }
}
