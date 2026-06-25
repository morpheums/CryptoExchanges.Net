using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Kraken.Streaming;

/// <summary>
/// <see cref="IStreamProtocol"/> for the Kraken WS v2 public WebSocket.
/// Subscribe/unsubscribe wire format: <c>{"method":"subscribe","params":{"channel":"ticker","symbol":["BTC/USD"]}}</c>.
/// Heartbeat: client sends JSON <c>{"method":"ping"}</c> every 30 s; server replies <c>{"method":"pong"}</c>.
/// OHLC <c>params</c> include an integer-minutes <c>"interval"</c> field (e.g. <c>1</c> for one-minute bars).
/// Routing key is <c>&lt;channel&gt;:&lt;symbol&gt;</c> in slash-form (e.g. <c>ticker:BTC/USD</c>).
/// </summary>
internal sealed class KrakenStreamProtocol : IStreamProtocol
{
    private static readonly byte[] s_pingBytes = "{\"method\":\"ping\"}"u8.ToArray();

    private static readonly HeartbeatPolicy s_heartbeatPolicy = new(
        Direction: HeartbeatDirection.ClientPing,
        Interval: TimeSpan.FromSeconds(30),
        Timeout: TimeSpan.FromSeconds(60),
        ClientPingPayload: s_pingBytes,
        PingFormat: PingFormat.Json);

    private readonly StreamConnectionInfo _connectionInfo;

    /// <summary>Caches the connection info from <paramref name="options"/> (static Kraken endpoint, no token negotiation).</summary>
    /// <param name="options">Stream options supplying the base URL.</param>
    public KrakenStreamProtocol(StreamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionInfo = new StreamConnectionInfo(
            new Uri(options.StreamBaseUrl),
            s_heartbeatPolicy,
            MinOutboundInterval: TimeSpan.FromMilliseconds(100));
    }

    /// <inheritdoc/>
    public ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)
        => new(_connectionInfo);

    /// <inheritdoc/>
    public string RoutingKeyFor(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BuildRoutingKey(request.Kind, NormalizeV2Symbol(request.WireSymbol));
    }

    /// <inheritdoc/>
    public string BuildSubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BuildMessage("subscribe", request);
    }

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BuildMessage("unsubscribe", request);
    }

    /// <inheritdoc/>
    public string? BuildSubscribeBatch(IReadOnlyList<StreamRequest> requests)
        => BuildBatch(requests, "subscribe");

    /// <inheritdoc/>
    public string? BuildUnsubscribeBatch(IReadOnlyList<StreamRequest> requests)
        => BuildBatch(requests, "unsubscribe");

    /// <inheritdoc/>
    public StreamFrame Classify(ReadOnlySpan<byte> frame)
    {
        if (frame.IsEmpty)
            return new StreamFrame(FrameKind.Error, null);

        try
        {
            var reader = new Utf8JsonReader(frame);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("method"u8, out var methodProp) ||
                methodProp.ValueKind != JsonValueKind.String)
            {
                // No "method" — check if it's a data frame with "channel" + "data".
                return ClassifyDataFrame(root);
            }

            var method = methodProp.GetString();

            if (string.Equals(method, "pong", StringComparison.Ordinal))
                return new StreamFrame(FrameKind.Pong, null);

            if (string.Equals(method, "subscribe", StringComparison.Ordinal) ||
                string.Equals(method, "unsubscribe", StringComparison.Ordinal))
            {
                // success:true → Ack; success:false → Error.
                if (root.TryGetProperty("success"u8, out var successProp) &&
                    successProp.ValueKind == JsonValueKind.True)
                    return new StreamFrame(FrameKind.Ack, null);

                return new StreamFrame(FrameKind.Error, null);
            }

            return new StreamFrame(FrameKind.Error, null);
        }
        catch (JsonException)
        {
            return new StreamFrame(FrameKind.Error, null);
        }
    }

    // Routing key: single-sourced helper used by both RoutingKeyFor and Classify.
    private static string BuildRoutingKey(StreamKind kind, string wireSymbol)
        => MapChannel(kind) + ":" + wireSymbol;

    // WS v2 uses canonical names (BTC, DOGE); REST aliases (XBT, XDG) must be normalized.
    private static string NormalizeV2Symbol(string wireSymbol) =>
        wireSymbol
            .Replace("XBT/", "BTC/", StringComparison.Ordinal)
            .Replace("/XBT", "/BTC", StringComparison.Ordinal)
            .Replace("XDG/", "DOGE/", StringComparison.Ordinal)
            .Replace("/XDG", "/DOGE", StringComparison.Ordinal);

    private static StreamFrame ClassifyDataFrame(JsonElement root)
    {
        if (!root.TryGetProperty("channel"u8, out var channelProp) ||
            channelProp.ValueKind != JsonValueKind.String)
            return new StreamFrame(FrameKind.Error, null);

        if (!root.TryGetProperty("data"u8, out var dataProp) ||
            dataProp.ValueKind != JsonValueKind.Array)
            return new StreamFrame(FrameKind.Error, null);

        var channel = channelProp.GetString()!;

        // Extract the symbol from the first data element.
        var enumerator = dataProp.EnumerateArray();
        if (!enumerator.MoveNext())
            return new StreamFrame(FrameKind.Error, null);

        var first = enumerator.Current;
        if (!first.TryGetProperty("symbol"u8, out var symbolProp) ||
            symbolProp.ValueKind != JsonValueKind.String)
            return new StreamFrame(FrameKind.Error, null);

        var wireSymbol = symbolProp.GetString()!;
        // Derive the routing key using the channel name as the StreamKind proxy.
        // BuildRoutingKey expects a StreamKind, so reconstruct directly from channel+wireSymbol.
        var routingKey = channel + ":" + wireSymbol;
        return new StreamFrame(FrameKind.Data, routingKey);
    }

    private static string BuildMessage(string method, StreamRequest request)
    {
        var channel = MapChannel(request.Kind);
        var v2Symbol = NormalizeV2Symbol(request.WireSymbol);
        var sb = new StringBuilder(128);
        sb.Append("{\"method\":\"").Append(method).Append("\",\"params\":{\"channel\":\"")
          .Append(channel).Append("\",\"symbol\":[\"").Append(v2Symbol).Append("\"]");
        if (request.Kind == StreamKind.Kline)
            sb.Append(",\"interval\":").Append(MapIntervalMinutes(request.Interval));
        sb.Append("}}");
        return sb.ToString();
    }

    private static string? BuildBatch(IReadOnlyList<StreamRequest> requests, string method)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return null;

        // Kraken WS v2 batching: symbol array in params, one channel per frame.
        // If the set mixes channels, return null (engine falls back per-frame).
        var firstKind = requests[0].Kind;
        var firstInterval = requests[0].Interval;
        for (var i = 1; i < requests.Count; i++)
        {
            if (requests[i].Kind != firstKind || requests[i].Interval != firstInterval)
                return null;
        }

        var channel = MapChannel(firstKind);
        var sb = new StringBuilder(method.Length + requests.Count * 16 + 64);
        sb.Append("{\"method\":\"").Append(method).Append("\",\"params\":{\"channel\":\"")
          .Append(channel).Append("\",\"symbol\":[");
        for (var i = 0; i < requests.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append('"').Append(NormalizeV2Symbol(requests[i].WireSymbol)).Append('"');
        }
        sb.Append(']');
        if (firstKind == StreamKind.Kline)
            sb.Append(",\"interval\":").Append(MapIntervalMinutes(firstInterval));
        sb.Append("}}");
        return sb.ToString();
    }

    private static string MapChannel(StreamKind kind) => kind switch
    {
        StreamKind.Ticker => "ticker",
        StreamKind.Trade => "trade",
        StreamKind.OrderBook => "book",
        StreamKind.Kline => "ohlc",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind,
            $"Unsupported stream kind: {kind}")
    };

    private static int MapIntervalMinutes(string? intervalToken) => intervalToken switch
    {
        nameof(KlineInterval.OneMinute) => 1,
        nameof(KlineInterval.ThreeMinutes) => 3,
        nameof(KlineInterval.FiveMinutes) => 5,
        nameof(KlineInterval.FifteenMinutes) => 15,
        nameof(KlineInterval.ThirtyMinutes) => 30,
        nameof(KlineInterval.OneHour) => 60,
        nameof(KlineInterval.TwoHours) => 120,
        nameof(KlineInterval.FourHours) => 240,
        nameof(KlineInterval.OneDay) => 1440,
        nameof(KlineInterval.OneWeek) => 10080,
        null or "" => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(intervalToken), intervalToken,
            $"Unsupported interval: {intervalToken}")
    };
}
