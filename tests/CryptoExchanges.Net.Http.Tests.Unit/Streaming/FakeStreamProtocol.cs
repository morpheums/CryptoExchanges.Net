using CryptoExchanges.Net.Core.Streaming;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// A controllable <see cref="IStreamProtocol"/> test double.
/// By default classifies all frames as Data frames routed to <see cref="NextRoutingKey"/>
/// (which defaults to <c>btcusdt@ticker</c> — the fake's own venue-style key).
/// <para>
/// <see cref="RoutingKeyFor"/> returns the same <see cref="NextRoutingKey"/> value so that
/// the engine's subscribe-time registration and receive-time lookup share the same keyspace.
/// Tests that need a different routing key per subscription should set
/// <see cref="NextRoutingKey"/> before calling <c>SubscribeAsync</c> and before enqueuing
/// frames, exactly as the real protocol would do.
/// </para>
/// </summary>
internal sealed class FakeStreamProtocol : IStreamProtocol
{
    /// <summary>
    /// Routing key returned by <see cref="RoutingKeyFor"/> (for registration) and by
    /// <see cref="Classify"/> (for dispatch). Both sides share this value so frames
    /// reach their subscription. Defaults to <c>btcusdt@ticker</c>.
    /// </summary>
    public string NextRoutingKey { get; set; } = "btcusdt@ticker";

    /// <summary>The kind all frames are classified as. Defaults to <see cref="FrameKind.Data"/>.</summary>
    public FrameKind DefaultKind { get; set; } = FrameKind.Data;

    /// <summary>Heartbeat policy returned by <see cref="Heartbeat"/>. Defaults to a long ServerPingClientPong.</summary>
    public HeartbeatPolicy HeartbeatPolicy { get; set; } = new HeartbeatPolicy(
        Direction: HeartbeatDirection.ServerPingClientPong,
        Interval: TimeSpan.FromSeconds(30),
        Timeout: TimeSpan.FromSeconds(60));

    /// <inheritdoc/>
    public Uri Endpoint { get; } = new Uri("wss://fake.test/ws");

    /// <inheritdoc/>
    public string RoutingKeyFor(StreamRequest request)
        => NextRoutingKey;

    /// <inheritdoc/>
    public string BuildSubscribe(StreamRequest request)
        => $"SUBSCRIBE:{NextRoutingKey}";

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
        => $"UNSUBSCRIBE:{NextRoutingKey}";

    /// <inheritdoc/>
    public StreamFrame Classify(ReadOnlySpan<byte> frame)
        => new(DefaultKind, DefaultKind == FrameKind.Data ? NextRoutingKey : null);

    /// <inheritdoc/>
    public HeartbeatPolicy Heartbeat => HeartbeatPolicy;
}
