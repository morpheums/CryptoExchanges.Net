using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Bitget.Streaming;

/// <summary>
/// <see cref="IStreamProtocol"/> for the Bitget v2 public spot WebSocket.
/// Subscribe/unsubscribe frame format uses <c>{"op":"subscribe","args":[{"instType":"SPOT","channel":"...","instId":"..."}]}</c>.
/// Heartbeat: client sends text <c>"ping"</c> every 30 s; server replies with text <c>"pong"</c>.
/// Order-book channels: <c>books5</c> (5 levels, default), <c>books15</c> (15 levels), <c>books</c> (full depth).
/// Kline channels: <c>candle1m</c>, <c>candle5m</c>, … <c>candle1D</c>, <c>candle1W</c>, <c>candle1M</c>.
/// Routing key: <c>channel:instId</c> (e.g. <c>ticker:BTCUSDT</c>), shared by <see cref="RoutingKeyFor"/> and <see cref="Classify"/>.
/// <c>MinOutboundInterval</c> = 100 ms (10 msg/s conservative floor).
/// </summary>
internal sealed class BitgetStreamProtocol : IStreamProtocol
{
    private static readonly ReadOnlyMemory<byte> s_pingPayload = "ping"u8.ToArray();

    private static readonly HeartbeatPolicy s_heartbeatPolicy = new(
        Direction: HeartbeatDirection.ClientPing,
        Interval: TimeSpan.FromSeconds(30),
        Timeout: TimeSpan.FromSeconds(90),
        ClientPingPayload: s_pingPayload,
        PingFormat: PingFormat.Text);

    private const string DefaultOrderBookChannel = "books5";

    private readonly StreamConnectionInfo _connectionInfo;

    /// <summary>Caches the connection info from <paramref name="options"/> (static endpoint, no token negotiation).</summary>
    /// <param name="options">Stream options supplying the base URL.</param>
    public BitgetStreamProtocol(StreamOptions options)
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
        return BuildRoutingKey(request);
    }

    /// <inheritdoc/>
    public string BuildSubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (channel, instId) = ResolveChannelAndInstId(request);
        return $"{{\"op\":\"subscribe\",\"args\":[{{\"instType\":\"SPOT\",\"channel\":\"{channel}\",\"instId\":\"{instId}\"}}]}}";
    }

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (channel, instId) = ResolveChannelAndInstId(request);
        return $"{{\"op\":\"unsubscribe\",\"args\":[{{\"instType\":\"SPOT\",\"channel\":\"{channel}\",\"instId\":\"{instId}\"}}]}}";
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

        // Bare text "pong" reply from Bitget heartbeat — not valid JSON, check before parse.
        if (frame.SequenceEqual("pong"u8))
            return new StreamFrame(FrameKind.Pong, null);

        try
        {
            var reader = new Utf8JsonReader(frame);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (root.TryGetProperty("action"u8, out _) &&
                root.TryGetProperty("arg"u8, out var argProp) &&
                root.TryGetProperty("data"u8, out _))
            {
                if (argProp.ValueKind != JsonValueKind.Object)
                    return new StreamFrame(FrameKind.Error, null);

                if (!argProp.TryGetProperty("channel"u8, out var chanProp) ||
                    !argProp.TryGetProperty("instId"u8, out var instIdProp))
                    return new StreamFrame(FrameKind.Error, null);

                if (chanProp.ValueKind != JsonValueKind.String ||
                    instIdProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);

                var key = $"{chanProp.GetString()}:{instIdProp.GetString()}";
                return new StreamFrame(FrameKind.Data, key);
            }

            if (root.TryGetProperty("event"u8, out var eventProp))
            {
                if (eventProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);

                var eventStr = eventProp.GetString();
                if (string.Equals(eventStr, "error", StringComparison.Ordinal))
                    return new StreamFrame(FrameKind.Error, null);

                if (root.TryGetProperty("code"u8, out var codeProp))
                {
                    if (codeProp.ValueKind != JsonValueKind.String)
                        return new StreamFrame(FrameKind.Error, null);
                    var code = codeProp.GetString();
                    return string.Equals(code, "0", StringComparison.Ordinal)
                        ? new StreamFrame(FrameKind.Ack, null)
                        : new StreamFrame(FrameKind.Error, null);
                }

                return new StreamFrame(FrameKind.Error, null);
            }

            return new StreamFrame(FrameKind.Error, null);
        }
        catch (JsonException)
        {
            return new StreamFrame(FrameKind.Error, null);
        }
    }

    private static string? BuildBatch(IReadOnlyList<StreamRequest> requests, string op)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return null;

        var builder = new StringBuilder(op.Length + requests.Count * 60 + 32);
        builder.Append("{\"op\":\"").Append(op).Append("\",\"args\":[");
        for (var i = 0; i < requests.Count; i++)
        {
            if (i > 0)
                builder.Append(',');
            var (channel, instId) = ResolveChannelAndInstId(requests[i]);
            builder.Append("{\"instType\":\"SPOT\",\"channel\":\"")
                   .Append(channel)
                   .Append("\",\"instId\":\"")
                   .Append(instId)
                   .Append("\"}");
        }
        builder.Append("]}");
        return builder.ToString();
    }

    private static string BuildRoutingKey(StreamRequest request)
    {
        var (channel, instId) = ResolveChannelAndInstId(request);
        return $"{channel}:{instId}";
    }

    private static (string channel, string instId) ResolveChannelAndInstId(StreamRequest request) =>
        request.Kind switch
        {
            StreamKind.Ticker => ("ticker", request.WireSymbol),
            StreamKind.Trade => ("trade", request.WireSymbol),
            StreamKind.OrderBook when request.Depth.HasValue => (MapDepthChannel(request.Depth.Value), request.WireSymbol),
            StreamKind.OrderBook => (DefaultOrderBookChannel, request.WireSymbol),
            StreamKind.Kline when request.Interval is not null => ($"candle{MapInterval(request.Interval)}", request.WireSymbol),
            StreamKind.Kline => ("candle1m", request.WireSymbol),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind,
                $"Unsupported stream kind: {request.Kind}")
        };

    private static string MapDepthChannel(int depth) => depth switch
    {
        5 => "books5",
        15 => "books15",
        _ => "books",
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
        nameof(KlineInterval.EightHours) => "8H",
        nameof(KlineInterval.TwelveHours) => "12H",
        nameof(KlineInterval.OneDay) => "1D",
        nameof(KlineInterval.ThreeDays) => "3D",
        nameof(KlineInterval.OneWeek) => "1W",
        nameof(KlineInterval.OneMonth) => "1M",
        _ => throw new ArgumentOutOfRangeException(nameof(intervalToken), intervalToken,
            $"Unsupported interval: {intervalToken}")
    };
}
