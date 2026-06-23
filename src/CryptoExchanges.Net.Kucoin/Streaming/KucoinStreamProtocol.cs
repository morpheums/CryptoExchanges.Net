using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Kucoin.Streaming;

/// <summary>
/// Per-exchange protocol strategy for the KuCoin public WebSocket streams.
/// Performs bullet-public token negotiation per connect, builds subscribe/unsubscribe
/// wire JSON, classifies incoming frames, and describes the heartbeat policy.
/// Pure data + classification — no timers or threads (binding constraint C1).
/// </summary>
internal sealed class KucoinStreamProtocol : IStreamProtocol
{
    private readonly IKucoinBulletPublicClient _bulletClient;
    private long _nextId;

    /// <summary>Initialises the protocol with the bullet-public client used to negotiate the WebSocket token.</summary>
    /// <param name="bulletClient">The bullet-public negotiation client.</param>
    public KucoinStreamProtocol(IKucoinBulletPublicClient bulletClient)
    {
        ArgumentNullException.ThrowIfNull(bulletClient);
        _bulletClient = bulletClient;
    }

    /// <inheritdoc />
    public async ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)
    {
        var bullet = await _bulletClient.NegotiateAsync(ct).ConfigureAwait(false);

        if (bullet.InstanceServers.Count == 0)
            throw new InvalidOperationException("bullet-public response returned no instance servers.");

        var server = bullet.InstanceServers[0];
        ValidateWsEndpoint(server.Endpoint);

        var connectId = Guid.NewGuid().ToString("N");
        var uri = new Uri($"{server.Endpoint.TrimEnd('/')}?token={Uri.EscapeDataString(bullet.Token)}&connectId={connectId}");

        var pingInterval = TimeSpan.FromMilliseconds(server.PingInterval);
        var pingTimeout = TimeSpan.FromMilliseconds(server.PingTimeout);

        // KuCoin ping payload uses a unix-ms timestamp as the message ID.
        var pingTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var pingPayload = Encoding.UTF8.GetBytes($"{{\"id\":\"{pingTs}\",\"type\":\"ping\"}}");

        var heartbeat = new HeartbeatPolicy(
            Direction: HeartbeatDirection.ClientPing,
            Interval: pingInterval,
            Timeout: pingTimeout,
            ClientPingPayload: pingPayload,
            PingFormat: PingFormat.Json);

        // bullet-public carries no rate-limit field; 100 ms (10 msg/s) stays well inside KuCoin's limits.
        return new StreamConnectionInfo(uri, heartbeat, MinOutboundInterval: TimeSpan.FromMilliseconds(100));
    }

    /// <inheritdoc />
    public string BuildSubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var topic = BuildTopic(request);
        var id = Interlocked.Increment(ref _nextId);
        return $"{{\"id\":\"{id}\",\"type\":\"subscribe\",\"topic\":\"{topic}\",\"privateChannel\":false,\"response\":true}}";
    }

    /// <inheritdoc />
    public string BuildUnsubscribe(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var topic = BuildTopic(request);
        var id = Interlocked.Increment(ref _nextId);
        return $"{{\"id\":\"{id}\",\"type\":\"unsubscribe\",\"topic\":\"{topic}\",\"privateChannel\":false,\"response\":true}}";
    }

    /// <inheritdoc />
    public string? BuildSubscribeBatch(IReadOnlyList<StreamRequest> requests)
        => BuildBatch(requests, "subscribe");

    /// <inheritdoc />
    public string? BuildUnsubscribeBatch(IReadOnlyList<StreamRequest> requests)
        => BuildBatch(requests, "unsubscribe");

    /// <inheritdoc />
    public string RoutingKeyFor(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return BuildTopic(request);
    }

    /// <inheritdoc />
    public StreamFrame Classify(ReadOnlySpan<byte> frame)
    {
        if (frame.IsEmpty)
            return new StreamFrame(FrameKind.Error, null);

        try
        {
            var reader = new Utf8JsonReader(frame);
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type"u8, out var typeProp))
                return new StreamFrame(FrameKind.Error, null);

            var type = typeProp.GetString();
            return type switch
            {
                "message" => ClassifyDataFrame(root),
                "ack" => new StreamFrame(FrameKind.Ack, null),
                "pong" => new StreamFrame(FrameKind.Pong, null),
                "error" => new StreamFrame(FrameKind.Error, null),
                // "welcome" on connect, "notice" system notices — treat as Ack so the engine discards them.
                "welcome" => new StreamFrame(FrameKind.Ack, null),
                _ => new StreamFrame(FrameKind.Error, null)
            };
        }
        catch (JsonException)
        {
            return new StreamFrame(FrameKind.Error, null);
        }
    }

    private static StreamFrame ClassifyDataFrame(JsonElement root)
    {
        if (!root.TryGetProperty("topic"u8, out var topicProp))
            return new StreamFrame(FrameKind.Error, null);

        var routingKey = topicProp.GetString();
        return new StreamFrame(FrameKind.Data, routingKey);
    }

    // One frame joining symbols under a shared channel prefix (e.g. "/market/level2:BTC-USDT,ETH-USDT").
    // Only valid for a single channel, so a mixed-channel set returns null (engine falls back per-frame).
    private string? BuildBatch(IReadOnlyList<StreamRequest> requests, string type)
    {
        ArgumentNullException.ThrowIfNull(requests);
        if (requests.Count == 0)
            return null;

        var firstTopic = BuildTopic(requests[0]);
        var colon = firstTopic.LastIndexOf(':');
        var channelPrefix = firstTopic[..colon];

        var symbols = new StringBuilder();
        symbols.Append(firstTopic.AsSpan(colon + 1));
        for (var i = 1; i < requests.Count; i++)
        {
            var topic = BuildTopic(requests[i]);
            var sep = topic.LastIndexOf(':');
            if (!topic.AsSpan(0, sep).SequenceEqual(channelPrefix))
                return null;

            symbols.Append(',');
            symbols.Append(topic.AsSpan(sep + 1));
        }

        var id = Interlocked.Increment(ref _nextId);
        return $"{{\"id\":\"{id}\",\"type\":\"{type}\",\"topic\":\"{channelPrefix}:{symbols}\",\"privateChannel\":false,\"response\":true}}";
    }

    /// <summary>
    /// Builds the KuCoin venue topic string for the given subscription request.
    /// </summary>
    private static string BuildTopic(StreamRequest request) => request.Kind switch
    {
        StreamKind.Ticker => $"/market/snapshot:{request.WireSymbol}",
        StreamKind.Trade => $"/market/match:{request.WireSymbol}",
        StreamKind.OrderBook => $"/market/level2:{request.WireSymbol}",
        StreamKind.Kline when request.Interval is not null =>
            $"/market/candles:{request.WireSymbol}_{MapInterval(request.Interval)}",
        StreamKind.Kline => $"/market/candles:{request.WireSymbol}_1min",
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind,
            $"Unsupported stream kind: {request.Kind}")
    };

    /// <summary>
    /// Maps a canonical <see cref="KlineInterval"/> enum name to the KuCoin wire interval notation.
    /// </summary>
    private static string MapInterval(string intervalToken) => intervalToken switch
    {
        nameof(KlineInterval.OneMinute) => "1min",
        nameof(KlineInterval.ThreeMinutes) => "3min",
        nameof(KlineInterval.FiveMinutes) => "5min",
        nameof(KlineInterval.FifteenMinutes) => "15min",
        nameof(KlineInterval.ThirtyMinutes) => "30min",
        nameof(KlineInterval.OneHour) => "1hour",
        nameof(KlineInterval.TwoHours) => "2hour",
        nameof(KlineInterval.FourHours) => "4hour",
        nameof(KlineInterval.SixHours) => "6hour",
        nameof(KlineInterval.EightHours) => "8hour",
        nameof(KlineInterval.TwelveHours) => "12hour",
        nameof(KlineInterval.OneDay) => "1day",
        nameof(KlineInterval.OneWeek) => "1week",
        nameof(KlineInterval.OneMonth) => "1month",
        nameof(KlineInterval.ThreeDays) => "3day",
        _ => throw new ArgumentOutOfRangeException(nameof(intervalToken), intervalToken,
            $"Unsupported interval: {intervalToken}")
    };

    /// <summary>SSRF guard: only <c>wss://</c> URIs on <c>*.kucoin.com</c> or <c>kucoin.com</c> are accepted.</summary>
    private static void ValidateWsEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed))
            throw new InvalidOperationException(
                $"bullet-public returned an invalid endpoint URI: '{endpoint}'.");

        if (!string.Equals(parsed.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"bullet-public endpoint must use the 'wss' scheme; got '{parsed.Scheme}'.");

        var host = parsed.Host;
        var isKucoinHost = string.Equals(host, "kucoin.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".kucoin.com", StringComparison.OrdinalIgnoreCase);

        if (!isKucoinHost)
            throw new InvalidOperationException(
                $"bullet-public endpoint host '{host}' is not a trusted KuCoin host.");
    }
}
