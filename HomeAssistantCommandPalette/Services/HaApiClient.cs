using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HomeAssistantCommandPalette.Models;
using Microsoft.Extensions.Caching.Memory;

namespace HomeAssistantCommandPalette.Services;

/// <summary>
/// Thin REST client for the Home Assistant HTTP API:
///   GET  /api/states                       → entity list
///   POST /api/services/{domain}/{service}  → call a service
/// Auth: Bearer &lt;long-lived access token&gt;.
/// </summary>
public sealed partial class HaApiClient : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    private readonly HaSettings _settings;
    private readonly object _gate = new();
    private HttpClient? _client;
    private string _clientUrl = string.Empty;
    private string _clientToken = string.Empty;
    private bool _clientIgnoreCerts;

    // Single MemoryCache backs all per-request caching. Keys are namespaced
    // by prefix so the four logical caches don't collide.
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private const string StatesCacheKey = "states";
    private const string AreaMapCacheKey = "area-map";
    private const string CameraKeyPrefix = "camera:";
    private const string PictureKeyPrefix = "picture:";

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(3);
    // Areas rarely change — keep them around longer than the state cache.
    private static readonly TimeSpan AreaCacheTtl = TimeSpan.FromMinutes(5);
    // When a fetch fails or returns no areas, retry sooner instead of
    // sitting on a useless cached result for 5 minutes. Covers the case
    // where the user is fixing their HA config in another tab.
    private static readonly TimeSpan AreaEmptyRetryTtl = TimeSpan.FromSeconds(30);
    // Match Raycast's default camera refresh cadence — short enough that
    // re-opening the page shows recent video, long enough to dedupe back-
    // to-back GetItems calls on the same page render.
    private static readonly TimeSpan CameraSnapshotTtl = TimeSpan.FromSeconds(5);
    // Avatars / entity pictures change rarely. A long TTL keeps repeat
    // page renders cheap; if the picture is updated in HA it'll surface on
    // the next CmdPal restart.
    private static readonly TimeSpan EntityPictureTtl = TimeSpan.FromMinutes(15);

    // Jinja template that returns a JSON array of [entity_id, area_name]
    // pairs. HA's `area_name(entity_id)` walks entity → device → area in
    // the registry, matching the resolution Raycast does over WebSocket.
    //
    // Dict / list comprehensions look natural here but HA's sandboxed
    // Jinja parser rejects them with "expected token ',', got 'for'".
    // Build the list explicitly via a namespace accumulator instead.
    private const string AreaMapTemplate =
        "{% set ns = namespace(items=[]) %}" +
        "{% for s in states %}" +
        "{% set a = area_name(s.entity_id) %}" +
        "{% if a %}{% set ns.items = ns.items + [[s.entity_id, a]] %}{% endif %}" +
        "{% endfor %}" +
        "{{ ns.items | tojson }}";

    /// <summary>
    /// Diagnostic for the most recent area map fetch:
    ///   -1 = never attempted / failed (template errored or network blip)
    ///    0 = HA responded but no entities have areas assigned
    ///   >0 = number of entities with an area in HA
    /// Read by <see cref="HomeAssistantCommandPalette.Pages.EntityListPage"/>
    /// to surface the right "no area" hint.
    /// </summary>
    public int LastAreaCount { get; private set; } = -1;

    /// <summary>
    /// Last error from the area-map fetch. Empty when the most recent
    /// fetch succeeded (regardless of count). Surfaced in Connection Check
    /// so the user can see why areas aren't showing up.
    /// </summary>
    public string LastAreaError { get; private set; } = string.Empty;

    public HaApiClient(HaSettings settings)
    {
        _settings = settings;
    }

    public HaQueryResult GetStates()
    {
#if DEMO_MODE
        return DemoHaData.Result();
#else
        if (!_settings.IsConfigured)
        {
            return new HaQueryResult
            {
                ErrorKind = HaErrorKind.NotConfigured,
                ErrorTitle = "Home Assistant not configured",
                ErrorDescription = "Open the extension settings and set the URL and Long-Lived Access Token.",
            };
        }

        if (_cache.TryGetValue(StatesCacheKey, out HaQueryResult? cached) && cached is { HasError: false })
        {
            return cached;
        }

        var result = FetchStates();
        // Only cache successful results; an error should retry on next call.
        if (!result.HasError)
        {
            _cache.Set(StatesCacheKey, result, CacheTtl);
        }
        return result;
#endif
    }

    public bool TryCallService(string domain, string service, string entityId, out string error)
        => TryCallService(domain, service, entityId, extraData: null, out error);

    /// <summary>
    /// Fetches the latest snapshot from <c>/api/camera_proxy/{entity_id}</c>
    /// and writes it to a temp file. Returns the absolute file path on
    /// success or null on any failure (auth, timeout, missing camera).
    /// Cached per-entity for <see cref="CameraSnapshotTtl"/> so a single
    /// page render with N cameras issues at most N HTTP gets, and a
    /// follow-up render within the TTL re-uses the cached file.
    /// </summary>
    public string? GetCameraSnapshotPath(string entityId)
    {
        if (string.IsNullOrEmpty(entityId) || !_settings.IsConfigured) return null;

        var cacheKey = CameraKeyPrefix + entityId;
        if (_cache.TryGetValue(cacheKey, out string? cachedPath)
            && !string.IsNullOrEmpty(cachedPath)
            && File.Exists(cachedPath))
        {
            return cachedPath;
        }

        try
        {
            var client = GetClient();
            // Tighter timeout than DefaultTimeout — a stale camera should
            // not block the entire camera list. ~3s is the same budget as
            // a single REST call, and the file-cache means subsequent
            // renders are free.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var url = $"{_settings.Url}/api/camera_proxy/{Uri.EscapeDataString(entityId)}";
            var response = client.GetAsync(url, cts.Token).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return null;

            var bytes = response.Content.ReadAsByteArrayAsync(cts.Token).GetAwaiter().GetResult();
            if (bytes.Length == 0) return null;

            var ext = response.Content.Headers.ContentType?.MediaType switch
            {
                "image/png" => "png",
                _ => "jpg",
            };
            var safe = entityId.Replace('.', '_').Replace('/', '_').Replace('\\', '_');
            var dir = Path.Combine(Path.GetTempPath(), "HomeAssistantCommandPalette", "camera");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{safe}.{ext}");
            File.WriteAllBytes(path, bytes);

            _cache.Set(cacheKey, path, CameraSnapshotTtl);
            return path;
        }
        catch
        {
            // Best-effort: a transient failure shouldn't take down the page.
            return null;
        }
    }

    /// <summary>
    /// Fetches an authenticated entity picture (e.g. <c>entity_picture</c>
    /// from a person entity). The path may be relative (HA-served, prefixed
    /// with the configured URL) or absolute (an arbitrary HTTPS URL — only
    /// the relative form gets a Bearer header attached). Returns the cached
    /// file path on success, or null on any failure. Cached separately
    /// from camera snapshots and per-(entity_id, url) so the file is
    /// re-fetched if HA rotates the URL.
    /// </summary>
    public string? GetEntityPicturePath(string entityId, string entityPicture)
    {
        if (string.IsNullOrEmpty(entityId) || string.IsNullOrEmpty(entityPicture) || !_settings.IsConfigured)
            return null;

        // Composite key: when HA rotates the entity_picture URL, the old
        // entry naturally falls out of the cache rather than being served
        // until the TTL expires.
        var cacheKey = PictureKeyPrefix + entityId + "|" + entityPicture;
        if (_cache.TryGetValue(cacheKey, out string? cachedPath)
            && !string.IsNullOrEmpty(cachedPath)
            && File.Exists(cachedPath))
        {
            return cachedPath;
        }

        try
        {
            var isRelative = entityPicture.StartsWith('/');
            var url = isRelative
                ? $"{_settings.Url.TrimEnd('/')}{entityPicture}"
                : entityPicture;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            HttpResponseMessage response;
            if (isRelative)
            {
                // HA-served — reuse the authed client.
                response = GetClient().GetAsync(url, cts.Token).GetAwaiter().GetResult();
            }
            else
            {
                // External URL (e.g. Gravatar, a CDN). No bearer.
                using var anon = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                response = anon.GetAsync(url, cts.Token).GetAwaiter().GetResult();
            }
            if (!response.IsSuccessStatusCode) return null;

            var bytes = response.Content.ReadAsByteArrayAsync(cts.Token).GetAwaiter().GetResult();
            if (bytes.Length == 0) return null;

            var ext = response.Content.Headers.ContentType?.MediaType switch
            {
                "image/png" => "png",
                "image/svg+xml" => "svg",
                "image/webp" => "webp",
                _ => "jpg",
            };
            var safe = entityId.Replace('.', '_').Replace('/', '_').Replace('\\', '_');
            var dir = Path.Combine(Path.GetTempPath(), "HomeAssistantCommandPalette", "picture");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"{safe}.{ext}");
            File.WriteAllBytes(path, bytes);

            _cache.Set(cacheKey, path, EntityPictureTtl);
            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sweeps temp snapshot / entity-picture files older than one hour.
    /// Called once at extension startup — the in-memory cache resets on
    /// restart, so any file from a previous session is unreachable. The
    /// 1 h threshold leaves a safety margin if a second CmdPal process
    /// is racing the cleanup. Best-effort: a failure is silent.
    /// </summary>
    public static void CleanupStaleSnapshots()
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromHours(1);
        foreach (var sub in new[] { "camera", "picture" })
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "HomeAssistantCommandPalette", sub);
                if (!Directory.Exists(dir)) continue;
                foreach (var path in Directory.EnumerateFiles(dir))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) < cutoff)
                        {
                            File.Delete(path);
                        }
                    }
                    catch
                    {
                        // File in use, locked, or vanished — skip it.
                    }
                }
            }
            catch
            {
                // Permission or I/O error reading the dir — skip the sweep.
            }
        }
    }

    /// <summary>
    /// Lists configured calendars via <c>GET /api/calendars</c>. Errors map
    /// to <see cref="HaErrorKind"/> the same way <see cref="GetStates"/>
    /// does so the calendar page can reuse the "press Enter to open
    /// settings" flow on auth / config failures.
    /// </summary>
    public HaCalendarsResult GetCalendars()
    {
        if (!_settings.IsConfigured)
        {
            return new HaCalendarsResult
            {
                ErrorKind = HaErrorKind.NotConfigured,
                ErrorTitle = "Home Assistant not configured",
                ErrorDescription = "Open the extension settings and set the URL and Long-Lived Access Token.",
            };
        }

        try
        {
            var client = GetClient();
            using var cts = new CancellationTokenSource(DefaultTimeout);
            var response = client.GetAsync($"{_settings.Url}/api/calendars", cts.Token).GetAwaiter().GetResult();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new HaCalendarsResult
                {
                    ErrorKind = HaErrorKind.Unauthorized,
                    ErrorTitle = "Token rejected",
                    ErrorDescription = "Generate a new Long-Lived Access Token under Profile → Security.",
                };
            }
            if (!response.IsSuccessStatusCode)
            {
                return new HaCalendarsResult
                {
                    ErrorKind = HaErrorKind.NetworkError,
                    ErrorTitle = $"Home Assistant returned {(int)response.StatusCode}",
                    ErrorDescription = response.ReasonPhrase ?? string.Empty,
                };
            }

            var json = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            return new HaCalendarsResult { Calendars = ParseCalendars(json) };
        }
        catch (UriFormatException)
        {
            return new HaCalendarsResult
            {
                ErrorKind = HaErrorKind.InvalidUrl,
                ErrorTitle = "Home Assistant URL is invalid",
                ErrorDescription = "Check the URL in extension settings.",
            };
        }
        catch (TaskCanceledException)
        {
            return new HaCalendarsResult
            {
                ErrorKind = HaErrorKind.NetworkError,
                ErrorTitle = "Couldn't reach Home Assistant",
                ErrorDescription = "Request timed out.",
            };
        }
        catch (Exception ex)
        {
            return new HaCalendarsResult
            {
                ErrorKind = HaErrorKind.NetworkError,
                ErrorTitle = "Couldn't reach Home Assistant",
                ErrorDescription = ex.Message,
            };
        }
    }

    /// <summary>
    /// Fetches events for a calendar between <paramref name="start"/> and
    /// <paramref name="end"/>. Best-effort: any failure returns an empty
    /// list so a single broken calendar doesn't take the whole page down.
    /// </summary>
    public IReadOnlyList<HaCalendarEvent> GetCalendarEvents(HaCalendar calendar, DateTimeOffset start, DateTimeOffset end)
    {
        if (!_settings.IsConfigured) return Array.Empty<HaCalendarEvent>();

        try
        {
            var client = GetClient();
            using var cts = new CancellationTokenSource(DefaultTimeout);
            // HA expects RFC 3339 / ISO 8601 with offset. The "o" round-trip
            // format on a UTC DateTimeOffset emits "2025-01-15T19:00:00.0000000+00:00".
            var startStr = start.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            var endStr = end.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
            var url = $"{_settings.Url}/api/calendars/{Uri.EscapeDataString(calendar.EntityId)}?start={Uri.EscapeDataString(startStr)}&end={Uri.EscapeDataString(endStr)}";
            var response = client.GetAsync(url, cts.Token).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return Array.Empty<HaCalendarEvent>();

            var json = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            return ParseCalendarEvents(json, calendar);
        }
        catch
        {
            return Array.Empty<HaCalendarEvent>();
        }
    }

    private static IReadOnlyList<HaCalendar> ParseCalendars(string json)
    {
        var dtos = JsonSerializer.Deserialize(json, HaJsonContext.Default.ListHaCalendarDto);
        if (dtos is null) return Array.Empty<HaCalendar>();
        var list = new List<HaCalendar>(dtos.Count);
        foreach (var dto in dtos)
        {
            if (string.IsNullOrEmpty(dto.EntityId)) continue;
            list.Add(new HaCalendar(dto.EntityId, string.IsNullOrEmpty(dto.Name) ? dto.EntityId : dto.Name));
        }
        return list;
    }

    private static IReadOnlyList<HaCalendarEvent> ParseCalendarEvents(string json, HaCalendar calendar)
    {
        var dtos = JsonSerializer.Deserialize(json, HaJsonContext.Default.ListHaCalendarEventDto);
        if (dtos is null) return Array.Empty<HaCalendarEvent>();
        var list = new List<HaCalendarEvent>(dtos.Count);
        foreach (var dto in dtos)
        {
            // Skip events with unparseable start times — same behaviour as
            // the previous hand-rolled parser.
            if (dto.Start is null) continue;
            var end = dto.End ?? dto.Start;
            list.Add(new HaCalendarEvent(
                calendar.EntityId,
                calendar.Name,
                string.IsNullOrEmpty(dto.Summary) ? "(no title)" : dto.Summary,
                dto.Start.Value,
                end.Value,
                dto.Start.AllDay,
                string.IsNullOrEmpty(dto.Description) ? null : dto.Description,
                string.IsNullOrEmpty(dto.Location) ? null : dto.Location));
        }
        return list;
    }

    /// <summary>
    /// Sends <paramref name="text"/> to <c>POST /api/conversation/process</c>
    /// and returns Assist's response. HA defaults the language to the
    /// instance's configured default when omitted, so no language hint is
    /// sent. Failures return a result with <c>Success=false</c> rather
    /// than throwing — the caller renders Error inline as a list row.
    /// </summary>
    public HaAssistResult AskAssist(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HaAssistResult(false, string.Empty, null, "Question is empty.");
        }
        if (!_settings.IsConfigured)
        {
            return new HaAssistResult(false, string.Empty, null, "Home Assistant is not configured.");
        }

        try
        {
            var client = GetClient();
            using var content = BuildAssistPayload(text);
            using var cts = new CancellationTokenSource(DefaultTimeout);
            var response = client.PostAsync($"{_settings.Url}/api/conversation/process", content, cts.Token).GetAwaiter().GetResult();

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new HaAssistResult(false, string.Empty, null, "Token rejected (401).");
            }
            if (!response.IsSuccessStatusCode)
            {
                return new HaAssistResult(false, string.Empty, null, $"HA returned {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var json = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            return ParseAssistResponse(json);
        }
        catch (Exception ex)
        {
            return new HaAssistResult(false, string.Empty, null, ex.Message);
        }
    }

    private static ByteArrayContent BuildAssistPayload(string text)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("text", text);
            writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        var payload = new ByteArrayContent(bytes);
        payload.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return payload;
    }

    private static HaAssistResult ParseAssistResponse(string json)
    {
        var dto = JsonSerializer.Deserialize(json, HaJsonContext.Default.HaAssistDto);
        var resp = dto?.Response;
        if (resp is null)
        {
            return new HaAssistResult(false, string.Empty, null, "Unexpected response shape.");
        }

        var speech = resp.Speech?.Plain?.Speech ?? string.Empty;
        var isError = string.Equals(resp.ResponseType, "error", StringComparison.OrdinalIgnoreCase);
        return new HaAssistResult(
            Success: !isError,
            Speech: speech,
            ResponseType: resp.ResponseType,
            Error: isError ? (string.IsNullOrEmpty(speech) ? "Assist returned an error." : speech) : null);
    }

    /// <summary>
    /// Pings <c>GET /api/config</c> and reports HA's reported version,
    /// location, time zone, run state and round-trip latency. Used by the
    /// Connection Check diagnostic page; intentionally avoids the state
    /// cache so each invocation hits the wire.
    /// </summary>
    public HaConfigProbe ProbeConfig()
    {
        if (!_settings.IsConfigured)
        {
            return new HaConfigProbe(false, HaErrorKind.NotConfigured,
                "Set the URL and access token in extension settings.",
                null, null, null, null, 0);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var client = GetClient();
            using var cts = new CancellationTokenSource(DefaultTimeout);
            var response = client.GetAsync($"{_settings.Url}/api/config", cts.Token).GetAwaiter().GetResult();
            var elapsed = sw.ElapsedMilliseconds;

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new HaConfigProbe(false, HaErrorKind.Unauthorized,
                    "Token rejected (401). Generate a new Long-Lived Access Token under Profile → Security.",
                    null, null, null, null, elapsed);
            }
            if (!response.IsSuccessStatusCode)
            {
                return new HaConfigProbe(false, HaErrorKind.NetworkError,
                    $"HA returned {(int)response.StatusCode} {response.ReasonPhrase}",
                    null, null, null, null, elapsed);
            }

            var json = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            var dto = JsonSerializer.Deserialize(json, HaJsonContext.Default.HaConfigDto);
            return new HaConfigProbe(true, HaErrorKind.None, null,
                dto?.Version, dto?.LocationName, dto?.TimeZone, dto?.State,
                elapsed);
        }
        catch (UriFormatException)
        {
            return new HaConfigProbe(false, HaErrorKind.InvalidUrl,
                "URL is invalid — must include scheme (http:// or https://).",
                null, null, null, null, sw.ElapsedMilliseconds);
        }
        catch (TaskCanceledException)
        {
            return new HaConfigProbe(false, HaErrorKind.NetworkError,
                $"Request timed out after {DefaultTimeout.TotalSeconds:0}s.",
                null, null, null, null, sw.ElapsedMilliseconds);
        }
        catch (HttpRequestException ex)
        {
            return new HaConfigProbe(false, HaErrorKind.NetworkError, ex.Message,
                null, null, null, null, sw.ElapsedMilliseconds);
        }
        catch (JsonException ex)
        {
            return new HaConfigProbe(false, HaErrorKind.ParseFailed, ex.Message,
                null, null, null, null, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Calls <c>POST /api/services/{domain}/{service}</c> with a body of
    /// <c>{"entity_id": ..., ...extraData}</c>. <paramref name="extraData"/>
    /// values may be string, bool, int / long / double; anything else is
    /// rendered via ToString.
    /// </summary>
    public bool TryCallService(
        string domain,
        string service,
        string entityId,
        IReadOnlyDictionary<string, object?>? extraData,
        out string error)
    {
        error = string.Empty;
        if (!_settings.IsConfigured)
        {
            error = "Home Assistant is not configured.";
            return false;
        }

        try
        {
            var client = GetClient();
            var url = $"{_settings.Url}/api/services/{domain}/{service}";
            using var content = BuildServiceCallPayload(entityId, extraData);

            using var cts = new CancellationTokenSource(DefaultTimeout);
            var response = client.PostAsync(url, content, cts.Token).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                error = $"HA returned {(int)response.StatusCode} {response.ReasonPhrase}";
                return false;
            }

            // Service call succeeded — invalidate cached states so the UI refresh
            // picks up the new state on the next GetItems().
            _cache.Remove(StatesCacheKey);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // Build {"entity_id":"...", ...extraData} without going through
    // JsonSerializer's reflection path — keeps the assembly trim/AOT clean.
    private static ByteArrayContent BuildServiceCallPayload(
        string entityId,
        IReadOnlyDictionary<string, object?>? extraData)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("entity_id", entityId);
            if (extraData is not null)
            {
                foreach (var kv in extraData)
                {
                    if (string.Equals(kv.Key, "entity_id", StringComparison.Ordinal)) continue;
                    WriteJsonValue(writer, kv.Key, kv.Value);
                }
            }
            writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        var payload = new ByteArrayContent(bytes);
        payload.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return payload;
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, string key, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNull(key); break;
            case bool b: writer.WriteBoolean(key, b); break;
            case int i: writer.WriteNumber(key, i); break;
            case long l: writer.WriteNumber(key, l); break;
            case double d: writer.WriteNumber(key, d); break;
            case float f: writer.WriteNumber(key, f); break;
            case string s: writer.WriteString(key, s); break;
            case System.Collections.IEnumerable e:
                writer.WritePropertyName(key);
                writer.WriteStartArray();
                foreach (var item in e) WriteJsonElement(writer, item);
                writer.WriteEndArray();
                break;
            default: writer.WriteString(key, value.ToString() ?? string.Empty); break;
        }
    }

    private static void WriteJsonElement(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null: writer.WriteNullValue(); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case int i: writer.WriteNumberValue(i); break;
            case long l: writer.WriteNumberValue(l); break;
            case double d: writer.WriteNumberValue(d); break;
            case float f: writer.WriteNumberValue(f); break;
            case string s: writer.WriteStringValue(s); break;
            default: writer.WriteStringValue(value.ToString() ?? string.Empty); break;
        }
    }

    private HaQueryResult FetchStates()
    {
        try
        {
            var client = GetClient();
            using var cts = new CancellationTokenSource(DefaultTimeout);
            var response = client.GetAsync($"{_settings.Url}/api/states", cts.Token).GetAwaiter().GetResult();
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new HaQueryResult
                {
                    ErrorKind = HaErrorKind.Unauthorized,
                    ErrorTitle = "Home Assistant rejected the access token",
                    ErrorDescription = "Generate a new Long-Lived Access Token under Profile → Security in Home Assistant.",
                };
            }
            if (!response.IsSuccessStatusCode)
            {
                return new HaQueryResult
                {
                    ErrorKind = HaErrorKind.NetworkError,
                    ErrorTitle = $"Home Assistant returned {(int)response.StatusCode}",
                    ErrorDescription = response.ReasonPhrase ?? string.Empty,
                };
            }

            var json = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            var entities = ParseStates(json);
            var areas = LoadAreaMapBestEffort();
            if (areas is { Count: > 0 })
            {
                entities = entities.Select(e => areas.TryGetValue(e.EntityId, out var area)
                    ? new HaEntity
                    {
                        EntityId = e.EntityId,
                        State = e.State,
                        Attributes = e.Attributes,
                        LastChanged = e.LastChanged,
                        LastUpdated = e.LastUpdated,
                        AreaName = area,
                    }
                    : e).ToList();
            }
            return new HaQueryResult { Items = entities };
        }
        catch (UriFormatException)
        {
            return new HaQueryResult
            {
                ErrorKind = HaErrorKind.InvalidUrl,
                ErrorTitle = "Home Assistant URL is invalid",
                ErrorDescription = "Check the URL in extension settings (must include scheme, e.g. http://...).",
            };
        }
        catch (TaskCanceledException)
        {
            return new HaQueryResult
            {
                ErrorKind = HaErrorKind.NetworkError,
                ErrorTitle = "Couldn't reach Home Assistant",
                ErrorDescription = "Request timed out. Check the URL and that the instance is reachable from this machine.",
            };
        }
        catch (HttpRequestException ex)
        {
            return new HaQueryResult
            {
                ErrorKind = HaErrorKind.NetworkError,
                ErrorTitle = "Couldn't reach Home Assistant",
                ErrorDescription = ex.Message,
            };
        }
        catch (JsonException ex)
        {
            return new HaQueryResult
            {
                ErrorKind = HaErrorKind.ParseFailed,
                ErrorTitle = "Couldn't parse Home Assistant response",
                ErrorDescription = ex.Message,
            };
        }
    }

    // Returns the cached map if fresh, refreshes via POST /api/template
    // otherwise. Best-effort: any failure (template disabled, parse error,
    // network blip) resolves to null so the caller continues without areas.
    private Dictionary<string, string>? LoadAreaMapBestEffort()
    {
        if (_cache.TryGetValue(AreaMapCacheKey, out Dictionary<string, string>? cached))
        {
            return cached;
        }

        try
        {
            var client = GetClient();
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString("template", AreaMapTemplate);
                writer.WriteEndObject();
            }
            using var content = new ByteArrayContent(stream.ToArray());
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            using var cts = new CancellationTokenSource(DefaultTimeout);
            var response = client.PostAsync($"{_settings.Url}/api/template", content, cts.Token).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                var body = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
                CacheAreas(null, $"HTTP {(int)response.StatusCode}: {Truncate(body, 200)}");
                return null;
            }
            var raw = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();

            var map = ParseAreaMap(raw);
            CacheAreas(map, string.Empty);
            return map;
        }
        catch (Exception ex)
        {
            CacheAreas(null, ex.Message);
            return null;
        }
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    private static Dictionary<string, string> ParseAreaMap(string raw)
    {
        // /api/template returns the rendered string. HA wraps the body in
        // JSON quotes some versions, leaves it bare in others — peel one
        // string layer if present.
        var trimmed = raw.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            try { trimmed = JsonSerializer.Deserialize(trimmed, HaJsonContext.Default.String) ?? trimmed; }
            catch { /* leave as-is */ }
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "[]" || trimmed == "[ ]") return map;

        try
        {
            var pairs = JsonSerializer.Deserialize(trimmed, HaJsonContext.Default.ListListString);
            if (pairs is null) return map;
            foreach (var pair in pairs)
            {
                if (pair is null || pair.Count < 2) continue;
                var id = pair[0];
                var area = pair[1];
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(area))
                {
                    map[id] = area;
                }
            }
        }
        catch (JsonException) { /* HA returned something we can't parse — fall through to empty */ }

        return map;
    }

    private void CacheAreas(Dictionary<string, string>? map, string error)
    {
        // Empty / null map → retry sooner so the user isn't stuck for 5 min
        // after fixing area assignments in HA. A populated map gets the
        // long TTL since areas rarely change.
        var ttl = (map is null || map.Count == 0) ? AreaEmptyRetryTtl : AreaCacheTtl;
        _cache.Set(AreaMapCacheKey, map, ttl);
        LastAreaCount = map is null ? -1 : map.Count;
        LastAreaError = error;
    }

    private static List<HaEntity> ParseStates(string json)
    {
        var dtos = JsonSerializer.Deserialize(json, HaJsonContext.Default.ListHaStateDto);
        var list = new List<HaEntity>(dtos?.Count ?? 0);
        if (dtos is null) return list;

        foreach (var dto in dtos)
        {
            if (string.IsNullOrEmpty(dto.EntityId)) continue;

            var attrs = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (dto.Attributes is not null)
            {
                foreach (var (key, value) in dto.Attributes)
                {
                    attrs[key] = ToObject(value);
                }
            }

            list.Add(new HaEntity
            {
                EntityId = dto.EntityId,
                State = dto.State ?? string.Empty,
                Attributes = attrs,
                LastChanged = dto.LastChanged,
                LastUpdated = dto.LastUpdated,
            });
        }

        list.Sort((a, b) => string.Compare(a.FriendlyName, b.FriendlyName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static object? ToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => ParseJsonArray(el),
        _ => el.GetRawText(),
    };

    // Recursive — preserves typed values inside arrays (esp. string[]
    // for things like hvac_modes / fan_modes).
    private static List<object?> ParseJsonArray(JsonElement el)
    {
        var items = new List<object?>(el.GetArrayLength());
        foreach (var item in el.EnumerateArray())
        {
            items.Add(ToObject(item));
        }
        return items;
    }

    private HttpClient GetClient()
    {
        lock (_gate)
        {
            if (_client is not null
                && string.Equals(_clientUrl, _settings.Url, StringComparison.OrdinalIgnoreCase)
                && string.Equals(_clientToken, _settings.Token, StringComparison.Ordinal)
                && _clientIgnoreCerts == _settings.IgnoreCertificateErrors)
            {
                return _client;
            }

            _client?.Dispose();

            HttpMessageHandler handler;
            if (_settings.IgnoreCertificateErrors)
            {
                handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                };
            }
            else
            {
                handler = new HttpClientHandler();
            }

            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = DefaultTimeout,
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.Token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _client = client;
            _clientUrl = _settings.Url;
            _clientToken = _settings.Token;
            _clientIgnoreCerts = _settings.IgnoreCertificateErrors;
            return _client;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _client?.Dispose();
            _client = null;
        }
        _cache.Dispose();
    }
}
