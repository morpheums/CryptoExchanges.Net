using CryptoExchanges.Net.Binance.Internal;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Binance.Streaming;

/// <summary>
/// Per-exchange protocol strategy for the venue WebSocket combined-stream endpoint.
/// Implements the subscribe/unsubscribe wire format, frame classification, and
/// heartbeat policy (server-ping / client-pong control frame). Pure data + classification
/// — no timers or threads (binding constraint C1).
/// </summary>
internal sealed class BinanceStreamProtocol : IStreamProtocol
{
    private static readonly HeartbeatPolicy s_heartbeatPolicy = new(
        Direction: HeartbeatDirection.ServerPingClientPong,
        Interval: TimeSpan.FromSeconds(20),
        Timeout: TimeSpan.FromSeconds(60));

    // Cached once in the constructor — Binance uses a static URL and static heartbeat policy.
    private readonly StreamConnectionInfo _connectionInfo;

    private int _nextId;

    /// <summary>
    /// Initialises the protocol with the streaming base URL from options.
    /// The combined-stream path (<c>/stream</c>) is appended automatically.
    /// The resolved <see cref="StreamConnectionInfo"/> is cached in the constructor
    /// and returned on every <see cref="ResolveConnectionAsync"/> call.
    /// </summary>
    /// <param name="options">Stream options supplying the base URL.</param>
    public BinanceStreamProtocol(BinanceStreamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var endpoint = new Uri(options.StreamBaseUrl.TrimEnd('/') + "/stream");
        // Binance closes the socket with PolicyViolation above 5 inbound msgs/sec; 200 ms between
        // control frames yields 5 msg/s with margin.
        _connectionInfo = new StreamConnectionInfo(
            endpoint,
            s_heartbeatPolicy,
            MinOutboundInterval: TimeSpan.FromMilliseconds(200));
    }

    /// <inheritdoc/>
    public ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)
        => new(_connectionInfo);

    /// <inheritdoc/>
    public string RoutingKeyFor(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Returns the same venue-native token that Classify reads from the "stream" field
        // of a combined-stream data frame (e.g. "btcusdt@ticker", "btcusdt@depth20",
        // "btcusdt@kline_1m"). Single-sourcing both sides through BuildStreamToken ensures
        // subscribe-time registration and receive-time lookup share one keyspace.
        return BuildStreamToken(request);
    }

    /// <inheritdoc/>
    public string BuildSubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = BuildStreamToken(request);
        var id = Interlocked.Increment(ref _nextId);
        return $"{{\"method\":\"SUBSCRIBE\",\"params\":[\"{token}\"],\"id\":{id}}}";
    }

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var token = BuildStreamToken(request);
        var id = Interlocked.Increment(ref _nextId);
        return $"{{\"method\":\"UNSUBSCRIBE\",\"params\":[\"{token}\"],\"id\":{id}}}";
    }

    /// <inheritdoc/>
    public StreamFrame Classify(ReadOnlySpan<byte> frame)
    {
        if (frame.IsEmpty)
            return new StreamFrame(FrameKind.Error, null);

        // Parse via Utf8JsonReader directly from the span — no intermediate array allocation
        // on the hot path (avoids the ToArray() alloc in the original implementation).
        var reader = new Utf8JsonReader(frame);
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Combined-stream data frames carry a "stream" field + "data" field.
        if (root.TryGetProperty("stream"u8, out var streamProp) &&
            root.TryGetProperty("data"u8, out _))
        {
            // The routing key is the full stream token, e.g. "btcusdt@ticker".
            var routingKey = streamProp.GetString();
            return new StreamFrame(FrameKind.Data, routingKey);
        }

        // Subscribe/unsubscribe acknowledgement: { "result": null, "id": N }
        if (root.TryGetProperty("result"u8, out var resultProp) &&
            root.TryGetProperty("id"u8, out _) &&
            resultProp.ValueKind == JsonValueKind.Null)
        {
            return new StreamFrame(FrameKind.Ack, null);
        }

        // Error response: { "code": -1, "msg": "..." }
        if (root.TryGetProperty("code"u8, out _) &&
            root.TryGetProperty("msg"u8, out _))
        {
            return new StreamFrame(FrameKind.Error, null);
        }

        // Unrecognised frame shape.
        return new StreamFrame(FrameKind.Error, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the venue stream-name token (e.g. <c>"btcusdt@ticker"</c>) from a
    /// canonical <see cref="StreamRequest"/>. The wire symbol is lower-cased per
    /// the combined-stream naming convention.
    /// </summary>
    private static string BuildStreamToken(StreamRequest request)
    {
        var symbol = request.WireSymbol.ToLowerInvariant();
        return request.Kind switch
        {
            StreamKind.Ticker => $"{symbol}@ticker",
            StreamKind.Trade => $"{symbol}@trade",
            StreamKind.OrderBook when request.Depth.HasValue => $"{symbol}@depth{request.Depth}",
            StreamKind.OrderBook => $"{symbol}@depth",
            StreamKind.Kline when request.Interval is not null =>
                $"{symbol}@kline_{MapInterval(request.Interval)}",
            StreamKind.Kline => $"{symbol}@kline_1m",
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, $"Unsupported stream kind: {request.Kind}")
        };
    }

    /// <summary>
    /// Maps the canonical <see cref="KlineInterval"/> enum name (from <c>StreamRequest.Interval</c>)
    /// to the venue wire notation (e.g. <c>"OneMinute"</c> → <c>"1m"</c>).
    /// </summary>
    private static string MapInterval(string intervalToken) => intervalToken switch
    {
        nameof(KlineInterval.OneMinute) => "1m",
        nameof(KlineInterval.ThreeMinutes) => "3m",
        nameof(KlineInterval.FiveMinutes) => "5m",
        nameof(KlineInterval.FifteenMinutes) => "15m",
        nameof(KlineInterval.ThirtyMinutes) => "30m",
        nameof(KlineInterval.OneHour) => "1h",
        nameof(KlineInterval.TwoHours) => "2h",
        nameof(KlineInterval.FourHours) => "4h",
        nameof(KlineInterval.SixHours) => "6h",
        nameof(KlineInterval.EightHours) => "8h",
        nameof(KlineInterval.TwelveHours) => "12h",
        nameof(KlineInterval.OneDay) => "1d",
        nameof(KlineInterval.ThreeDays) => "3d",
        nameof(KlineInterval.OneWeek) => "1w",
        nameof(KlineInterval.OneMonth) => "1M",
        _ => throw new ArgumentOutOfRangeException(nameof(intervalToken), intervalToken, $"Unsupported interval: {intervalToken}")
    };
}
