using System.Reflection;
using HomeAssistantCommandPalette.Services;

namespace HomeAssistantCommandPalette.Tests;

public sealed class RestHaClientAreaCacheTests
{
    [Fact]
    public void area_map_empty_result_retries_after_one_minute()
    {
        var ttl = typeof(RestHaClient)
            .GetField("AreaEmptyRetryTtl", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);

        Assert.Equal(TimeSpan.FromMinutes(1), ttl);
    }
}
