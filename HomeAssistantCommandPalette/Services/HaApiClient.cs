using System;
using System.Collections.Generic;
using System.IO;
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
            using var content = BuildEntityIdPayload(entityId);

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

    // Build {"entity_id":"..."} without going through JsonSerializer's reflection
    // path — keeps the assembly trim/AOT clean.
    private static ByteArrayContent BuildEntityIdPayload(string entityId)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("entity_id", entityId);
            writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        var payload = new ByteArrayContent(bytes);
        payload.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return payload;
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
        _ => el.GetRawText(),
    };

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
