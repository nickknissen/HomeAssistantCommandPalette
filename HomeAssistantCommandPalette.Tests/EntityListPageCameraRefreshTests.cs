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
    public void camera_auto_refresh_uses_default_three_second_interval()
    {
        var settings = new HaSettings();
        SetCameraRefreshInterval(settings, "3000");

        Assert.Equal(TimeSpan.FromSeconds(3), EntityListPage.CameraAutoRefreshIntervalFromSettings(settings));
    }

    [Fact]
    public void camera_auto_refresh_can_be_disabled_with_zero()
    {
        var settings = new HaSettings();
        SetCameraRefreshInterval(settings, "0");

        Assert.Equal(TimeSpan.Zero, EntityListPage.CameraAutoRefreshIntervalFromSettings(settings));
    }

    [Fact]
    public void camera_snapshot_cache_ttl_follows_configured_refresh_interval()
    {
        var settings = new HaSettings();
        SetCameraRefreshInterval(settings, "7500");
        using var client = new RestHaClient(settings);

        var ttl = typeof(RestHaClient)
            .GetMethod("GetCameraSnapshotTtl", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(client, null);

        Assert.Equal(TimeSpan.FromMilliseconds(7500), ttl);
    }

    private static void SetCameraRefreshInterval(HaSettings settings, string value)
    {
        var setting = typeof(HaSettings)
            .GetField("_cameraRefreshIntervalSetting", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(settings)!;
        setting.GetType().GetProperty("Value")!.SetValue(setting, value);
    }
}
