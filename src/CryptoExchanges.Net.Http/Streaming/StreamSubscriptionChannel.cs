using System.Threading.Channels;
using CryptoExchanges.Net.Core.Streaming;
using Microsoft.Extensions.Logging;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Per-subscription bounded channel and consumer task.
/// Frames routed by the engine pump are written here; a dedicated consumer task reads
/// them and invokes the typed delivery callback. Per-subscription FIFO is preserved.
/// </summary>
/// <remarks>
/// <para>
/// The channel uses <see cref="BoundedChannelFullMode.DropOldest"/>: when the channel is
/// full the oldest queued item is evicted, the per-subscription dropped count is incremented,
/// and <c>OnLagged</c> is raised (R4). The lag signal is never injected into the data stream
/// and is never thrown as an exception.
/// </para>
/// <para>
/// Callback exceptions are caught and logged; the consumer task never dies on a throwing
/// callback (isolation guarantee).
/// </para>
/// </remarks>
internal sealed class StreamSubscriptionChannel<T> : IAsyncDisposable
{
    private static readonly Action<ILogger, Exception?> s_logCallbackException =
        LoggerMessage.Define(LogLevel.Error, new EventId(100, "CallbackException"),
            "Unhandled exception in stream subscription callback; pump continues.");

    private readonly int _capacity;
    private readonly Channel<object> _channel;
    private readonly Func<T, ValueTask> _onUpdate;
    private readonly Func<StreamLag, ValueTask>? _onLagged;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _consumerTask;

    // Tracks the number of items currently pending in the channel (written but not yet consumed).
    // Written by the pump (single writer) and decremented by the consumer (single reader).
    // Used to detect when a write will overflow the channel capacity and trigger DropOldest.
    private int _pendingCount;

    // Accumulates the count of items dropped due to backpressure between consumer deliveries.
    private int _droppedCount;

    /// <summary>
    /// Initialises a new per-subscription bounded channel and starts its consumer task.
    /// </summary>
    /// <param name="capacity">Maximum number of undelivered frames before DropOldest eviction.</param>
    /// <param name="onUpdate">The typed delivery callback.</param>
    /// <param name="onLagged">Optional lag callback invoked when frames are dropped.</param>
    /// <param name="logger">Logger for callback exception isolation.</param>
    public StreamSubscriptionChannel(
        int capacity,
        Func<T, ValueTask> onUpdate,
        Func<StreamLag, ValueTask>? onLagged,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        ArgumentNullException.ThrowIfNull(logger);

        _capacity = capacity;
        _onUpdate = onUpdate;
        _onLagged = onLagged;
        _logger = logger;

        _channel = Channel.CreateBounded<object>(new BoundedChannelOptions(capacity)
        {
            // DropOldest: when full, evicts the oldest item and writes the newest.
            // TryWrite always returns true, so we track overflow via _pendingCount.
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true,
        });

        _consumerTask = Task.Run(ConsumeAsync);
    }

    /// <summary>
    /// Writes a decoded model object to the channel using DropOldest semantics.
    /// When the channel is at capacity, the oldest pending item is evicted and the
    /// dropped count is incremented so that <c>OnLagged</c> fires before the next delivery.
    /// </summary>
    /// <param name="item">The decoded model object delivered to <c>OnUpdate</c> after cast to <typeparamref name="T"/>.</param>
    public void Write(object item)
    {
        // Detect overflow BEFORE writing. The pending count is incremented by the writer
        // and decremented by the consumer, so pendingCount accurately reflects in-flight items
        // from the writer's perspective (no concurrent writers).
        var pending = Interlocked.Increment(ref _pendingCount);
        if (pending > _capacity)
        {
            // Channel is full — DropOldest will evict one item when TryWrite is called.
            // We count the eviction here so OnLagged can report it.
            Interlocked.Decrement(ref _pendingCount); // DropOldest evicts one; net pending stays <= capacity
            Interlocked.Increment(ref _droppedCount);
        }

        // DropOldest mode: always succeeds.
        _channel.Writer.TryWrite(item);
    }

    /// <summary>Signals the channel writer that no more items will be written and waits for the consumer to drain.</summary>
    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        await _cts.CancelAsync().ConfigureAwait(false);
        try { await _consumerTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _cts.Dispose();
    }

    // ── Private consumer ──────────────────────────────────────────────────────

    private async Task ConsumeAsync()
    {
        var reader = _channel.Reader;
        try
        {
            await foreach (var item in reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                // Decrement pending count (consumer has taken one item from the channel).
                Interlocked.Decrement(ref _pendingCount);

                // Report any accumulated drops before delivering the next item.
                var dropped = Interlocked.Exchange(ref _droppedCount, 0);
                if (dropped > 0 && _onLagged is not null)
                    await InvokeIsolatedAsync(() => _onLagged(new StreamLag(dropped))).ConfigureAwait(false);

                await InvokeIsolatedAsync(() => _onUpdate((T)item)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* clean shutdown */ }
    }

    private async ValueTask InvokeIsolatedAsync(Func<ValueTask> callback)
    {
        try
        {
            await callback().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // intentional: callback exception must not kill the consumer task
        catch (Exception ex)
#pragma warning restore CA1031
        {
            s_logCallbackException(_logger, ex);
        }
    }
}
