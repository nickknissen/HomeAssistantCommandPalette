using System.Reflection;
using HomeAssistantCommandPalette.Pages;
using HomeAssistantCommandPalette.Services;

namespace HomeAssistantCommandPalette.Tests;

public sealed class EntityListPageCameraRefreshTests
{
    [Fact]
    public void camera_auto_refresh_only_applies_to_camera_domain_page()
    {
        Assert.True(EntityListPage.IsCameraAutoRefreshPage(["camera"], deviceClasses: null));

        Assert.False(EntityListPage.IsCameraAutoRefreshPage(["light"], deviceClasses: null));
        Assert.False(EntityListPage.IsCameraAutoRefreshPage(["camera", "light"], deviceClasses: null));
        Assert.False(EntityListPage.IsCameraAutoRefreshPage(domains: null, deviceClasses: null));
        Assert.False(EntityListPage.IsCameraAutoRefreshPage(["camera"], ["battery"]));
    }

    [Fact]
    public void camera_auto_refresh_interval_matches_snapshot_cache_ttl()
    {
        var autoRefreshInterval = typeof(EntityListPage)
            .GetField("CameraAutoRefreshInterval", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);
        var snapshotTtl = typeof(RestHaClient)
            .GetField("CameraSnapshotTtl", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null);

        Assert.Equal(TimeSpan.FromSeconds(3), autoRefreshInterval);
        Assert.Equal(autoRefreshInterval, snapshotTtl);
    }
}
