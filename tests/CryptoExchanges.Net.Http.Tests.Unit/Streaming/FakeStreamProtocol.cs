using CryptoExchanges.Net.Core.Streaming;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// A controllable <see cref="IStreamProtocol"/> test double.
/// By default classifies all frames as Data frames routed to <see cref="NextRoutingKey"/>
/// (which defaults to <c>BTCUSDT@TICKER</c>, the default subscribe key used in tests).
/// </summary>
internal sealed class FakeStreamProtocol : IStreamProtocol
{
    /// <summary>Routing key returned for all Data frames. Defaults to BTCUSDT@TICKER.</summary>
    public string NextRoutingKey { get; set; } = "BTCUSDT@TICKER";

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
    public string BuildSubscribe(StreamRequest request)
        => $"SUBSCRIBE:{StreamEngine.BuildRoutingKey(request)}";

    /// <inheritdoc/>
    public string BuildUnsubscribe(StreamRequest request)
        => $"UNSUBSCRIBE:{StreamEngine.BuildRoutingKey(request)}";

    /// <inheritdoc/>
    public StreamFrame Classify(ReadOnlySpan<byte> frame)
        => new(DefaultKind, DefaultKind == FrameKind.Data ? NextRoutingKey : null);

    /// <inheritdoc/>
    public HeartbeatPolicy Heartbeat => HeartbeatPolicy;
}
