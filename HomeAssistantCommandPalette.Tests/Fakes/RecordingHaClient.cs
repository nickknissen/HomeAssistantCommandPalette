using System;
using System.Collections.Generic;
using HomeAssistantCommandPalette.Models;
using HomeAssistantCommandPalette.Services;

namespace HomeAssistantCommandPalette.Tests.Fakes;

/// <summary>
/// Test double for <see cref="IHaClient"/> that records every
/// <c>TryCallService</c> invocation. Tests construct one of these,
/// hand it to a behavior via <see cref="DomainCtx"/>, invoke a command
/// returned by the behavior, and assert against the recorded call.
/// </summary>
internal sealed class RecordingHaClient : IHaClient
{
    public List<CallRecord> Calls { get; } = new();

    public bool TryCallService(string domain, string service, string entityId, out string errorMessage)
        => TryCallService(domain, service, entityId, extraData: null, out errorMessage);

    public bool TryCallService(
        string domain,
        string service,
        string entityId,
        IReadOnlyDictionary<string, object?>? extraData,
        out string errorMessage)
    {
        Calls.Add(new CallRecord(domain, service, entityId, extraData));
        errorMessage = string.Empty;
        return true;
    }

    public HaQueryResult GetStates() => throw new NotSupportedException();

    public string? GetCameraSnapshotPath(string entityId) => null;

    public string? GetEntityPicturePath(string entityId, string entityPicture) => null;

    public HaCalendarsResult GetCalendars() => throw new NotSupportedException();

    public IReadOnlyList<HaCalendarEvent> GetCalendarEvents(HaCalendar calendar, DateTimeOffset start, DateTimeOffset endTime)
        => throw new NotSupportedException();

    public HaAssistResult AskAssist(string text) => throw new NotSupportedException();

    public HaConfigProbe ProbeConfig() => throw new NotSupportedException();

    public int LastAreaCount => -1;

    public string LastAreaError => string.Empty;

    public void Dispose() { }

    public sealed record CallRecord(
        string Domain,
        string Service,
        string EntityId,
        IReadOnlyDictionary<string, object?>? ExtraData);
}
