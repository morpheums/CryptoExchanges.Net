namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Immutable record carrying the resolved WebSocket endpoint URI, the heartbeat
/// policy, and the outbound-frame pacing floor for a single connection attempt. Returned by
/// <see cref="IStreamProtocol.ResolveConnectionAsync"/> before every connect and reconnect.
/// </summary>
/// <remarks>
/// Constraint K1: this record carries ONLY venue/transport policy data —
/// <see cref="Uri"/>, <see cref="HeartbeatPolicy"/>, and <see cref="TimeSpan"/> pacing.
/// No <c>Core.Models</c> and no DeltaMapper references are permitted under
/// <c>CryptoExchanges.Net.Http</c>. The engine stays byte/opaque.
/// </remarks>
/// <param name="Endpoint">
/// The WebSocket endpoint URI to connect to. For venues that require token-negotiated
/// connections (e.g. KuCoin bullet-public) this URI includes all required query parameters;
/// it is resolved fresh on every connect/reconnect so a new token is embedded each time.
/// </param>
/// <param name="Heartbeat">
/// The heartbeat policy for the connection. The engine executes the timers, watchdog,
/// send, and pong logic from this policy (binding constraint C1 — the protocol only
/// describes the policy; no timers or threads belong in the protocol).
/// </param>
/// <param name="MinOutboundInterval">
/// Minimum spacing the engine enforces between outbound control frames, honouring the venue's
/// inbound message-rate limit. <see cref="TimeSpan.Zero"/> (the default) means unthrottled.
/// </param>
internal sealed record StreamConnectionInfo(
    Uri Endpoint,
    HeartbeatPolicy Heartbeat,
    TimeSpan MinOutboundInterval = default);
