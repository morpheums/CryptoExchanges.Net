using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using CryptoExchanges.Net.Http.Streaming;

namespace CryptoExchanges.Net.Http.Tests.Unit.Streaming;

/// <summary>
/// A controllable test double for <see cref="IWebSocketConnection"/>.
/// Emits canned frames on demand, captures sent messages, and can simulate
/// clean disconnects or reconnects — all without a network.
/// </summary>
public sealed class FakeWebSocketConnection : IWebSocketConnection
{
    private readonly ConcurrentQueue<ReadOnlyMemory<byte>?> _inbound = new();
    private readonly SemaphoreSlim _available = new(0);

    /// <summary>Messages sent via <see cref="SendTextAsync"/> in order.</summary>
    public List<string> SentText { get; } = [];

    /// <summary>Pong payloads sent via <see cref="SendPongAsync"/> in order.</summary>
    public List<ReadOnlyMemory<byte>> SentPongs { get; } = [];

    /// <summary>
    /// Number of times <see cref="ConnectAsync"/> has been called.
    /// Use to assert reconnect behaviour without a network.
    /// </summary>
    public int ConnectCount { get; private set; }

    /// <inheritdoc/>
    public WebSocketState State { get; private set; } = WebSocketState.None;

    /// <inheritdoc/>
    public bool IsOpen => State == WebSocketState.Open;

    /// <inheritdoc/>
    public Task ConnectAsync(Uri uri, CancellationToken ct)
    {
        ConnectCount++;
        State = WebSocketState.Open;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendTextAsync(string text, CancellationToken ct)
    {
        SentText.Add(text);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendPongAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        SentPongs.Add(payload);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken ct)
    {
        await _available.WaitAsync(ct).ConfigureAwait(false);
        _inbound.TryDequeue(out var frame);
        return frame;
    }

    /// <inheritdoc/>
    public Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken ct)
    {
        State = WebSocketState.Closed;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        State = WebSocketState.Closed;
        _available.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Test-control helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Enqueues a canned text frame (UTF-8 encoded) for the next <see cref="ReceiveAsync"/> call.
    /// </summary>
    public void EnqueueFrame(string text)
    {
        _inbound.Enqueue(Encoding.UTF8.GetBytes(text));
        _available.Release();
    }

    /// <summary>
    /// Enqueues raw binary frame bytes for the next <see cref="ReceiveAsync"/> call.
    /// </summary>
    public void EnqueueFrame(ReadOnlyMemory<byte> bytes)
    {
        _inbound.Enqueue(bytes);
        _available.Release();
    }

    /// <summary>
    /// Enqueues a <see langword="null"/> frame to simulate a clean venue-initiated close.
    /// The engine interprets a <see langword="null"/> return from <see cref="ReceiveAsync"/>
    /// as a signal to attempt a reconnect.
    /// </summary>
    public void SimulateDisconnect()
    {
        State = WebSocketState.Closed;
        _inbound.Enqueue(null);
        _available.Release();
    }

    /// <summary>
    /// Resets the fake to an open state so the engine can reconnect after a simulated disconnect.
    /// Does not clear sent-message history so callers can assert across the reconnect boundary.
    /// </summary>
    public void SimulateReconnect()
    {
        State = WebSocketState.Open;
    }
}
