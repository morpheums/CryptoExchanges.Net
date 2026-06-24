using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Bybit.Streaming;

/// <summary>
/// Per-exchange protocol strategy for the Bybit v5 public spot WebSocket endpoint
/// (<c>wss://stream.bybit.com/v5/public/spot</c>). Implements the subscribe/unsubscribe
/// wire format, frame classification, and heartbeat policy. Pure data + classification —
/// no timers or threads (binding constraint C1).
/// </summary>
/// <remarks>
/// <para>
/// Bybit v5 WebSocket confirmed specifics (verified against Bybit v5 WS API docs):
/// <list type="bullet">
///   <item><description><strong>Heartbeat</strong>: Bybit sends a server-side WebSocket
///   control Ping every ~20 s; <c>ClientWebSocket</c> auto-pongs RFC 6455 control Pong
///   frames. Protocol uses <see cref="HeartbeatDirection.ServerPingClientPong"/>,
///   Interval = 20 s, Timeout = 60 s.</description></item>
///   <item><description><strong>Order-book depth levels</strong>: 1, 50, 200 (spot).
///   Default depth = 50 (<c>orderbook.50.BTCUSDT</c>).</description></item>
///   <item><description><strong>Batch cap</strong>: 100 topics per frame (Bybit allows many
///   args per frame; 100 matches the engine pre-chunk cap).</description></item>
///   <item><description><strong>MinOutboundInterval</strong>: 100 ms (10 msg/s; conservative
///   per-venue pacing aligned with KuCoin).</description></item>
///   <item><description><strong>Snapshot vs delta</strong>: both classify as
///   <see cref="FrameKind.Data"/> on the same <c>topic</c> routing key — the decoder
///   handles shape differences (no local-book maintenance required).</description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class BybitStreamProtocol : IStreamProtocol
{
    // Confirmed: Bybit v5 public spot sends server-side control Ping every 20 s.
    // ClientWebSocket handles the Pong automatically (RFC 6455 control frames).
    private static readonly HeartbeatPolicy s_heartbeatPolicy = new(
        Direction: HeartbeatDirection.ServerPingClientPong,
        Interval: TimeSpan.FromSeconds(20),
        Timeout: TimeSpan.FromSeconds(60));

    // Default order-book depth (Bybit v5 public spot supports 1, 50, 200).
    private const int DefaultOrderBookDepth = 50;

    // Cached once — Bybit v5 public spot uses a static URL and static heartbeat policy.
    private readonly StreamConnectionInfo _connectionInfo;

    private int _nextReqId;

    /// <summary>
    /// Initialises the protocol with the streaming base URL from options.
    /// The resolved <see cref="StreamConnectionInfo"/> is cached in the constructor
    /// and returned on every <see cref="ResolveConnectionAsync"/> call (static URL — no token negotiation).
    /// </summary>
    /// <param name="options">Stream options supplying the base URL.</param>
    public BybitStreamProtocol(BybitStreamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var endpoint = new Uri(options.StreamBaseUrl);
        // Bybit v5 public spot: conservative 100 ms pacing (10 msg/s).
        _connectionInfo = new StreamConnectionInfo(
            endpoint,
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
        // Returns the same venue-native topic that Classify reads from the "topic" field
        // of a Bybit v5 data frame (e.g. "tickers.BTCUSDT", "orderbook.50.BTCUSDT").
        // Single-sourcing through BuildTopic ensures subscribe-time and receive-time keyspaces agree.
        return BuildTopic(request);
    }

    /// <inheritdoc/>
    public string BuildSubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var topic = BuildTopic(request);
        var reqId = Interlocked.Increment(ref _nextReqId);
        return $"{{\"req_id\":\"{reqId}\",\"op\":\"subscribe\",\"args\":[\"{topic}\"]}}";
    }

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var topic = BuildTopic(request);
        var reqId = Interlocked.Increment(ref _nextReqId);
        return $"{{\"req_id\":\"{reqId}\",\"op\":\"unsubscribe\",\"args\":[\"{topic}\"]}}";
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

            // Data frames carry a "topic" field and a "type" field ("snapshot" or "delta").
            // Both snapshot and delta classify as Data on the same topic routing key.
            if (root.TryGetProperty("topic"u8, out var topicProp) &&
                root.TryGetProperty("type"u8, out _))
            {
                var routingKey = topicProp.GetString();
                return new StreamFrame(FrameKind.Data, routingKey);
            }

            // Pong reply when Bybit v5 server responds to a client JSON ping:
            // {"success":true,"ret_msg":"pong","op":"pong","req_id":"...",...}
            if (root.TryGetProperty("op"u8, out var opProp))
            {
                var op = opProp.GetString();
                if (string.Equals(op, "pong", StringComparison.Ordinal))
                    return new StreamFrame(FrameKind.Pong, null);

                // Subscribe/unsubscribe ack: {"success":true,"ret_msg":"subscribe","op":"subscribe",...}
                if (root.TryGetProperty("success"u8, out var successProp))
                {
                    return successProp.GetBoolean()
                        ? new StreamFrame(FrameKind.Ack, null)
                        : new StreamFrame(FrameKind.Error, null);
                }
            }

            // Unrecognised frame shape.
            return new StreamFrame(FrameKind.Error, null);
        }
        catch (JsonException)
        {
            return new StreamFrame(FrameKind.Error, null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // One subscribe/unsubscribe frame with a multi-topic args array.
    // Bybit v5 allows many topics per frame; cap matches the engine pre-chunk limit (100).
    private string? BuildBatch(IReadOnlyList<StreamRequest> requests, string op)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return null;

        var builder = new StringBuilder(op.Length + requests.Count * 24 + 48);
        var reqId = Interlocked.Increment(ref _nextReqId);
        builder.Append("{\"req_id\":\"").Append(reqId).Append("\",\"op\":\"").Append(op).Append("\",\"args\":[");
        for (var i = 0; i < requests.Count; i++)
        {
            if (i > 0)
                builder.Append(',');
            builder.Append('"').Append(BuildTopic(requests[i])).Append('"');
        }
        builder.Append("]}");
        return builder.ToString();
    }

    /// <summary>
    /// Builds the Bybit v5 venue-native topic string from a canonical <see cref="StreamRequest"/>.
    /// This is the single source of truth for both <see cref="RoutingKeyFor"/> and
    /// <see cref="Classify"/>. Symbol wire format: UPPERCASE, no delimiter (per <c>BybitSymbolFormat</c>).
    /// </summary>
    /// <remarks>
    /// Kline interval mapping: canonical <see cref="KlineInterval"/> enum names → Bybit codes
    /// <c>1 3 5 15 30 60 120 240 360 720 D W M</c>.
    /// Order-book default depth = 50 (confirmed available levels: 1, 50, 200 on Bybit v5 spot).
    /// </remarks>
    private static string BuildTopic(StreamRequest request) => request.Kind switch
    {
        StreamKind.Ticker => $"tickers.{request.WireSymbol}",
        StreamKind.Trade => $"publicTrade.{request.WireSymbol}",
        StreamKind.OrderBook when request.Depth.HasValue => $"orderbook.{request.Depth}.{request.WireSymbol}",
        StreamKind.OrderBook => $"orderbook.{DefaultOrderBookDepth}.{request.WireSymbol}",
        StreamKind.Kline when request.Interval is not null =>
            $"kline.{MapInterval(request.Interval)}.{request.WireSymbol}",
        StreamKind.Kline => $"kline.1.{request.WireSymbol}",
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind,
            $"Unsupported stream kind: {request.Kind}")
    };

    /// <summary>
    /// Maps the canonical <see cref="KlineInterval"/> enum name to the Bybit v5 wire interval code.
    /// Bybit v5 spot kline intervals: <c>1 3 5 15 30 60 120 240 360 720 D W M</c>.
    /// </summary>
    private static string MapInterval(string intervalToken) => intervalToken switch
    {
        nameof(KlineInterval.OneMinute) => "1",
        nameof(KlineInterval.ThreeMinutes) => "3",
        nameof(KlineInterval.FiveMinutes) => "5",
        nameof(KlineInterval.FifteenMinutes) => "15",
        nameof(KlineInterval.ThirtyMinutes) => "30",
        nameof(KlineInterval.OneHour) => "60",
        nameof(KlineInterval.TwoHours) => "120",
        nameof(KlineInterval.FourHours) => "240",
        nameof(KlineInterval.SixHours) => "360",
        nameof(KlineInterval.TwelveHours) => "720",
        nameof(KlineInterval.OneDay) => "D",
        nameof(KlineInterval.OneWeek) => "W",
        nameof(KlineInterval.OneMonth) => "M",
        _ => throw new ArgumentOutOfRangeException(nameof(intervalToken), intervalToken,
            $"Unsupported interval: {intervalToken}")
    };
}
