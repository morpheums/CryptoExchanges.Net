using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Bybit.Streaming;

/// <summary>
/// <see cref="IStreamProtocol"/> for the Bybit v5 public spot WebSocket — subscribe/unsubscribe
/// wire format, frame classification, and heartbeat/pacing policy.
/// </summary>
internal sealed class BybitStreamProtocol : IStreamProtocol
{
    private static readonly HeartbeatPolicy s_heartbeatPolicy = new(
        Direction: HeartbeatDirection.ServerPingClientPong,
        Interval: TimeSpan.FromSeconds(20),
        Timeout: TimeSpan.FromSeconds(60));

    private const int DefaultOrderBookDepth = 50;

    private readonly StreamConnectionInfo _connectionInfo;

    private int _nextReqId;

    /// <summary>Caches the connection info from <paramref name="options"/> (static endpoint, no token negotiation).</summary>
    /// <param name="options">Stream options supplying the base URL.</param>
    public BybitStreamProtocol(StreamOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var endpoint = new Uri(options.StreamBaseUrl);
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

            // ValueKind guards below: a wrong-typed field throws InvalidOperationException,
            // which is not a JsonException and would escape the catch.
            if (root.TryGetProperty("topic"u8, out var topicProp) &&
                root.TryGetProperty("type"u8, out _))
            {
                if (topicProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);
                var routingKey = topicProp.GetString();
                return new StreamFrame(FrameKind.Data, routingKey);
            }

            // Control frames: {"op":"pong",...} and the subscribe/unsubscribe ack {"success":bool,"op":...}.
            if (root.TryGetProperty("op"u8, out var opProp))
            {
                if (opProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);
                if (string.Equals(opProp.GetString(), "pong", StringComparison.Ordinal))
                    return new StreamFrame(FrameKind.Pong, null);

                if (root.TryGetProperty("success"u8, out var successProp))
                {
                    if (successProp.ValueKind != JsonValueKind.True &&
                        successProp.ValueKind != JsonValueKind.False)
                        return new StreamFrame(FrameKind.Error, null);
                    return successProp.GetBoolean()
                        ? new StreamFrame(FrameKind.Ack, null)
                        : new StreamFrame(FrameKind.Error, null);
                }
            }

            return new StreamFrame(FrameKind.Error, null);
        }
        catch (JsonException)
        {
            return new StreamFrame(FrameKind.Error, null);
        }
    }

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

    // Venue-native topic — the single source of truth for both RoutingKeyFor and Classify.
    private static string BuildTopic(StreamRequest request) => request.Kind switch
    {
        StreamKind.Ticker => $"tickers.{request.WireSymbol}",
        StreamKind.Trade => $"publicTrade.{request.WireSymbol}",
        StreamKind.OrderBook => $"orderbook.{MapOrderBookDepth(request.Depth)}.{request.WireSymbol}",
        StreamKind.Kline when request.Interval is not null =>
            $"kline.{MapInterval(request.Interval)}.{request.WireSymbol}",
        StreamKind.Kline => $"kline.1.{request.WireSymbol}",
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind,
            $"Unsupported stream kind: {request.Kind}")
    };

    // Bybit v5 spot order books publish only at depths 1/50/200/1000; an unsupported depth
    // yields an invalid topic the venue rejects, so round each request UP to the nearest tier.
    private static int MapOrderBookDepth(int? requested) => requested switch
    {
        null => DefaultOrderBookDepth,
        <= 1 => 1,
        <= 50 => 50,
        <= 200 => 200,
        _ => 1000
    };

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
