namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Specifies the wire format the engine uses when sending a client-initiated ping
/// (relevant only when <see cref="HeartbeatDirection.ClientPing"/> is active).
/// </summary>
internal enum PingFormat
{
    /// <summary>
    /// A standard WebSocket RFC 6455 Ping control frame. The venue responds with
    /// a Pong control frame handled automatically by most WebSocket implementations.
    /// </summary>
    ControlFrame,

    /// <summary>
    /// A plain-text WebSocket message (opcode 0x01) carrying the ping payload.
    /// Used by venues that treat heartbeats as application-level text messages.
    /// </summary>
    Text,

    /// <summary>
    /// A JSON-encoded WebSocket message (opcode 0x01) carrying the ping payload.
    /// Used by venues that define a JSON envelope for their heartbeat request.
    /// </summary>
    Json,
}
