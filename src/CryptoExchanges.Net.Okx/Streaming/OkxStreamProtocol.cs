using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Okx.Streaming;

/// <summary>
/// <see cref="IStreamProtocol"/> for the OKX v5 public spot WebSocket.
/// Subscribe/unsubscribe wire format: <c>{"op":"subscribe","args":[{"channel":"tickers","instId":"BTC-USDT"}]}</c>.
/// Heartbeat: client sends text <c>"ping"</c> every 25 s; server replies bare-text <c>"pong"</c>.
/// Order-book channel: <c>books5</c> (top-5 levels, confirmed OKX v5 public docs).
/// </summary>
internal sealed class OkxStreamProtocol : IStreamProtocol
{
    private static readonly byte[] s_pingBytes = "ping"u8.ToArray();

    private static readonly HeartbeatPolicy s_heartbeatPolicy = new(
        Direction: HeartbeatDirection.ClientPing,
        Interval: TimeSpan.FromSeconds(25),
        Timeout: TimeSpan.FromSeconds(35),
        ClientPingPayload: s_pingBytes,
        PingFormat: PingFormat.Text);

    private readonly StreamConnectionInfo _connectionInfo;

    /// <summary>Caches the connection info from <paramref name="options"/> (static OKX endpoint, no token negotiation).</summary>
    /// <param name="options">Stream options supplying the base URL.</param>
    public OkxStreamProtocol(StreamOptions options)
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
        return BuildRoutingKey(request);
    }

    /// <inheritdoc/>
    public string BuildSubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (channel, instId) = BuildChannelAndInstId(request);
        return $"{{\"op\":\"subscribe\",\"args\":[{{\"channel\":\"{channel}\",\"instId\":\"{instId}\"}}]}}";
    }

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (channel, instId) = BuildChannelAndInstId(request);
        return $"{{\"op\":\"unsubscribe\",\"args\":[{{\"channel\":\"{channel}\",\"instId\":\"{instId}\"}}]}}";
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

        // OKX server replies with bare-text "pong" (not JSON) to the client "ping".
        if (frame.SequenceEqual("pong"u8))
            return new StreamFrame(FrameKind.Pong, null);

        try
        {
            var reader = new Utf8JsonReader(frame);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (root.TryGetProperty("arg"u8, out var argProp) &&
                root.TryGetProperty("data"u8, out _))
            {
                if (argProp.ValueKind != JsonValueKind.Object)
                    return new StreamFrame(FrameKind.Error, null);
                if (!argProp.TryGetProperty("channel"u8, out var channelProp) ||
                    channelProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);
                if (!argProp.TryGetProperty("instId"u8, out var instIdProp) ||
                    instIdProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);

                var routingKey = channelProp.GetString() + ":" + instIdProp.GetString();
                return new StreamFrame(FrameKind.Data, routingKey);
            }

            if (root.TryGetProperty("event"u8, out var eventProp))
            {
                if (eventProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);
                var ev = eventProp.GetString();
                if (string.Equals(ev, "subscribe", StringComparison.Ordinal) ||
                    string.Equals(ev, "unsubscribe", StringComparison.Ordinal))
                    return new StreamFrame(FrameKind.Ack, null);
                if (string.Equals(ev, "error", StringComparison.Ordinal))
                    return new StreamFrame(FrameKind.Error, null);
            }

            return new StreamFrame(FrameKind.Error, null);
        }
        catch (JsonException)
        {
            return new StreamFrame(FrameKind.Error, null);
        }
    }

    private static string BuildRoutingKey(StreamRequest request)
    {
        var (channel, instId) = BuildChannelAndInstId(request);
        return channel + ":" + instId;
    }

    private static string? BuildBatch(IReadOnlyList<StreamRequest> requests, string op)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return null;

        var sb = new StringBuilder(op.Length + requests.Count * 48 + 32);
        sb.Append("{\"op\":\"").Append(op).Append("\",\"args\":[");
        for (var i = 0; i < requests.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            var (channel, instId) = BuildChannelAndInstId(requests[i]);
            sb.Append("{\"channel\":\"").Append(channel).Append("\",\"instId\":\"").Append(instId).Append("\"}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private static (string Channel, string InstId) BuildChannelAndInstId(StreamRequest request) =>
        (MapChannel(request), request.WireSymbol);

    private static string MapChannel(StreamRequest request) => request.Kind switch
    {
        StreamKind.Ticker => "tickers",
        StreamKind.Trade => "trades",
        StreamKind.OrderBook => "books5",
        StreamKind.Kline when request.Interval is not null => "candle" + MapInterval(request.Interval),
        StreamKind.Kline => "candle1m",
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind,
            $"Unsupported stream kind: {request.Kind}")
    };

    private static string MapInterval(string intervalToken) => intervalToken switch
    {
        nameof(KlineInterval.OneMinute) => "1m",
        nameof(KlineInterval.ThreeMinutes) => "3m",
        nameof(KlineInterval.FiveMinutes) => "5m",
        nameof(KlineInterval.FifteenMinutes) => "15m",
        nameof(KlineInterval.ThirtyMinutes) => "30m",
        nameof(KlineInterval.OneHour) => "1H",
        nameof(KlineInterval.TwoHours) => "2H",
        nameof(KlineInterval.FourHours) => "4H",
        nameof(KlineInterval.SixHours) => "6H",
        nameof(KlineInterval.TwelveHours) => "12H",
        nameof(KlineInterval.OneDay) => "1D",
        nameof(KlineInterval.OneWeek) => "1W",
        nameof(KlineInterval.OneMonth) => "1M",
        _ => throw new ArgumentOutOfRangeException(nameof(intervalToken), intervalToken,
            $"Unsupported interval: {intervalToken}")
    };
}
