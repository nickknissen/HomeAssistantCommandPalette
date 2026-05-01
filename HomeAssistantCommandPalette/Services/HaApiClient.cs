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

    private readonly object _cacheLock = new();
    private HaQueryResult? _cachedResult;
    private DateTime _cachedAtUtc = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(3);

    private readonly object _areaCacheLock = new();
    private Dictionary<string, string>? _cachedAreaMap;
    private DateTime _areaCachedAtUtc = DateTime.MinValue;
    // Areas rarely change — keep them around longer than the state cache.
    private static readonly TimeSpan AreaCacheTtl = TimeSpan.FromMinutes(5);

    private readonly object _cameraCacheLock = new();
    private readonly Dictionary<string, (string Path, DateTime FetchedAtUtc)> _cameraCache = new(StringComparer.Ordinal);
    // Match Raycast's default camera refresh cadence — short enough that
    // re-opening the page shows recent video, long enough to dedupe back-
    // to-back GetItems calls on the same page render.
    private static readonly TimeSpan CameraSnapshotTtl = TimeSpan.FromSeconds(5);

    private readonly object _pictureCacheLock = new();
    private readonly Dictionary<string, (string Url, string Path, DateTime FetchedAtUtc)> _pictureCache = new(StringComparer.Ordinal);
    // Avatars / entity pictures change rarely. A long TTL keeps repeat
    // page renders cheap; if the picture is updated in HA it'll surface on
    // the next CmdPal restart.
    private static readonly TimeSpan EntityPictureTtl = TimeSpan.FromMinutes(15);

    // Single Jinja template that returns a JSON object mapping each
    // entity_id to its area name. HA's `area_name(entity_id)` walks
    // entity → device → area in the registry, matching the resolution
    // Raycast does over WebSocket. Dict comprehension + tojson is the
    // cleanest variant: no commas to manage, and the output is real JSON.
    private const string AreaMapTemplate =
        "{{ {s.entity_id: area_name(s.entity_id) for s in states if area_name(s.entity_id)} | tojson }}";

    /// <summary>
    /// Diagnostic for the most recent area map fetch:
    ///   -1 = never attempted / failed (template errored or network blip)
    ///    0 = HA responded but no entities have areas assigned
    ///   >0 = number of entities with an area in HA
    /// Read by <see cref="HomeAssistantCommandPalette.Pages.EntityListPage"/>
    /// to surface the right "no area" hint.
    /// </summary>
    public int LastAreaCount { get; private set; } = -1;

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

        lock (_cacheLock)
        {
            if (_cachedResult is { HasError: false } && DateTime.UtcNow - _cachedAtUtc < CacheTtl)
            {
                return _cachedResult;
            }
        }

        var result = FetchStates();

        lock (_cacheLock)
        {
            _cachedResult = result;
            _cachedAtUtc = DateTime.UtcNow;
            return result;
        }
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

        lock (_cameraCacheLock)
        {
            if (_cameraCache.TryGetValue(entityId, out var entry)
                && DateTime.UtcNow - entry.FetchedAtUtc < CameraSnapshotTtl
                && File.Exists(entry.Path))
            {
                return entry.Path;
            }
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

            lock (_cameraCacheLock)
            {
                _cameraCache[entityId] = (path, DateTime.UtcNow);
            }
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

        lock (_pictureCacheLock)
        {
            if (_pictureCache.TryGetValue(entityId, out var entry)
                && entry.Url == entityPicture
                && DateTime.UtcNow - entry.FetchedAtUtc < EntityPictureTtl
                && File.Exists(entry.Path))
            {
                return entry.Path;
            }
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

            lock (_pictureCacheLock)
            {
                _pictureCache[entityId] = (entityPicture, path, DateTime.UtcNow);
            }
            return path;
        }
        catch
        {
            return null;
        }
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
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? Read(string name) => root.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            return new HaConfigProbe(true, HaErrorKind.None, null,
                Read("version"), Read("location_name"), Read("time_zone"), Read("state"),
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
            lock (_cacheLock)
            {
                _cachedResult = null;
            }
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
            default: writer.WriteString(key, value.ToString() ?? string.Empty); break;
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
        lock (_areaCacheLock)
        {
            if (_cachedAreaMap is not null && DateTime.UtcNow - _areaCachedAtUtc < AreaCacheTtl)
            {
                return _cachedAreaMap;
            }
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
                CacheAreas(null);
                return null;
            }
            var raw = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();

            var map = ParseAreaMap(raw);
            CacheAreas(map);
            return map;
        }
        catch
        {
            CacheAreas(null);
            return null;
        }
    }

    private static Dictionary<string, string> ParseAreaMap(string raw)
    {
        // /api/template returns the rendered string. HA wraps the body in
        // JSON quotes some versions, leaves it bare in others — peel one
        // string layer if present.
        var trimmed = raw.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            try { trimmed = JsonDocument.Parse(trimmed).RootElement.GetString() ?? trimmed; }
            catch { /* leave as-is */ }
        }

        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "{ }" || trimmed == "{}") return map;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return map;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    var area = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(area)) map[prop.Name] = area;
                }
            }
        }
        catch (JsonException) { /* HA returned something we can't parse — fall through to empty */ }

        return map;
    }

    private void CacheAreas(Dictionary<string, string>? map)
    {
        lock (_areaCacheLock)
        {
            _cachedAreaMap = map;
            _areaCachedAtUtc = DateTime.UtcNow;
            LastAreaCount = map is null ? -1 : map.Count;
        }
    }

    private static List<HaEntity> ParseStates(string json)
    {
        var list = new List<HaEntity>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var entityId = el.TryGetProperty("entity_id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(entityId)) continue;

            var state = el.TryGetProperty("state", out var stEl) ? stEl.GetString() ?? string.Empty : string.Empty;

            var attrs = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (el.TryGetProperty("attributes", out var attrEl) && attrEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in attrEl.EnumerateObject())
                {
                    attrs[prop.Name] = ToObject(prop.Value);
                }
            }

            list.Add(new HaEntity
            {
                EntityId = entityId,
                State = state,
                Attributes = attrs,
                LastChanged = ReadDate(el, "last_changed"),
                LastUpdated = ReadDate(el, "last_updated"),
            });
        }

        list.Sort((a, b) => string.Compare(a.FriendlyName, b.FriendlyName, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    private static DateTimeOffset? ReadDate(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.String) return null;
        return DateTimeOffset.TryParse(prop.GetString(), out var dt) ? dt : null;
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
    }
}
