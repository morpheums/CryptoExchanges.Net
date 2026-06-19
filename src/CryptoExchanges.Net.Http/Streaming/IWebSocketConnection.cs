using System.Net.WebSockets;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Minimal transport abstraction over a raw WebSocket connection.
/// Encapsulates connect, send (text and control/pong), receive, close, and state
/// so the engine never depends directly on <see cref="System.Net.WebSockets.ClientWebSocket"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam that unit tests replace with a fake (e.g. <c>FakeWebSocketConnection</c>),
/// making the reconnecting engine fully testable without a network. The real
/// <see cref="System.Net.WebSockets.ClientWebSocket"/>-backed implementation ships in TASK-044.
/// </para>
/// <para>
/// Keep the surface minimal and transport-only (bytes in/out + connect/close).
/// No protocol knowledge — no heartbeat logic, no subscribe text, no frame parsing here.
/// </para>
/// </remarks>
internal interface IWebSocketConnection : IAsyncDisposable
{
    /// <summary>
    /// The current state of the underlying WebSocket connection.
    /// </summary>
    WebSocketState State { get; }

    /// <summary>
    /// <see langword="true"/> when <see cref="State"/> is <see cref="WebSocketState.Open"/>.
    /// Convenience shorthand for callers that only need a binary open/not-open check.
    /// </summary>
    bool IsOpen { get; }

    /// <summary>
    /// Opens the WebSocket connection to the given <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The endpoint to connect to.</param>
    /// <param name="ct">A token to cancel the connection attempt.</param>
    Task ConnectAsync(Uri uri, CancellationToken ct);

    /// <summary>
    /// Sends a UTF-8 text message over the open connection.
    /// </summary>
    /// <param name="text">The text payload to send.</param>
    /// <param name="ct">A token to cancel the send.</param>
    Task SendTextAsync(string text, CancellationToken ct);

    /// <summary>
    /// Sends a binary payload as a WebSocket Pong control frame (RFC 6455 §5.5.3).
    /// Used by the engine to respond to server-initiated Ping frames.
    /// </summary>
    /// <param name="payload">The pong payload bytes (typically echoes the ping payload).</param>
    /// <param name="ct">A token to cancel the send.</param>
    Task SendPongAsync(ReadOnlyMemory<byte> payload, CancellationToken ct);

    /// <summary>
    /// Awaits and returns the next complete frame from the connection.
    /// Returns <see langword="null"/> when the connection is closed normally by the venue
    /// (clean close handshake), signalling the engine to attempt a reconnect.
    /// </summary>
    /// <param name="ct">A token to cancel the receive.</param>
    /// <returns>
    /// The raw frame bytes, or <see langword="null"/> if the connection closed cleanly.
    /// </returns>
    ValueTask<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken ct);

    /// <summary>
    /// Initiates a clean WebSocket close handshake with the given <paramref name="status"/>
    /// and <paramref name="description"/>.
    /// </summary>
    /// <param name="status">The close status code.</param>
    /// <param name="description">A human-readable reason for closing.</param>
    /// <param name="ct">A token to cancel the close.</param>
    Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken ct);
}
