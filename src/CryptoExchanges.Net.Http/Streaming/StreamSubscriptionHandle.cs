using CryptoExchanges.Net.Core.Streaming;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Returned to the consumer from each subscribe call. Exposes
/// <see cref="StreamConnectionState"/> and disposes the subscription on
/// <see cref="DisposeAsync"/>.
/// </summary>
/// <typeparam name="T">The decoded model type; carried only so the engine can resolve
/// the typed channel. The consumer interacts with this only via <see cref="IStreamSubscription"/>.</typeparam>
internal sealed class StreamSubscriptionHandle<T> :
    IStreamSubscription,
    StreamEngine.IEngineHandle
{
    private readonly string _routingKey;
    private readonly StreamEngine _engine;
    private volatile int _state = (int)StreamConnectionState.Connecting;
    private int _disposed;

    /// <summary>
    /// Initialises a new <see cref="StreamSubscriptionHandle{T}"/>.
    /// </summary>
    /// <param name="routingKey">The engine routing key for this subscription.</param>
    /// <param name="engine">The engine that owns this subscription.</param>
    /// <param name="handlers">The callback bundle; lifecycle callbacks are stored for engine invocation.</param>
    public StreamSubscriptionHandle(
        string routingKey,
        StreamEngine engine,
        StreamHandlers<T> handlers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(handlers);

        _routingKey = routingKey;
        _engine = engine;
        ReconnectingCallback = handlers.OnReconnecting;
        ReconnectedCallback = handlers.OnReconnected;
    }

    // ── IStreamSubscription ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public StreamConnectionState State => (StreamConnectionState)_state;

    /// <inheritdoc/>
    public bool IsConnected => State == StreamConnectionState.Live;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await _engine.UnsubscribeAsync(_routingKey).ConfigureAwait(false);
    }

    // ── IEngineHandle ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public void SetState(StreamConnectionState state)
        => Interlocked.Exchange(ref _state, (int)state);

    /// <inheritdoc/>
    public Func<ValueTask>? ReconnectingCallback { get; }

    /// <inheritdoc/>
    public Func<ValueTask>? ReconnectedCallback { get; }
}
