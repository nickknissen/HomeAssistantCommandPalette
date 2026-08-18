using System.Reflection;
using HomeAssistantCommandPalette.Pages;
using HomeAssistantCommandPalette.Pages.Domains.IconPipeline;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;

namespace HomeAssistantCommandPalette.Tests;

/// <summary>
/// End-to-end over <see cref="FakeHaServer"/>: the real client hydrating
/// from the WebSocket, the real page rendering on top of it. Covers the
/// seam the per-page tests mock out — that a big instance still only ever
/// reaches CmdPal one page at a time.
/// </summary>
public sealed class EntityListPageLiveInstanceTests : IDisposable
{
    private const int EntityCount = 1500;

    private readonly FakeHaServer _server = new(EntityCount, port: 18477);
    private readonly HaSettings _settings = new();
    private readonly RestHaClient _client;

    public EntityListPageLiveInstanceTests()
    {
        SetText("_urlSetting", _server.Url);
        SetText("_tokenSetting", "fake-token");
        _client = new RestHaClient(_settings);
    }

    private void SetText(string field, string value)
    {
        var setting = typeof(HaSettings)
            .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(_settings)!;
        setting.GetType().GetProperty("Value")!.SetValue(setting, value);
    }

    private EntityListPage NewPage(IEntityIconResolver? resolver = null)
        => new(_settings, _client, resolver ?? new StubIconResolver(), title: "All Entities", id: "ha.entities");

    private void WaitForHydration()
    {
        // First GetStates kicks the WS pump; hydration lands shortly after.
        for (var attempt = 0; attempt < 50 && !_client.IsLive; attempt++)
        {
            _ = _client.GetStates();
            Thread.Sleep(100);
        }
    }

    [Fact]
    public void a_large_instance_is_handed_over_one_page_at_a_time()
    {
        WaitForHydration();
        var page = NewPage();

        var items = page.GetItems();

        Assert.True(_client.IsLive, "expected the WebSocket snapshot to be live");
        Assert.Equal(EntityCount, _client.GetStates().Items.Count);
        Assert.Equal(100, items.Length);
        Assert.True(page.HasMoreItems);
    }

    [Fact]
    public void repeat_renders_hand_back_the_same_item_instances()
    {
        WaitForHydration();
        var page = NewPage();

        var first = page.GetItems();
        var second = page.GetItems();

        // CmdPal keys its ViewModel cache on item reference identity, so
        // this is what keeps a re-render from re-reading every property
        // over COM and minting a fresh set of wrappers.
        Assert.Equal(first, second);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }
}
