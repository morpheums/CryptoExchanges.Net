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
    /// <summary>
    /// Resolves the connection parameters (endpoint URI and heartbeat policy) for the
    /// next connection attempt. The engine calls this method immediately before every
    /// <c>ConnectAsync</c> — both the initial connect and every subsequent reconnect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations may perform I/O (e.g. a token-negotiation HTTP call for
    /// venues that require a short-lived connection token). For static venues the
    /// implementation returns a pre-built <see cref="StreamConnectionInfo"/> via
    /// <see cref="ValueTask.FromResult{TResult}"/> with negligible overhead.
    /// </para>
    /// <para>
    /// <strong>Constraint C1</strong>: the protocol only <em>describes</em> the heartbeat
    /// through the returned <see cref="StreamConnectionInfo.Heartbeat"/>; no timers,
    /// threads, or behavioral methods belong here.
    /// </para>
    /// <para>
    /// If <paramref name="ct"/> is cancelled before resolution completes, an
    /// <see cref="OperationCanceledException"/> must propagate to the engine, which will
    /// abort the connect/reconnect attempt.
    /// </para>
    /// </remarks>
    /// <param name="ct">A cancellation token to abort the resolution.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> whose result is the resolved
    /// <see cref="StreamConnectionInfo"/> for this connection attempt.
    /// </returns>
    ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct);

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
    /// Builds one subscribe frame covering all of <paramref name="requests"/>, or
    /// <see langword="null"/> if the venue cannot batch this set (the engine then falls back to the
    /// per-frame <see cref="BuildSubscribe"/> loop). The engine pre-chunks to one frame's worth.
    /// </summary>
    /// <param name="requests">Descriptors to cover in a single frame.</param>
    /// <returns>The batched subscribe payload, or <see langword="null"/> if unsupported.</returns>
    string? BuildSubscribeBatch(IReadOnlyList<StreamRequest> requests) => null;

    /// <summary>
    /// Unsubscribe counterpart to <see cref="BuildSubscribeBatch"/>; same null-and-prechunk contract.
    /// </summary>
    /// <param name="requests">Descriptors to cancel in a single frame.</param>
    /// <returns>The batched unsubscribe payload, or <see langword="null"/> if unsupported.</returns>
    string? BuildUnsubscribeBatch(IReadOnlyList<StreamRequest> requests) => null;

    /// <summary>
    /// Connection-level frames the engine sends (paced) immediately after every physical
    /// (re)connect, before the subscribe-set replay. Returns frame text only — the engine
    /// executes the sending and pacing (constraint C1). Default none.
    /// </summary>
    /// <returns>The connect-time frame payloads in send order, or an empty list for venues that need none.</returns>
    IReadOnlyList<string> ConnectFrames() => [];

    /// <summary>
    /// Returns the routing key the engine must use to register and look up subscriptions for
    /// the given <paramref name="request"/>. The key produced here must be identical to the
    /// <see cref="StreamFrame.RoutingKey"/> that <see cref="Classify"/> returns for a data frame
    /// belonging to the same stream — both sides share one venue-native keyspace. The engine
    /// calls this method on subscribe and uses <see cref="Classify"/> on receive; they must agree.
    /// </summary>
    /// <param name="request">The subscription descriptor.</param>
    /// <returns>The venue-native routing key (e.g. <c>btcusdt@ticker</c>).</returns>
    string RoutingKeyFor(StreamRequest request);

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
}
