namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Pure-data record describing the heartbeat policy for a venue's WebSocket connection.
/// The protocol <em>describes</em> the policy; the engine <em>executes</em> it (timers,
/// watchdog, send, pong — binding constraint C1). No timers, threads, or behavioral
/// methods live here.
/// </summary>
/// <param name="Direction">
/// Whether the venue sends pings and the engine sends pongs, or the engine sends
/// periodic client-initiated pings.
/// </param>
/// <param name="Interval">
/// The client-ping cadence (for <see cref="HeartbeatDirection.ClientPing"/>) or the
/// expected server-ping interval (used by the engine's liveness watchdog).
/// </param>
/// <param name="Timeout">
/// The liveness watchdog threshold: if no heartbeat signal is received within this
/// duration the engine treats the connection as dead and initiates a reconnect.
/// </param>
/// <param name="ClientPingPayload">
/// The raw bytes to send as the ping payload. Relevant only for
/// <see cref="HeartbeatDirection.ClientPing"/>. Defaults to an empty
/// <see cref="ReadOnlyMemory{T}"/> (zero bytes).
/// </param>
/// <param name="PingFormat">
/// The wire format used when the engine sends client-initiated pings.
/// Defaults to <see cref="PingFormat.ControlFrame"/>. Ignored for
/// <see cref="HeartbeatDirection.ServerPingClientPong"/>.
/// </param>
internal sealed record HeartbeatPolicy(
    HeartbeatDirection Direction,
    TimeSpan Interval,
    TimeSpan Timeout,
    ReadOnlyMemory<byte> ClientPingPayload = default,
    PingFormat PingFormat = PingFormat.ControlFrame);
