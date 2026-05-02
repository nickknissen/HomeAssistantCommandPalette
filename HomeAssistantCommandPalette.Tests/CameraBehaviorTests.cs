using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Pages.Domains;
using HomeAssistantCommandPalette.Pages.Domains.Behaviors;
using HomeAssistantCommandPalette.Services;
using HomeAssistantCommandPalette.Tests.Fakes;

namespace HomeAssistantCommandPalette.Tests;

public class CameraBehaviorTests
{
    private sealed class StubSnapshotClient : IHaClient
    {
        public string? Path { get; init; }

        public string? GetCameraSnapshotPath(string entityId) => Path;

        // unused
        public bool TryCallService(string domain, string service, string entityId, out string errorMessage) => throw new NotSupportedException();
        public bool TryCallService(string domain, string service, string entityId, IReadOnlyDictionary<string, object?>? extraData, out string errorMessage) => throw new NotSupportedException();
        public string? GetEntityPicturePath(string entityId, string entityPicture) => null;
        public HaQueryResult GetStates() => throw new NotSupportedException();
        public HaCalendarsResult GetCalendars() => throw new NotSupportedException();
        public IReadOnlyList<HaCalendarEvent> GetCalendarEvents(HaCalendar calendar, DateTimeOffset start, DateTimeOffset endTime) => throw new NotSupportedException();
        public HaAssistResult AskAssist(string text) => throw new NotSupportedException();
        public HaConfigProbe ProbeConfig() => throw new NotSupportedException();
        public int LastAreaCount => -1;
        public string LastAreaError => string.Empty;
        public void Dispose() { }
    }

    [Fact]
    public void HeroImage_returns_null_when_snapshot_path_unavailable()
    {
        var entity = TestEntities.Make("camera.front_door", "idle");
        var ctx = new DomainCtx(entity, new StubSnapshotClient { Path = null }, new HaSettings(), OnSuccess: () => { });
        Assert.Null(new CameraBehavior().BuildHeroImage(in ctx));
    }

    [Fact]
    public void HeroImage_wraps_snapshot_path_when_present()
    {
        var entity = TestEntities.Make("camera.front_door", "idle");
        var ctx = new DomainCtx(entity, new StubSnapshotClient { Path = "C:\\tmp\\snap.jpg" }, new HaSettings(), OnSuccess: () => { });
        Assert.NotNull(new CameraBehavior().BuildHeroImage(in ctx));
    }
}
