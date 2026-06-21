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

    /// <summary>
    /// Initialises the protocol with the injectable bullet-public client used to negotiate
    /// the WebSocket token on every connection attempt.
    /// </summary>
    /// <param name="bulletClient">The bullet-public negotiation client.</param>
    public KucoinStreamProtocol(IKucoinBulletPublicClient bulletClient)
    {
        ArgumentNullException.ThrowIfNull(bulletClient);
        _bulletClient = bulletClient;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Calls <c>POST /api/v1/bullet-public</c> on every connect/reconnect to obtain a fresh
    /// short-lived token. The returned URI is validated to the expected KuCoin WS host scheme
    /// (SSRF guard: only <c>wss://</c> URIs pointing to <c>*.kucoin.com</c> are accepted).
    /// </remarks>
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

        // Build a timestamped ping payload each connection using the server-returned interval
        // as the message ID. The static template is replaced with a per-connection timestamp.
        var pingTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var pingPayload = Encoding.UTF8.GetBytes($"{{\"id\":\"{pingTs}\",\"type\":\"ping\"}}");

        var heartbeat = new HeartbeatPolicy(
            Direction: HeartbeatDirection.ClientPing,
            Interval: pingInterval,
            Timeout: pingTimeout,
            ClientPingPayload: pingPayload,
            PingFormat: PingFormat.Json);

        return new StreamConnectionInfo(uri, heartbeat);
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
    public string RoutingKeyFor(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // The routing key IS the topic string — the same token that Classify reads from the
        // "topic" field of a KuCoin data frame. Both sides share one venue-native keyspace.
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static StreamFrame ClassifyDataFrame(JsonElement root)
    {
        if (!root.TryGetProperty("topic"u8, out var topicProp))
            return new StreamFrame(FrameKind.Error, null);

        var routingKey = topicProp.GetString();
        return new StreamFrame(FrameKind.Data, routingKey);
    }

    /// <summary>
    /// Builds the KuCoin venue topic string for the given subscription request.
    /// </summary>
    private static string BuildTopic(StreamRequest request) => request.Kind switch
    {
        StreamKind.Ticker => $"/market/ticker:{request.WireSymbol}",
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

    /// <summary>
    /// SSRF guard: only <c>wss://</c> URIs whose host ends with <c>.kucoin.com</c> or is exactly
    /// <c>kucoin.com</c> are accepted. Prevents a compromised negotiation endpoint from redirecting
    /// the client to an attacker-controlled host.
    /// </summary>
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
