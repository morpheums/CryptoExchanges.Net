using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Coinbase.Streaming;

/// <summary><see cref="IStreamProtocol"/> for the Coinbase Advanced Trade public WebSocket (subscribe/unsubscribe, heartbeat, routing).</summary>
internal sealed class CoinbaseStreamProtocol : IStreamProtocol
{
    private static readonly HeartbeatPolicy s_heartbeatPolicy = new(
        Direction: HeartbeatDirection.ServerPingClientPong,
        Interval: TimeSpan.FromSeconds(30),
        Timeout: TimeSpan.FromSeconds(60));

    // Proactive "heartbeats" channel keepalive is not wired (no connect-time protocol hook; the engine only
    // replays the stored subscribe-set). Liveness is handled by the engine's heartbeat watchdog (s_heartbeatPolicy).

    // level2 subscribe frames → push frames arrive as "l2_data"; routing key uses the push channel.
    private const string OrderBookSubscribeChannel = "level2";
    private const string OrderBookPushChannel = "l2_data";

    private readonly StreamConnectionInfo _connectionInfo;

    /// <summary>Caches the connection info from <paramref name="options"/> (static endpoint, no token negotiation).</summary>
    /// <param name="options">Stream options supplying the base URL.</param>
    public CoinbaseStreamProtocol(StreamOptions options)
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
        return BuildRoutingKey(request.Kind, request.WireSymbol);
    }

    /// <inheritdoc/>
    public string BuildSubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (subscribeChannel, productId) = BuildSubscribeChannelAndProductId(request);
        return $"{{\"type\":\"subscribe\",\"product_ids\":[\"{productId}\"],\"channel\":\"{subscribeChannel}\"}}";
    }

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (subscribeChannel, productId) = BuildSubscribeChannelAndProductId(request);
        return $"{{\"type\":\"unsubscribe\",\"product_ids\":[\"{productId}\"],\"channel\":\"{subscribeChannel}\"}}";
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

            // Error or ack: identified by top-level "type" field.
            if (root.TryGetProperty("type"u8, out var typeProp))
            {
                if (typeProp.ValueKind != JsonValueKind.String)
                    return new StreamFrame(FrameKind.Error, null);
                var typeValue = typeProp.GetString();
                if (string.Equals(typeValue, "error", StringComparison.Ordinal))
                    return new StreamFrame(FrameKind.Error, null);
                if (string.Equals(typeValue, "subscriptions", StringComparison.Ordinal))
                    return new StreamFrame(FrameKind.Ack, null);
            }

            if (!root.TryGetProperty("channel"u8, out var channelProp) ||
                channelProp.ValueKind != JsonValueKind.String)
                return new StreamFrame(FrameKind.Error, null);

            var channel = channelProp.GetString()!;

            if (string.Equals(channel, "heartbeats", StringComparison.Ordinal))
                return new StreamFrame(FrameKind.Pong, null);

            if (!root.TryGetProperty("events"u8, out var eventsProp) ||
                eventsProp.ValueKind != JsonValueKind.Array)
                return new StreamFrame(FrameKind.Error, null);

            var productId = ExtractProductId(channel, eventsProp);
            if (productId is null)
                return new StreamFrame(FrameKind.Error, null);

            return new StreamFrame(FrameKind.Data, BuildRoutingKey(channel, productId));
        }
        catch (JsonException)
        {
            return new StreamFrame(FrameKind.Error, null);
        }
    }

    // product_id location differs by channel: l2_data has it directly on the event; others embed
    // it inside a nested array (candles→"candles", ticker→"tickers", market_trades→"trades").
    private static string? ExtractProductId(string pushChannel, JsonElement eventsArray)
    {
        var enumerator = eventsArray.EnumerateArray();
        if (!enumerator.MoveNext())
            return null;

        var firstEvent = enumerator.Current;

        if (string.Equals(pushChannel, "candles", StringComparison.Ordinal))
            return ExtractProductIdFromNestedArray(firstEvent, "candles"u8);

        if (string.Equals(pushChannel, "ticker", StringComparison.Ordinal))
            return ExtractProductIdFromNestedArray(firstEvent, "tickers"u8);

        if (string.Equals(pushChannel, "market_trades", StringComparison.Ordinal))
            return ExtractProductIdFromNestedArray(firstEvent, "trades"u8);

        if (!firstEvent.TryGetProperty("product_id"u8, out var productIdProp) ||
            productIdProp.ValueKind != JsonValueKind.String)
            return null;

        return productIdProp.GetString();
    }

    private static string? ExtractProductIdFromNestedArray(JsonElement eventEl, ReadOnlySpan<byte> arrayPropertyName)
    {
        if (!eventEl.TryGetProperty(arrayPropertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return null;
        var inner = arr.EnumerateArray();
        if (!inner.MoveNext())
            return null;
        if (!inner.Current.TryGetProperty("product_id"u8, out var pid) || pid.ValueKind != JsonValueKind.String)
            return null;
        return pid.GetString();
    }

    private static string BuildRoutingKey(StreamKind kind, string wireSymbol)
        => BuildRoutingKey(MapPushChannel(kind), wireSymbol);

    private static string BuildRoutingKey(string pushChannel, string productId)
        => pushChannel + ":" + productId;

    private static string? BuildBatch(IReadOnlyList<StreamRequest> requests, string type)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return null;

        // Coinbase requires one subscribe frame per channel; batch only when all requests share one channel.
        var byChannel = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var req in requests)
        {
            var (subscribeChannel, productId) = BuildSubscribeChannelAndProductId(req);
            if (!byChannel.TryGetValue(subscribeChannel, out var ids))
            {
                ids = [];
                byChannel[subscribeChannel] = ids;
            }
            ids.Add(productId);
        }

        if (byChannel.Count != 1)
            return null;

        foreach (var kv in byChannel)
            return BuildChannelFrame(type, kv.Key, kv.Value);

        return null;
    }

    private static string BuildChannelFrame(string type, string channel, List<string> productIds)
    {
        var sb = new StringBuilder(64 + productIds.Count * 12);
        sb.Append("{\"type\":\"").Append(type).Append("\",\"product_ids\":[");
        for (var i = 0; i < productIds.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(productIds[i]).Append('"');
        }
        sb.Append("],\"channel\":\"").Append(channel).Append("\"}");
        return sb.ToString();
    }

    private static (string SubscribeChannel, string ProductId) BuildSubscribeChannelAndProductId(StreamRequest request)
        => (MapSubscribeChannel(request.Kind), request.WireSymbol);

    private static string MapSubscribeChannel(StreamKind kind) => kind switch
    {
        StreamKind.Ticker => "ticker",
        StreamKind.Trade => "market_trades",
        StreamKind.OrderBook => OrderBookSubscribeChannel,
        StreamKind.Kline => "candles",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, $"Unsupported stream kind: {kind}")
    };

    private static string MapPushChannel(StreamKind kind) => kind switch
    {
        StreamKind.Ticker => "ticker",
        StreamKind.Trade => "market_trades",
        StreamKind.OrderBook => OrderBookPushChannel,
        StreamKind.Kline => "candles",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, $"Unsupported stream kind: {kind}")
    };
}
