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
/// <remarks>
/// All public members are thread-safe: the engine's receive-pump runs on a background
/// task concurrently with test assertions, so <see cref="SentText"/> and
/// <see cref="SentPongs"/> use <see cref="ConcurrentQueue{T}"/> instead of plain
/// <see cref="List{T}"/> to avoid data races.
///
/// The fake is designed to survive multiple connect/disconnect/reconnect cycles when
/// the engine's connection factory returns the same instance (the common test pattern).
/// On each <see cref="ConnectAsync"/> call the internal receive-semaphore is recreated
/// so that <see cref="ReceiveAsync"/> works correctly after the engine reconnects.
/// </remarks>
public sealed class FakeWebSocketConnection : IWebSocketConnection, IDisposable
{
    private readonly ConcurrentQueue<ReadOnlyMemory<byte>?> _inbound = new();

    // Recreated on each ConnectAsync so reconnect tests can reuse the same fake instance.
    // Guarded by _semLock to avoid data races between ConnectAsync and concurrent ReceiveAsync.
    private SemaphoreSlim _available = new(0);
    private readonly object _semLock = new();

    // ── Captured outbound messages (thread-safe) ──────────────────────────────

    /// <summary>Messages sent via <see cref="SendTextAsync"/> in arrival order.</summary>
    public ConcurrentQueue<string> SentText { get; } = new();

    /// <summary>Ping payloads sent via <see cref="SendPingAsync"/> in arrival order.</summary>
    public ConcurrentQueue<ReadOnlyMemory<byte>> SentPings { get; } = new();

    /// <summary>Pong payloads sent via <see cref="SendPongAsync"/> in arrival order.</summary>
    public ConcurrentQueue<ReadOnlyMemory<byte>> SentPongs { get; } = new();

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

        // Recreate the semaphore so ReceiveAsync works after a reconnect cycle.
        // Any previously enqueued frames from before the disconnect are drained first.
        lock (_semLock)
        {
            while (_inbound.TryDequeue(out _)) { } // drain stale frames
            var old = _available;
            _available = new SemaphoreSlim(0);
            old.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendTextAsync(string text, CancellationToken ct)
    {
        SentText.Enqueue(text);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendPingAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        SentPings.Enqueue(payload);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task SendPongAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        SentPongs.Enqueue(payload);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> ReceiveAsync(CancellationToken ct)
    {
        SemaphoreSlim sem;
        lock (_semLock) { sem = _available; }
        await sem.WaitAsync(ct).ConfigureAwait(false);
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
        // Set state Closed. Do NOT dispose _available here — the engine may reconnect using
        // the same instance (test factory `() => fake`). ConnectAsync recreates _available on
        // the next connect, and Dispose() on the IDisposable path handles final cleanup.
        State = WebSocketState.Closed;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_semLock)
        {
            _available.Dispose();
        }
    }

    // ── Test-control helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Enqueues a canned text frame (UTF-8 encoded) for the next <see cref="ReceiveAsync"/> call.
    /// </summary>
    public void EnqueueFrame(string text)
    {
        SemaphoreSlim sem;
        lock (_semLock) { sem = _available; }
        _inbound.Enqueue(Encoding.UTF8.GetBytes(text));
        sem.Release();
    }

    /// <summary>
    /// Enqueues raw binary frame bytes for the next <see cref="ReceiveAsync"/> call.
    /// </summary>
    public void EnqueueFrame(ReadOnlyMemory<byte> bytes)
    {
        SemaphoreSlim sem;
        lock (_semLock) { sem = _available; }
        _inbound.Enqueue(bytes);
        sem.Release();
    }

    /// <summary>
    /// Enqueues a <see langword="null"/> frame to simulate a clean venue-initiated close.
    /// The engine interprets a <see langword="null"/> return from <see cref="ReceiveAsync"/>
    /// as a signal to attempt a reconnect.
    /// </summary>
    public void SimulateDisconnect()
    {
        State = WebSocketState.Closed;
        SemaphoreSlim sem;
        lock (_semLock) { sem = _available; }
        _inbound.Enqueue(null);
        sem.Release();
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
