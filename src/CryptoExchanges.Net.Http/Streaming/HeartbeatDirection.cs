namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Describes which side initiates the heartbeat for a given venue.
/// The engine reads this from <see cref="HeartbeatPolicy.Direction"/> to decide
/// whether to wait for an incoming ping and respond with a pong, or to send
/// periodic client-initiated pings.
/// </summary>
internal enum HeartbeatDirection
{
    /// <summary>
    /// The venue sends periodic ping frames; the engine responds with pong frames.
    /// Standard WebSocket RFC 6455 control-frame ping/pong.
    /// </summary>
    ServerPingClientPong,

    /// <summary>
    /// The engine sends periodic client-initiated pings on the configured cadence.
    /// Used by venues that expect the client to maintain liveness probes.
    /// </summary>
    ClientPing,
}
