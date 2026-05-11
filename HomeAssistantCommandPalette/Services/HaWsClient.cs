using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HomeAssistantCommandPalette.Models;

namespace HomeAssistantCommandPalette.Services;

/// <summary>
/// Long-lived Home Assistant WebSocket client. Connects to
/// <c>/api/websocket</c>, authenticates, performs a one-shot
/// <c>get_states</c> hydration, then subscribes to the
/// <c>state_changed</c> event stream and maintains an in-memory snapshot
/// keyed by entity_id. Reconnects with exponential backoff on any failure.
/// </summary>
/// <remarks>
/// The client is fire-and-forget by design: callers read the current
/// snapshot via <see cref="Entities"/> and subscribe to <see cref="StateChanged"/>
/// to know when to re-render. Before <see cref="IsHydrated"/> flips true
/// the dictionary is empty — callers should fall back to REST during the
/// cold-start window.
/// </remarks>
internal sealed partial class HaWsClient : IDisposable
{
    private readonly HaSettings _settings;
    private readonly ConcurrentDictionary<string, HaEntity> _entities = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonDocument>> _pending = new();

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private int _msgId;
    private volatile bool _hydrated;

    // Connection identity at the time of the last (re)start. When settings
    // change, RestHaClient calls Restart() to pick up the new URL/token.
    private string _lastUrl = string.Empty;
    private string _lastToken = string.Empty;
    private bool _lastIgnoreCerts;

    public HaWsClient(HaSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// True once the initial <c>get_states</c> response has populated
    /// <see cref="Entities"/>. Until then the dictionary is empty and
    /// callers should fall back to REST.
    /// </summary>
    public bool IsHydrated => _hydrated;

    /// <summary>
    /// Live view of the current state map. Reads are thread-safe; the
    /// returned reference may mutate as new <c>state_changed</c> events
    /// arrive. Snapshot via <c>.Values.ToList()</c> if you need stability.
    /// </summary>
    public ConcurrentDictionary<string, HaEntity> Entities => _entities;

    /// <summary>
    /// Raised when the in-memory state map mutates. Argument is the
    /// entity_id that changed, or <c>null</c> when the whole map is reset
    /// (initial hydration, reconnect, disconnect). Handlers run on the WS
    /// read loop's thread; keep them cheap.
    /// </summary>
    public event Action<string?>? StateChanged;

    /// <summary>
    /// Spawns the background connect / read loop if it isn't already
    /// running for the current settings. Safe to call repeatedly — calls
    /// after the first are no-ops unless the URL/token changed.
    /// </summary>
    public void EnsureStarted()
    {
        if (!_settings.IsConfigured) return;

        var url = _settings.Url;
        var token = _settings.Token;
        var ignoreCerts = _settings.IgnoreCertificateErrors;

        // Already running for the same connection identity? Leave it alone.
        if (_runTask is { IsCompleted: false }
            && string.Equals(_lastUrl, url, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_lastToken, token, StringComparison.Ordinal)
            && _lastIgnoreCerts == ignoreCerts)
        {
            return;
        }

        StopInternal();

        _lastUrl = url;
        _lastToken = token;
        _lastIgnoreCerts = ignoreCerts;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _runTask = Task.Run(() => RunWithBackoffAsync(ct), ct);
    }

    private void StopInternal()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
        _cts = null;
        _runTask = null;
        _hydrated = false;
        _entities.Clear();
        // Don't fire StateChanged on stop — it'd just trigger a refresh
        // for a snapshot that's about to be replaced.

        // Cancel any in-flight request so the next connection isn't holding
        // ghost TCSes.
        foreach (var kv in _pending)
        {
            if (_pending.TryRemove(kv.Key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }
    }

    private async Task RunWithBackoffAsync(CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);
        var maxBackoff = TimeSpan.FromSeconds(30);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunSessionAsync(ct).ConfigureAwait(false);
                // Clean session exit (server closed) — reset backoff so the
                // next attempt starts fast.
                backoff = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Auth failures, parse errors, network blips — all fall
                // through to backoff + retry. We deliberately don't
                // surface this to the user; REST stays as the fallback.
            }

            // Connection died — invalidate hydration so callers fall back
            // to REST until we re-hydrate.
            _hydrated = false;
            _entities.Clear();
            FireStateChanged(entityId: null);

            try { await Task.Delay(backoff, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }

            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, maxBackoff.Ticks));
        }
    }

    private async Task RunSessionAsync(CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        if (_lastIgnoreCerts)
        {
            // User has explicitly enabled "Ignore TLS certificate errors"
            // in settings — same trade-off the REST path makes. Mirrors
            // Raycast's homeassistant extension default for self-signed LANs.
#pragma warning disable CA5359
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
        }

        var wsUri = BuildWsUri(_lastUrl);
        // Timeout the connect attempt — ConnectAsync can otherwise hang
        // for the OS default (~20 s) on a black-holed host.
        using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            connectCts.CancelAfter(TimeSpan.FromSeconds(10));
            await ws.ConnectAsync(wsUri, connectCts.Token).ConfigureAwait(false);
        }

        // ── Auth handshake ──────────────────────────────────────────
        // Server greets with auth_required → we send auth → expect auth_ok.
        using (var doc = await ReceiveJsonAsync(ws, ct).ConfigureAwait(false))
        {
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type != "auth_required")
            {
                throw new InvalidOperationException($"Expected auth_required, got '{type}'.");
            }
        }

        await SendJsonAsync(ws, w =>
        {
            w.WriteString("type", "auth");
            w.WriteString("access_token", _lastToken);
        }, ct).ConfigureAwait(false);

        using (var doc = await ReceiveJsonAsync(ws, ct).ConfigureAwait(false))
        {
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type == "auth_invalid")
            {
                // Don't retry on bad token — break out and let backoff sit.
                // Token won't fix itself; we wait for the user to update
                // settings (which calls Restart()).
                throw new UnauthorizedAccessException("auth_invalid");
            }
            if (type != "auth_ok")
            {
                throw new InvalidOperationException($"Expected auth_ok, got '{type}'.");
            }
        }

        // ── Hydrate via get_states ───────────────────────────────────
        // We send id-correlated commands but during the handshake nothing's
        // listening yet — kick off the read loop *after* we issue
        // get_states so the response can be matched to its TCS.
        var getStatesId = NextId();
        var getStatesTask = SendCommandAsync(ws, getStatesId, w =>
        {
            w.WriteString("type", "get_states");
        }, ct);

        // Read loop runs concurrently with the rest of this method —
        // it's the only thing that drains the socket from here on.
        var readLoop = Task.Run(() => ReadLoopAsync(ws, ct), ct);

        using (var statesDoc = await getStatesTask.ConfigureAwait(false))
        {
            HydrateFromGetStates(statesDoc.RootElement);
        }
        _hydrated = true;
        FireStateChanged(entityId: null);

        // ── Subscribe to state_changed ──────────────────────────────
        var subId = NextId();
        using (var subDoc = await SendCommandAsync(ws, subId, w =>
        {
            w.WriteString("type", "subscribe_events");
            w.WriteString("event_type", "state_changed");
        }, ct).ConfigureAwait(false))
        {
            // Result is success:true with no body when subscription is
            // accepted; we just need to confirm the result frame arrived.
            if (!subDoc.RootElement.TryGetProperty("success", out var ok) || !ok.GetBoolean())
            {
                throw new InvalidOperationException("subscribe_events failed");
            }
        }

        // Block until the read loop exits (server closed, error, or cancel).
        await readLoop.ConfigureAwait(false);
    }

    private void HydrateFromGetStates(JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var snapshot = new ConcurrentDictionary<string, HaEntity>(StringComparer.Ordinal);
        foreach (var stateEl in result.EnumerateArray())
        {
            var dto = stateEl.Deserialize(HaJsonContext.Default.HaStateDto);
            if (dto is null || string.IsNullOrEmpty(dto.EntityId)) continue;
            snapshot[dto.EntityId] = HaEntityMapper.FromDto(dto);
        }

        // Replace en-masse so a partial hydration can't be observed.
        _entities.Clear();
        foreach (var kv in snapshot)
        {
            _entities[kv.Key] = kv.Value;
        }
    }

    private async Task ReadLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            JsonDocument? doc;
            try
            {
                doc = await ReceiveJsonAsync(ws, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch
            {
                // Read failure ends the session; outer loop will reconnect.
                return;
            }

            try
            {
                DispatchFrame(doc.RootElement);
            }
            finally
            {
                doc.Dispose();
            }
        }
    }

    private void DispatchFrame(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var typeEl)) return;
        var type = typeEl.GetString();

        switch (type)
        {
            case "result":
                // Correlate with the TCS the sender registered.
                if (root.TryGetProperty("id", out var idEl)
                    && _pending.TryRemove(idEl.GetInt32(), out var tcs))
                {
                    // Clone the document so the TCS can outlive the
                    // current frame's JsonDocument lifetime.
                    var bytes = Encoding.UTF8.GetBytes(root.GetRawText());
                    var clone = JsonDocument.Parse(bytes);
                    tcs.TrySetResult(clone);
                }
                break;

            case "event":
                if (!root.TryGetProperty("event", out var ev)) break;
                if (!ev.TryGetProperty("event_type", out var et)) break;
                if (et.GetString() != "state_changed") break;
                if (!ev.TryGetProperty("data", out var data)) break;
                ApplyStateChanged(data);
                break;

            // Anything else (pong, etc.) is ignored.
        }
    }

    private void ApplyStateChanged(JsonElement data)
    {
        var entityId = data.TryGetProperty("entity_id", out var idEl) ? idEl.GetString() : null;
        if (string.IsNullOrEmpty(entityId)) return;

        if (data.TryGetProperty("new_state", out var ns) && ns.ValueKind != JsonValueKind.Null)
        {
            var dto = ns.Deserialize(HaJsonContext.Default.HaStateDto);
            if (dto is not null && !string.IsNullOrEmpty(dto.EntityId))
            {
                // Preserve any AreaName the REST stitch had previously
                // assigned — state_changed events don't carry area info.
                _entities.TryGetValue(entityId, out var prev);
                _entities[entityId] = HaEntityMapper.FromDto(dto, prev?.AreaName);
            }
        }
        else
        {
            // new_state null = entity removed (or unavailable in some HA
            // versions). Drop from the map.
            _entities.TryRemove(entityId, out _);
        }

        FireStateChanged(entityId);
    }

    private void FireStateChanged(string? entityId)
    {
        var handler = StateChanged;
        if (handler is null) return;
        try { handler(entityId); }
        catch { /* never let a subscriber kill the read loop */ }
    }

    public async Task<IReadOnlyList<HaWeatherForecast>> FetchForecastOnceAsync(string entityId, string kind, CancellationToken ct)
    {
        if (!_settings.IsConfigured) return Array.Empty<HaWeatherForecast>();

        using var ws = new ClientWebSocket();
        if (_settings.IgnoreCertificateErrors)
        {
#pragma warning disable CA5359
            ws.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var token = timeout.Token;

        await ws.ConnectAsync(BuildWsUri(_settings.Url), token).ConfigureAwait(false);
        using (var hello = await ReceiveJsonAsync(ws, token).ConfigureAwait(false))
        {
            if (hello.RootElement.GetProperty("type").GetString() != "auth_required") return Array.Empty<HaWeatherForecast>();
        }

        await SendJsonAsync(ws, w =>
        {
            w.WriteString("type", "auth");
            w.WriteString("access_token", _settings.Token);
        }, token).ConfigureAwait(false);
        using (var auth = await ReceiveJsonAsync(ws, token).ConfigureAwait(false))
        {
            if (auth.RootElement.GetProperty("type").GetString() != "auth_ok") return Array.Empty<HaWeatherForecast>();
        }

        var id = NextId();
        await SendJsonAsync(ws, w =>
        {
            w.WriteNumber("id", id);
            w.WriteString("type", "weather/subscribe_forecast");
            w.WriteString("entity_id", entityId);
            w.WriteString("forecast_type", kind);
        }, token).ConfigureAwait(false);

        var subscribed = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var doc = await ReceiveJsonAsync(ws, token).ConfigureAwait(false);
                var root = doc.RootElement;
                if (!root.TryGetProperty("type", out var typeEl)) continue;
                var frameType = typeEl.GetString();
                if (frameType == "result" && root.TryGetProperty("id", out var rid) && rid.GetInt32() == id)
                {
                    subscribed = root.TryGetProperty("success", out var ok) && ok.GetBoolean();
                    if (!subscribed) return Array.Empty<HaWeatherForecast>();
                    continue;
                }

                if (frameType == "event" && root.TryGetProperty("id", out var eid) && eid.GetInt32() == id)
                {
                    var forecastRoot = root.GetProperty("event");
                    if (forecastRoot.TryGetProperty("forecast", out var forecast))
                    {
                        return ParseForecast(forecast);
                    }
                    if (forecastRoot.TryGetProperty("data", out var data) && data.TryGetProperty("forecast", out var nested))
                    {
                        return ParseForecast(nested);
                    }
                }
            }
        }
        finally
        {
            if (subscribed && ws.State == WebSocketState.Open)
            {
                try
                {
                    await SendJsonAsync(ws, w =>
                    {
                        w.WriteNumber("id", NextId());
                        w.WriteString("type", "unsubscribe_events");
                        w.WriteNumber("subscription", id);
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
            }
        }

        return Array.Empty<HaWeatherForecast>();
    }

    private static IReadOnlyList<HaWeatherForecast> ParseForecast(JsonElement forecast)
    {
        if (forecast.ValueKind != JsonValueKind.Array) return Array.Empty<HaWeatherForecast>();
        var items = new List<HaWeatherForecast>();
        foreach (var item in forecast.EnumerateArray())
        {
            DateTimeOffset? time = null;
            if (item.TryGetProperty("datetime", out var dt) && dt.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(dt.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            {
                time = parsed;
            }

            items.Add(new HaWeatherForecast
            {
                Time = time,
                Condition = StringProp(item, "condition") ?? string.Empty,
                Temperature = DoubleProp(item, "temperature"),
                Templow = DoubleProp(item, "templow"),
                Precipitation = DoubleProp(item, "precipitation"),
                PrecipitationProbability = DoubleProp(item, "precipitation_probability"),
                WindSpeed = DoubleProp(item, "wind_speed"),
                WindBearing = DoubleProp(item, "wind_bearing"),
                Humidity = DoubleProp(item, "humidity"),
            });
        }
        return items;
    }

    private static string? StringProp(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? DoubleProp(JsonElement item, string name)
        => item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var v) ? v : null;

    private int NextId() => Interlocked.Increment(ref _msgId);

    private async Task<JsonDocument> SendCommandAsync(
        ClientWebSocket ws,
        int id,
        Action<Utf8JsonWriter> writeBody,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<JsonDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        try
        {
            await SendJsonAsync(ws, w =>
            {
                w.WriteNumber("id", id);
                writeBody(w);
            }, ct).ConfigureAwait(false);

            // 10s ceiling so a wedged HA can't permanently park us in await.
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(TimeSpan.FromSeconds(10));
            using (linked.Token.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }
        catch
        {
            _pending.TryRemove(id, out _);
            throw;
        }
    }

    private static async Task SendJsonAsync(ClientWebSocket ws, Action<Utf8JsonWriter> writeBody, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writeBody(writer);
            writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        await ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
    }

    private static async Task<JsonDocument> ReceiveJsonAsync(ClientWebSocket ws, CancellationToken ct)
    {
        // HA frames are typically a few KB; pool a 16 KB buffer and resize
        // by appending to MemoryStream when a frame happens to be larger
        // (entity registry list can run 100+ KB on big setups).
        var pooled = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            using var ms = new MemoryStream();
            var segment = new ArraySegment<byte>(pooled);
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(segment, ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new IOException("WebSocket closed by server.");
                }
                ms.Write(pooled, 0, result.Count);
            }
            while (!result.EndOfMessage);

            ms.Position = 0;
            return JsonDocument.Parse(ms);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pooled);
        }
    }

    private static Uri BuildWsUri(string baseUrl)
    {
        // baseUrl is normalized in HaSettings — TrimEnd('/'), no path.
        // http → ws, https → wss; anything else throws (settings validation
        // surface). The BCL's Uri ctor is tolerant of mixed cases.
        var scheme = baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "wss"
            : baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "ws"
            : throw new UriFormatException($"URL must start with http:// or https:// (got '{baseUrl}').");

        var hostAndPath = baseUrl[(baseUrl.IndexOf("://", StringComparison.Ordinal) + 3)..];
        return new Uri($"{scheme}://{hostAndPath}/api/websocket");
    }

    public void Dispose()
    {
        StopInternal();
    }
}
