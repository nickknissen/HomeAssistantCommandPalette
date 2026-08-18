using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;

namespace HomeAssistantCommandPalette.Tests;

/// <summary>
/// The details pane is the expensive part of a row: a numeric sensor's
/// trend row fetches 24h of history, a camera's hero image fetches a
/// snapshot. CmdPal only reads Details for the selected row, so a render
/// must not pay for either.
/// </summary>
public sealed class EntityListPageLazyDetailsTests
{
    private static readonly HaEntity NumericSensor = TestEntities.Make(
        "sensor.living_room_temperature",
        state: "21.5",
        friendlyName: "Living room temperature",
        attributes: new Dictionary<string, object?> { ["unit_of_measurement"] = "°C" });

    private static readonly HaEntity Camera = TestEntities.Make(
        "camera.front_door",
        state: "idle",
        friendlyName: "Front door");

    private static EntityListPage AllEntitiesPage(RecordingHaClient client)
        => new(new HaSettings(), client, new StubIconResolver(), title: "All Entities", id: "ha.entities");

    private static EntityListPage CameraGridPage(RecordingHaClient client)
        => new(new HaSettings(), client, new StubIconResolver(), title: "Cameras", id: "ha.cameras", domains: ["camera"]);

    [Fact]
    public void rendering_the_list_fetches_neither_history_nor_snapshots()
    {
        var client = new RecordingHaClient();
        client.Entities.AddRange([NumericSensor, Camera]);

        var items = AllEntitiesPage(client).GetItems();

        Assert.Equal(2, items.Length);
        Assert.Empty(client.HistoryRequests);
        Assert.Empty(client.CameraSnapshotRequests);
    }

    [Fact]
    public void selecting_a_sensor_row_fetches_its_history_once()
    {
        var client = new RecordingHaClient
        {
            History = [new HaHistoryPoint(DateTimeOffset.UtcNow.AddHours(-1), 20), new HaHistoryPoint(DateTimeOffset.UtcNow, 21.5)],
        };
        client.Entities.Add(NumericSensor);
        var item = AllEntitiesPage(client).GetItems()[0];

        var details = item.Details;

        Assert.Single(client.HistoryRequests);
        Assert.Equal(NumericSensor.EntityId, client.HistoryRequests[0].EntityId);
        Assert.NotNull(details);

        // Second read is served from the item, not from HA.
        Assert.Same(details, item.Details);
        Assert.Single(client.HistoryRequests);
    }

    [Fact]
    public void selecting_a_camera_row_fetches_its_snapshot()
    {
        var client = new RecordingHaClient { CameraSnapshotPath = @"C:\temp\front_door.jpg" };
        client.Entities.Add(Camera);
        var item = AllEntitiesPage(client).GetItems()[0];

        _ = item.Details;

        Assert.Equal([Camera.EntityId], client.CameraSnapshotRequests);
    }

    [Fact]
    public void camera_grid_page_still_fetches_snapshots_up_front()
    {
        // The grid renders each snapshot as the card thumbnail, so there
        // the fetch can't wait for selection.
        var client = new RecordingHaClient { CameraSnapshotPath = @"C:\temp\front_door.jpg" };
        client.Entities.Add(Camera);

        var items = CameraGridPage(client).GetItems();

        Assert.Equal([Camera.EntityId], client.CameraSnapshotRequests);
        Assert.Equal(@"C:\temp\front_door.jpg", items[0].Icon?.Light.Icon);
    }
}
