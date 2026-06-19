namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Discriminates the engine-visible kind of a received WebSocket frame.
/// The engine acts on <see cref="Data"/> (routes to a subscription channel),
/// skips <see cref="Ack"/> and <see cref="Pong"/>, and surfaces <see cref="Error"/>
/// for reconnect decisions.
/// </summary>
internal enum FrameKind
{
    /// <summary>A market-data frame carrying a routable payload.</summary>
    Data,

    /// <summary>An exchange acknowledgement (subscribe/unsubscribe echo); the engine discards these.</summary>
    Ack,

    /// <summary>
    /// A pong response (control-frame or text/JSON pong). Returned by
    /// <see cref="IStreamProtocol.Classify"/> when the frame satisfies the venue's liveness response;
    /// the engine resets its watchdog timer.
    /// </summary>
    Pong,

    /// <summary>
    /// An error or unknown frame from the venue. The engine uses this signal to decide
    /// whether to force a reconnect.
    /// </summary>
    Error,
}
