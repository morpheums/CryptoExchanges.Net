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
internal sealed class ClientWebSocketConnection : IWebSocketConnection
{
    // Buffer used for receive — reused across calls within one connection lifetime.
    private const int ReceiveBufferSize = 8192;
    private readonly ClientWebSocket _ws = new();

    /// <inheritdoc/>
    public WebSocketState State => _ws.State;

    /// <inheritdoc/>
    public bool IsOpen => _ws.State == WebSocketState.Open;

    /// <inheritdoc/>
    public Task ConnectAsync(Uri uri, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(uri);
        return _ws.ConnectAsync(uri, ct);
    }

    /// <inheritdoc/>
    public Task SendTextAsync(string text, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        return _ws.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: ct);
    }

    /// <inheritdoc/>
    public async Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
        => await _ws.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken: ct).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task SendPongAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
        => await _ws.SendAsync(payload, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken: ct).ConfigureAwait(false);

    /// <inheritdoc/>
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
