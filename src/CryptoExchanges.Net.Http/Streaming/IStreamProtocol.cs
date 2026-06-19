namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Per-exchange protocol strategy injected into the generic reconnecting byte-engine.
/// Supplies venue-specific knowledge: the WebSocket endpoint, subscribe/unsubscribe
/// wire text, frame classification, and heartbeat policy.
/// </summary>
/// <remarks>
/// <para>
/// Implemented as a sealed class per exchange (invariant 11: prefer injected interfaces
/// over static classes with behavior). The engine resolves this via DI — never calls
/// <c>new XxxStreamProtocol()</c> directly.
/// </para>
/// <para>
/// <strong>Binding constraint C1</strong>: the protocol <em>describes</em> heartbeat
/// (policy data + frame classification); the engine <em>executes</em> it (timers,
/// watchdog, send, pong). No timers, threads, or <c>StartHeartbeat</c>-style behavioral
/// methods belong here.
/// </para>
/// </remarks>
internal interface IStreamProtocol
{
    /// <summary>The WebSocket endpoint URI for this venue.</summary>
    Uri Endpoint { get; }

    /// <summary>
    /// Builds the subscribe request text to send over the socket for the given
    /// <paramref name="request"/>. The format is venue-specific (flat-string, JSON object, etc.).
    /// </summary>
    /// <param name="request">The subscription descriptor.</param>
    /// <returns>The wire-format subscribe payload as a UTF-8 string.</returns>
    string BuildSubscribe(StreamRequest request);

    /// <summary>
    /// Builds the unsubscribe request text to send over the socket for the given
    /// <paramref name="request"/>. The engine calls this before removing the request from
    /// the stored subscribe set (binding constraint K2: only acknowledged unsubscribes
    /// are removed from the replay set so a reconnect does not resurface them).
    /// </summary>
    /// <param name="request">The subscription descriptor to cancel.</param>
    /// <returns>The wire-format unsubscribe payload as a UTF-8 string.</returns>
    string BuildUnsubscribe(StreamRequest request);

    /// <summary>
    /// Classifies a raw received frame into an engine-actionable <see cref="StreamFrame"/>.
    /// <list type="bullet">
    ///   <item><see cref="FrameKind.Data"/> — routable market-data frame; <see cref="StreamFrame.RoutingKey"/> identifies the target subscription.</item>
    ///   <item><see cref="FrameKind.Ack"/> — subscribe/unsubscribe echo; the engine discards it.</item>
    ///   <item><see cref="FrameKind.Pong"/> — venue liveness response; the engine resets its watchdog timer.</item>
    ///   <item><see cref="FrameKind.Error"/> — error or unrecognised frame; the engine may force a reconnect.</item>
    /// </list>
    /// </summary>
    /// <param name="frame">The raw frame bytes as received from the socket.</param>
    /// <returns>The classified <see cref="StreamFrame"/>.</returns>
    StreamFrame Classify(ReadOnlySpan<byte> frame);

    /// <summary>
    /// The heartbeat policy for this venue. The engine reads this once on connection
    /// and executes the timers, watchdog, and send/pong logic accordingly.
    /// </summary>
    HeartbeatPolicy Heartbeat { get; }
}
