using System.Net.WebSockets;
using System.Text;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Production <see cref="IWebSocketConnection"/> implementation backed by
/// <see cref="System.Net.WebSockets.ClientWebSocket"/>.
/// One instance is created per connect/reconnect attempt by the connection factory
/// (no captive dependency — the factory produces a fresh instance for each connect,
/// matching the engine's own lifecycle).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Control-frame keep-alive</strong>: <see cref="System.Net.WebSockets.ClientWebSocket"/>
/// can only emit data frames (opcodes 0x01 text / 0x02 binary); it cannot emit RFC 6455 control
/// frames (Ping 0x09 / Pong 0x0A) directly. Framework-managed keep-alive / auto-pong is
/// configured via <see cref="System.Net.WebSockets.ClientWebSocketOptions.KeepAliveInterval"/>
/// (set in the constructor). Consequently <see cref="SendPingAsync"/> and
/// <see cref="SendPongAsync"/> send <em>data</em> frames — correct for venues that use
/// application-level heartbeat messages (<see cref="PingFormat.Text"/> /
/// <see cref="PingFormat.Json"/>). For <see cref="PingFormat.ControlFrame"/> the engine
/// must NOT call these methods; framework keep-alive handles the control-frame handshake
/// automatically.
/// </para>
/// </remarks>
internal sealed class ClientWebSocketConnection : IWebSocketConnection
{
    // Buffer used for receive — reused across calls within one connection lifetime.
    private const int ReceiveBufferSize = 8192;

    /// <summary>
    /// Hard ceiling on a single inbound WebSocket message. Frames larger than this indicate
    /// a misbehaving venue or a memory-exhaustion attempt; the receive loop throws rather than
    /// buffering an unbounded payload.
    /// </summary>
    private const int MaxMessageBytes = 4 * 1024 * 1024; // 4 MiB

    private readonly ClientWebSocket _ws;

    /// <summary>
    /// Initialises a new connection with the specified framework keep-alive interval.
    /// </summary>
    /// <param name="keepAliveInterval">
    /// The <see cref="System.Net.WebSockets.ClientWebSocketOptions.KeepAliveInterval"/> passed
    /// to the underlying <see cref="System.Net.WebSockets.ClientWebSocket"/>. The framework uses
    /// this to manage RFC 6455 Ping/Pong control-frame keep-alive automatically.
    /// Defaults to 20 seconds when not specified.
    /// </param>
    public ClientWebSocketConnection(TimeSpan? keepAliveInterval = null)
    {
        _ws = new ClientWebSocket();
        _ws.Options.KeepAliveInterval = keepAliveInterval ?? TimeSpan.FromSeconds(20);
    }

    /// <inheritdoc/>
    public WebSocketState State => _ws.State;

    /// <inheritdoc/>
    public bool IsOpen => _ws.State == WebSocketState.Open;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="uri"/> does not use the <c>ws</c> or <c>wss</c> scheme.
    /// </exception>
    public Task ConnectAsync(Uri uri, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"URI scheme '{uri.Scheme}' is not supported; expected 'ws' or 'wss'.",
                nameof(uri));
        }
        return _ws.ConnectAsync(uri, ct);
    }

    /// <inheritdoc/>
    public Task SendTextAsync(string text, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        return _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct);
    }

    /// <summary>
    /// Sends <paramref name="payload"/> as a binary <em>data</em> frame (opcode 0x02).
    /// </summary>
    /// <remarks>
    /// Note: <see cref="System.Net.WebSockets.ClientWebSocket"/> cannot emit RFC 6455 Ping
    /// control frames (opcode 0x09); this method sends a binary data frame instead.
    /// It is correct for venues using application-level heartbeat messages
    /// (<see cref="PingFormat.Text"/> / <see cref="PingFormat.Json"/>). For
    /// <see cref="PingFormat.ControlFrame"/> the engine relies on the framework
    /// <see cref="System.Net.WebSockets.ClientWebSocketOptions.KeepAliveInterval"/> and must
    /// NOT call this method.
    /// </remarks>
    /// <inheritdoc/>
    public Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
        => _ws.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken: ct).AsTask();

    /// <summary>
    /// Sends <paramref name="payload"/> as a binary <em>data</em> frame (opcode 0x02).
    /// </summary>
    /// <remarks>
    /// Note: <see cref="System.Net.WebSockets.ClientWebSocket"/> cannot emit RFC 6455 Pong
    /// control frames (opcode 0x0A); this method sends a binary data frame instead.
    /// Framework auto-pong (via <see cref="System.Net.WebSockets.ClientWebSocketOptions.KeepAliveInterval"/>)
    /// handles server-initiated Ping / Pong control frames automatically.
    /// </remarks>
    /// <inheritdoc/>
    public Task SendPongAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
        => _ws.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken: ct).AsTask();

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a single inbound message exceeds <see cref="MaxMessageBytes"/> (4 MiB).
    /// </exception>
    public async ValueTask<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken ct)
    {
        using var ms = new System.IO.MemoryStream();
        var buffer = new byte[ReceiveBufferSize];
        ValueWebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);

            if (result.MessageType == WebSocketMessageType.Close)
                return null; // Clean venue-initiated close → engine reconnects.

            if (ms.Length + result.Count > MaxMessageBytes)
                throw new InvalidOperationException(
                    $"WebSocket message exceeded {MaxMessageBytes} bytes.");

            await ms.WriteAsync(buffer.AsMemory(0, result.Count), ct).ConfigureAwait(false);
        }
        while (!result.EndOfMessage);

        return ms.ToArray();
    }

    /// <inheritdoc/>
    public Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken ct)
        => _ws.CloseAsync(status, description, ct);

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ws.Dispose();
        return ValueTask.CompletedTask;
    }
}
