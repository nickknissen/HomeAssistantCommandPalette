using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HomeAssistantCommandPalette.Tests.Fakes;

/// <summary>
/// In-process Home Assistant stand-in: the REST endpoints the extension
/// calls plus enough of the WebSocket protocol to hydrate
/// <c>HaWsClient</c>. Lets a test drive the real client end to end and
/// time it, with the instance size and per-endpoint latency dialled in.
/// </summary>
internal sealed class FakeHaServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly string _statesJson;
    private readonly string _areaMapJson;

    public string Url { get; }

    /// <summary>Delay applied before answering /api/states.</summary>
    public TimeSpan StatesLatency { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Delay applied before answering /api/template. Models the Jinja
    /// render the area map asks for, which walks the whole registry.
    /// </summary>
    public TimeSpan TemplateLatency { get; set; } = TimeSpan.Zero;

    /// <summary>Delay before the WS get_states result frame.</summary>
    public TimeSpan WsHydrateLatency { get; set; } = TimeSpan.Zero;

    public int StatesRequests;
    public int TemplateRequests;

    public FakeHaServer(int entityCount, int port)
    {
        Url = $"http://localhost:{port}";
        _listener.Prefixes.Add($"{Url}/");
        _statesJson = BuildStatesJson(entityCount);
        _areaMapJson = BuildAreaMapJson(entityCount);
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Size of the /api/states payload, for reporting.</summary>
    public int StatesPayloadBytes => Encoding.UTF8.GetByteCount(_statesJson);

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        try
        {
            if (ctx.Request.IsWebSocketRequest)
            {
                var ws = await ctx.AcceptWebSocketAsync(subProtocol: null);
                await RunWebSocketAsync(ws.WebSocket);
                return;
            }

            var path = ctx.Request.Url?.AbsolutePath ?? string.Empty;
            switch (path)
            {
                case "/api/states":
                    Interlocked.Increment(ref StatesRequests);
                    if (StatesLatency > TimeSpan.Zero) await Task.Delay(StatesLatency);
                    await WriteAsync(ctx, _statesJson);
                    break;

                case "/api/template":
                    Interlocked.Increment(ref TemplateRequests);
                    if (TemplateLatency > TimeSpan.Zero) await Task.Delay(TemplateLatency);
                    await WriteAsync(ctx, _areaMapJson);
                    break;

                case "/api/services":
                    await WriteAsync(ctx, "[]");
                    break;

                case "/api/config":
                    await WriteAsync(ctx, """{"version":"2026.6.0","location_name":"Fake","time_zone":"UTC","state":"RUNNING"}""");
                    break;

                default:
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                    break;
            }
        }
        catch
        {
            try { ctx.Response.Abort(); } catch { }
        }
    }

    private static async Task WriteAsync(HttpListenerContext ctx, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }

    // auth_required → auth → auth_ok → get_states result → subscribe result.
    private async Task RunWebSocketAsync(WebSocket ws)
    {
        try
        {
            await SendAsync(ws, """{"type":"auth_required","ha_version":"2026.6.0"}""");

            while (ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var frame = await ReceiveAsync(ws);
                if (frame is null) return;

                using var doc = JsonDocument.Parse(frame);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                var id = root.TryGetProperty("id", out var i) ? i.GetInt32() : 0;

                switch (type)
                {
                    case "auth":
                        await SendAsync(ws, """{"type":"auth_ok","ha_version":"2026.6.0"}""");
                        break;

                    case "get_states":
                        if (WsHydrateLatency > TimeSpan.Zero) await Task.Delay(WsHydrateLatency);
                        await SendAsync(ws, $$"""{"id":{{id}},"type":"result","success":true,"result":{{_statesJson}}}""");
                        break;

                    default:
                        await SendAsync(ws, $$"""{"id":{{id}},"type":"result","success":true,"result":null}""");
                        break;
                }
            }
        }
        catch
        {
            // Client went away — nothing to do.
        }
    }

    private static async Task SendAsync(WebSocket ws, string json)
        => await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

    private static async Task<string?> ReceiveAsync(WebSocket ws)
    {
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    // Attribute shape mirrors a real instance closely enough for payload
    // size and parse cost to be representative.
    private static string BuildStatesJson(int count)
    {
        var sb = new StringBuilder(count * 400);
        sb.Append('[');
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            var numeric = i % 100 < 45;
            var domain = numeric ? "sensor" : (i % 5) switch
            {
                0 => "light",
                1 => "switch",
                2 => "binary_sensor",
                3 => "automation",
                _ => "media_player",
            };
            var state = numeric ? (20 + (i % 10)).ToString(System.Globalization.CultureInfo.InvariantCulture) : "on";
            sb.Append("{\"entity_id\":\"").Append(domain).Append(".entity_").Append(i)
              .Append("\",\"state\":\"").Append(state)
              .Append("\",\"attributes\":{\"friendly_name\":\"Entity number ").Append(i)
              .Append(" with a reasonably long name\",\"unit_of_measurement\":\"°C\",\"device_class\":\"temperature\",")
              .Append("\"state_class\":\"measurement\",\"icon\":\"mdi:thermometer\",\"supported_features\":0,")
              .Append("\"editable\":false,\"attribution\":\"Data provided by a fake integration\"},")
              .Append("\"last_changed\":\"2026-08-18T09:00:00.000000+00:00\",")
              .Append("\"last_updated\":\"2026-08-18T09:00:00.000000+00:00\",")
              .Append("\"context\":{\"id\":\"01J000000000000000000").Append(i % 10)
              .Append("\",\"parent_id\":null,\"user_id\":null}}");
        }
        sb.Append(']');
        return sb.ToString();
    }

    // /api/template returns the rendered string: [[entity_id, area], ...]
    private static string BuildAreaMapJson(int count)
    {
        var sb = new StringBuilder(count * 40);
        sb.Append('[');
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(',');
            var numeric = i % 100 < 45;
            var domain = numeric ? "sensor" : (i % 5) switch
            {
                0 => "light",
                1 => "switch",
                2 => "binary_sensor",
                3 => "automation",
                _ => "media_player",
            };
            sb.Append($"[\"{domain}.entity_{i}\",\"Area {i % 20}\"]");
        }
        sb.Append(']');
        return sb.ToString();
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        _listener.Close();
        _cts.Dispose();
    }
}
