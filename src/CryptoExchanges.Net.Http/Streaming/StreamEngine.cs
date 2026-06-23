using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using CryptoExchanges.Net.Core.Streaming;
using Microsoft.Extensions.Logging;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Exchange-agnostic reconnecting byte-engine. Drives one <see cref="IWebSocketConnection"/>
/// using an injected <see cref="IStreamProtocol"/>.
/// </summary>
/// <remarks>
/// <para>
/// Responsibilities (from the locked design):
/// <list type="bullet">
///   <item>Connect / lazy-open: open on first subscribe, keep-warm while ≥1 active
///         subscription, idle-close after <see cref="StreamEngineOptions.IdleCloseDelay"/>.</item>
///   <item>Single receive-pump per socket: reads frames via
///         <see cref="IWebSocketConnection.ReceiveAsync"/>, classifies with
///         <see cref="IStreamProtocol.Classify"/>, routes <c>Data</c> frames to
///         the target subscription's bounded channel, skips <c>Ack</c>,
///         resets the liveness watchdog on <c>Pong</c>, logs <c>Error</c>.</item>
///   <item>Per-subscription bounded channel (DropOldest, single-reader/single-writer).
///         A per-subscription consumer invokes the decode closure then the typed callback;
///         callback exceptions are caught and logged — the pump never dies.</item>
///   <item>Heartbeat EXECUTION (C1): driven entirely by <see cref="HeartbeatPolicy"/>
///         data; the protocol owns no timers. <c>ClientPing</c> → timer sends ping at
///         <c>Interval</c>; <c>ServerPingClientPong</c> → pong control frames; watchdog
///         triggers forced reconnect after <c>Timeout</c> without liveness.</item>
///   <item>Auto-reconnect (K3): engine's own bounded backoff loop, NOT the REST Polly
///         pipeline. Replays the stored subscribe set on reconnect (K2).</item>
/// </list>
/// </para>
/// <para>
/// Binding constraints: K1 (no <c>Core.Models</c>/DeltaMapper), K2 (subscribe-set replay),
/// K3 (engine backoff loop), C1 (heartbeat execution here, not in the protocol).
/// </para>
/// </remarks>
internal sealed class StreamEngine : IAsyncDisposable
{
    // ── LoggerMessage delegates (CA1848) ──────────────────────────────────────

    private static readonly Action<ILogger, Exception?> s_logReceiveError =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, "ReceiveError"), "WebSocket receive error; initiating reconnect.");

    private static readonly Action<ILogger, Exception?> s_logVenueClose =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "VenueClose"), "WebSocket connection closed by venue; initiating reconnect.");

    private static readonly Action<ILogger, string?, Exception?> s_logErrorFrame =
        LoggerMessage.Define<string?>(LogLevel.Warning, new EventId(3, "ErrorFrame"), "Received error frame from venue: {Frame}");

    private static readonly Action<ILogger, string?, Exception?> s_logNoSubscription =
        LoggerMessage.Define<string?>(LogLevel.Debug, new EventId(4, "NoSubscription"), "No subscription for routing key '{Key}'; frame discarded.");

    private static readonly Action<ILogger, string?, Exception?> s_logDecodeException =
        LoggerMessage.Define<string?>(LogLevel.Error, new EventId(5, "DecodeException"), "Decode exception for routing key '{Key}'; frame dropped.");

    private static readonly Action<ILogger, Exception?> s_logDispatchException =
        LoggerMessage.Define(LogLevel.Error, new EventId(6, "DispatchException"), "Unexpected exception in pump classify/dispatch; frame dropped.");

    private static readonly Action<ILogger, int, Exception?> s_logMaxAttemptsReached =
        LoggerMessage.Define<int>(LogLevel.Error, new EventId(7, "MaxAttemptsReached"), "Max reconnect attempts ({Max}) reached; closing all subscriptions.");

    private static readonly Action<ILogger, int, TimeSpan, Exception?> s_logReconnectAttempt =
        LoggerMessage.Define<int, TimeSpan>(LogLevel.Information, new EventId(8, "ReconnectAttempt"), "Reconnect attempt {Attempt} in {Delay}…");

    private static readonly Action<ILogger, int, Exception?> s_logReconnectFailed =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(9, "ReconnectFailed"), "Reconnect attempt {Attempt} failed.");

    private static readonly Action<ILogger, string?, Exception?> s_logUnsubFailed =
        LoggerMessage.Define<string?>(LogLevel.Warning, new EventId(10, "UnsubFailed"), "Failed to send unsubscribe for routing key '{Key}'.");

    private static readonly Action<ILogger, string?, Exception?> s_logReplayFailed =
        LoggerMessage.Define<string?>(LogLevel.Warning, new EventId(11, "ReplayFailed"), "Failed to replay subscribe for routing key '{Key}' on reconnect.");

    private static readonly Action<ILogger, int, int, Exception?> s_logBatchedReplay =
        LoggerMessage.Define<int, int>(LogLevel.Information, new EventId(18, "BatchedReplay"), "Replaying {Count} subscriptions in {Frames} frame(s) on reconnect.");

    private static readonly Action<ILogger, Exception?> s_logSocketCloseException =
        LoggerMessage.Define(LogLevel.Debug, new EventId(12, "SocketCloseException"), "Socket close raised an exception during idle-close (ignored).");

    private static readonly Action<ILogger, Exception?> s_logSocketDisposeOnReconnect =
        LoggerMessage.Define(LogLevel.Debug, new EventId(13, "SocketDisposeOnReconnect"), "Socket dispose on reconnect (ignored).");

    private static readonly Action<ILogger, TimeSpan, Exception?> s_logWatchdogTriggered =
        LoggerMessage.Define<TimeSpan>(LogLevel.Warning, new EventId(14, "WatchdogTriggered"), "Liveness watchdog triggered (no heartbeat in {Timeout}); forcing reconnect.");

    private static readonly Action<ILogger, Exception?> s_logPingSendFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(15, "PingSendFailed"), "Client ping send failed.");

    private static readonly Action<ILogger, Exception?> s_logIdleClose =
        LoggerMessage.Define(LogLevel.Debug, new EventId(16, "IdleClose"), "No subscriptions after idle window; closing socket.");

    private static readonly Action<ILogger, Exception?> s_logLifecycleException =
        LoggerMessage.Define(LogLevel.Error, new EventId(17, "LifecycleException"), "Unhandled exception in lifecycle callback; engine continues.");

    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly IStreamProtocol _protocol;
    private readonly StreamDecoderRegistry _decoders;
    private readonly StreamEngineOptions _options;
    private readonly Func<IWebSocketConnection> _connectionFactory;
    private readonly ILogger _logger;

    // ── Engine state ─────────────────────────────────────────────────────────

    // Both Binance (stream tokens) and KuCoin (joined symbols) cap a single control frame at 100
    // entries; the reconnect-replay chunker groups the subscribe set into batches of this size.
    private const int MaxBatchSize = 100;

    private readonly SemaphoreSlim _gate = new(1, 1);

    // Serialises ALL outbound control frames independently of _gate so a pacing delay never
    // blocks the subscribe/reconnect critical section (and so the heartbeat ping, which runs
    // outside _gate, can no longer race a subscribe send on the same socket).
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    private readonly CancellationTokenSource _disposeCts = new();

    // Active connection's pacing floor + last-send instant (monotonic). Set on every
    // socket open / reconnect from the resolved StreamConnectionInfo.MinOutboundInterval.
    private TimeSpan _minOutboundInterval;
    private long _lastSendTicks;

    private IWebSocketConnection? _socket;
    private Task? _pumpTask;
    private CancellationTokenSource? _pumpCts;

    // Per-subscription state: keyed by routing key
    private readonly ConcurrentDictionary<string, SubscriptionEntry> _subscriptions = new();

    // K2: stored subscribe set — keyed by routing key, value is the subscribe wire text
    private readonly ConcurrentDictionary<string, StreamRequest> _subscribeSet = new();

    // Idle-close
    private CancellationTokenSource? _idleCloseCts;
    private Task? _idleCloseTask;

    // Heartbeat / liveness
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private int _livenessFlag; // set to 1 on pong/liveness; watchdog clears to 0

    // Reconnect backoff
    private readonly BackoffSchedule _backoff;
    private volatile bool _isDisposed;
    private int _reconnecting; // 0=idle, 1=in-progress; prevents concurrent reconnect loops

    /// <summary>
    /// Initialises a new <see cref="StreamEngine"/> with the injected dependencies.
    /// </summary>
    /// <param name="protocol">The venue-specific protocol strategy.</param>
    /// <param name="decoders">The per-stream-kind decode closure registry.</param>
    /// <param name="options">Engine configuration (channel capacity, backoff bounds, idle-close window).</param>
    /// <param name="connectionFactory">Factory that produces a fresh <see cref="IWebSocketConnection"/> instance for each connect/reconnect.</param>
    /// <param name="logger">Logger for pump exceptions, reconnect events, and callback failures.</param>
    public StreamEngine(
        IStreamProtocol protocol,
        StreamDecoderRegistry decoders,
        StreamEngineOptions options,
        Func<IWebSocketConnection> connectionFactory,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        ArgumentNullException.ThrowIfNull(decoders);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _protocol = protocol;
        _decoders = decoders;
        _options = options;
        _connectionFactory = connectionFactory;
        _logger = logger;

        _backoff = new BackoffSchedule(
            options.BackoffInitial,
            options.BackoffMax,
            options.BackoffMultiplier);
    }

    // ── Public subscribe API ──────────────────────────────────────────────────

    /// <summary>
    /// Subscribes a new stream, lazily opens the socket if necessary, and returns a
    /// handle whose <see cref="StreamConnectionState"/> reflects engine lifecycle.
    /// </summary>
    /// <typeparam name="T">The decoded model type delivered to the callback.</typeparam>
    /// <param name="request">The subscription request descriptor.</param>
    /// <param name="handlers">The typed callback bundle.</param>
    /// <param name="ct">A token to cancel the subscribe operation.</param>
    /// <returns>An <see cref="IStreamSubscription"/> whose <c>DisposeAsync</c> unsubscribes.</returns>
    /// <remarks>
    /// Under per-venue throttling (<see cref="StreamConnectionInfo.MinOutboundInterval"/> &gt;
    /// <see cref="TimeSpan.Zero"/>) the returned task may be delayed up to one interval while the
    /// engine paces the subscribe frame. The subscribe gate is held across that delay by design.
    /// </remarks>
    public async Task<IStreamSubscription> SubscribeAsync<T>(
        StreamRequest request,
        StreamHandlers<T> handlers,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(handlers);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Cancel any pending idle-close since we now have a subscriber.
            CancelIdleClose();

            // Lazy-open the socket.
            if (_socket is null || !_socket.IsOpen)
                await OpenSocketAsync(ct).ConfigureAwait(false);

            var routingKey = _protocol.RoutingKeyFor(request);

            // Build the channel and subscription handle.
            var decoder = _decoders.Resolve(request.Kind);
            var channel = new StreamSubscriptionChannel<T>(
                _options.ChannelCapacity,
                handlers.OnUpdate,
                handlers.OnLagged,
                _logger);

            var handle = new StreamSubscriptionHandle<T>(
                routingKey,
                this,
                handlers);

            var entry = new SubscriptionEntry(
                channel: channel,
                writeFrame: bytes =>
                {
                    var model = decoder(bytes);
                    channel.Write(model);
                },
                handle: handle);

            _subscriptions[routingKey] = entry;
            _subscribeSet[routingKey] = request;

            // Send the subscribe message (paced + serialised via SendControlAsync).
            var subscribeText = _protocol.BuildSubscribe(request);
            await SendControlAsync(subscribeText, ct).ConfigureAwait(false);

            handle.SetState(StreamConnectionState.Live);
            return handle;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Unsubscribes a routing key: sends the unsubscribe message, removes from the
    /// channel map and replay set (K2), and schedules idle-close if no subscriptions remain.
    /// </summary>
    /// <param name="routingKey">The routing key of the subscription to remove.</param>
    internal async Task UnsubscribeAsync(string routingKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        await _gate.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
        try
        {
            if (!_subscriptions.TryRemove(routingKey, out var entry))
                return;

            // K2: remove from replay set BEFORE sending wire unsubscribe.
            if (_subscribeSet.TryRemove(routingKey, out var request))
            {
                if (_socket is not null && _socket.IsOpen)
                {
                    try
                    {
                        var unsubText = _protocol.BuildUnsubscribe(request);
                        await SendControlAsync(unsubText, _disposeCts.Token).ConfigureAwait(false);
                    }
#pragma warning disable CA1031 // intentional broad catch: unsubscribe failure must not propagate
                    catch (Exception ex)
#pragma warning restore CA1031
                    {
                        s_logUnsubFailed(_logger, routingKey, ex);
                    }
                }
            }

            entry.Handle.SetState(StreamConnectionState.Closed);
            await entry.Channel.DisposeAsync().ConfigureAwait(false);

            // Schedule idle-close if no subscriptions remain.
            if (_subscriptions.IsEmpty)
                ScheduleIdleClose();
        }
        finally
        {
            _gate.Release();
        }
    }

    // ── Socket lifecycle ──────────────────────────────────────────────────────

    private async Task OpenSocketAsync(CancellationToken ct)
    {
        var info = await _protocol.ResolveConnectionAsync(ct).ConfigureAwait(false);
        _socket = _connectionFactory();
        await _socket.ConnectAsync(info.Endpoint, ct).ConfigureAwait(false);
        ApplyConnectionPacing(info);
        StartPump(info.Heartbeat);
    }

    // Captures the active connection's pacing floor and resets the last-send clock so the first
    // frame on a freshly opened socket is never artificially delayed.
    private void ApplyConnectionPacing(StreamConnectionInfo info)
    {
        _minOutboundInterval = info.MinOutboundInterval;
        _lastSendTicks = Stopwatch.GetTimestamp() - Stopwatch.Frequency; // older than any interval
    }

    /// <summary>
    /// Serialises and paces a single outbound control frame. All sends (subscribe, unsubscribe,
    /// reconnect-replay, client-ping) route through here so that (a) at most one
    /// <see cref="IWebSocketConnection.SendTextAsync"/> is ever in flight, and (b) consecutive
    /// frames are spaced by at least <see cref="StreamConnectionInfo.MinOutboundInterval"/> for the
    /// active connection. When the interval is <see cref="TimeSpan.Zero"/> no delay is applied and
    /// behaviour is byte-identical to an un-throttled send (apart from the serialisation guarantee).
    /// </summary>
    /// <param name="text">The control-frame text to send.</param>
    /// <param name="ct">A token to cancel the send; linked with the engine dispose token internally.</param>
    private async Task SendControlAsync(string text, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _disposeCts.Token);
        await _sendSemaphore.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            if (_minOutboundInterval > TimeSpan.Zero)
            {
                var elapsed = Stopwatch.GetElapsedTime(_lastSendTicks);
                if (elapsed < _minOutboundInterval)
                    await Task.Delay(_minOutboundInterval - elapsed, linked.Token).ConfigureAwait(false);
            }

            if (_socket is not null && _socket.IsOpen)
                await _socket.SendTextAsync(text, linked.Token).ConfigureAwait(false);

            _lastSendTicks = Stopwatch.GetTimestamp();
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private async Task CloseSocketAsync()
    {
        // Stop the pump and heartbeat.
        if (_pumpCts is not null)
        {
            await _pumpCts.CancelAsync().ConfigureAwait(false);
            _pumpCts.Dispose();
            _pumpCts = null;
        }

        StopHeartbeat();

        if (_socket is not null)
        {
            try
            {
                if (_socket.IsOpen)
                    await _socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "idle-close", CancellationToken.None).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // intentional: close failure must not block idle-close path
            catch (Exception ex)
#pragma warning restore CA1031
            {
                s_logSocketCloseException(_logger, ex);
            }
            finally
            {
                await _socket.DisposeAsync().ConfigureAwait(false);
                _socket = null;
            }
        }

        if (_pumpTask is not null)
        {
            try { await _pumpTask.ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: pump task exceptions are already handled internally
            catch (Exception) { /* swallow — pump handles its own exceptions */ }
#pragma warning restore CA1031
            _pumpTask = null;
        }
    }

    // ── Pump ──────────────────────────────────────────────────────────────────

    private async Task PumpLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ReadOnlyMemory<byte>? frame;
            try
            {
                frame = await _socket!.ReceiveAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
#pragma warning disable CA1031 // intentional: any receive failure triggers a reconnect
            catch (Exception ex)
#pragma warning restore CA1031
            {
                s_logReceiveError(_logger, ex);
                _ = Task.Run(() => ReconnectAsync(), _disposeCts.Token);
                return;
            }

            if (frame is null)
            {
                // Null = clean venue-initiated close → reconnect.
                s_logVenueClose(_logger, null);
                _ = Task.Run(() => ReconnectAsync(), _disposeCts.Token);
                return;
            }

            // Any received frame proves the socket is alive — reset the liveness watchdog now,
            // before classification. Under ClientWebSocket auto-pong (ServerPingClientPong policy)
            // the venue Ping control frame is answered automatically and never surfaces here, so
            // FrameKind.Pong is never received; resetting on every data frame keeps the watchdog
            // from falsely triggering on healthy high-data / quiet-data streams alike.
            Interlocked.Exchange(ref _livenessFlag, 1);

            // Classify and dispatch.
            try
            {
                var classified = _protocol.Classify(frame.Value.Span);
                switch (classified.Kind)
                {
                    case FrameKind.Ack:
                        break; // discard

                    case FrameKind.Pong:
                        // Liveness already reset above on every received frame; no further action.
                        break;

                    case FrameKind.Error:
                        s_logErrorFrame(_logger, Encoding.UTF8.GetString(frame.Value.Span), null);
                        break;

                    case FrameKind.Data:
                        if (classified.RoutingKey is not null &&
                            _subscriptions.TryGetValue(classified.RoutingKey, out var entry))
                        {
                            try { entry.WriteFrame(frame.Value); }
#pragma warning disable CA1031 // intentional: decode failure must not kill the pump
                            catch (Exception ex)
#pragma warning restore CA1031
                            {
                                s_logDecodeException(_logger, classified.RoutingKey, ex);
                            }
                        }
                        else
                        {
                            s_logNoSubscription(_logger, classified.RoutingKey, null);
                        }
                        break;
                }
            }
#pragma warning disable CA1031 // intentional: classify/dispatch failure must not kill the pump
            catch (Exception ex)
#pragma warning restore CA1031
            {
                s_logDispatchException(_logger, ex);
            }
        }
    }

    // ── Reconnect (K3 — engine's own backoff, NOT Polly) ──────────────────────

    private async Task ReconnectAsync()
    {
        if (_isDisposed) return;

        // Guard against concurrent reconnect loops (e.g., watchdog + pump error firing simultaneously).
        if (Interlocked.CompareExchange(ref _reconnecting, 1, 0) != 0) return;

        try
        {
            await ReconnectCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Exchange(ref _reconnecting, 0);
        }
    }

    private async Task ReconnectCoreAsync()
    {
        if (_isDisposed) return;

        await _gate.WaitAsync(_disposeCts.Token).ConfigureAwait(false);

        // Transition all live subscriptions to Reconnecting.
        foreach (var entry in _subscriptions.Values)
        {
            entry.Handle.SetState(StreamConnectionState.Reconnecting);
            await InvokeLifecycleAsync(entry.Handle.ReconnectingCallback).ConfigureAwait(false);
        }

        _gate.Release();

        // Cancel the pump FIRST so it unblocks any pending ReceiveAsync before socket disposal.
        if (_pumpCts is not null)
        {
            await _pumpCts.CancelAsync().ConfigureAwait(false);
        }

        // Stop old heartbeat.
        StopHeartbeat();

        if (_socket is not null)
        {
            try { await _socket.DisposeAsync().ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: dispose failure must not block reconnect
            catch (Exception ex) { s_logSocketDisposeOnReconnect(_logger, ex); }
#pragma warning restore CA1031
            _socket = null;
        }
        if (_pumpTask is not null)
        {
            try { await _pumpTask.ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: old pump task is already done
            catch (Exception) { /* already handled inside pump */ }
#pragma warning restore CA1031
            _pumpTask = null;
        }
        if (_pumpCts is not null)
        {
            _pumpCts.Dispose();
            _pumpCts = null;
        }

        // Bounded backoff loop (K3).
        var maxAttempts = _options.MaxReconnectAttempts;
        while (!_isDisposed && !_disposeCts.IsCancellationRequested)
        {
            if (maxAttempts > 0 && _backoff.Attempt >= maxAttempts)
            {
                s_logMaxAttemptsReached(_logger, maxAttempts, null);
                await CloseAllSubscriptionsAsync().ConfigureAwait(false);
                return;
            }

            var delay = _backoff.Next();
            s_logReconnectAttempt(_logger, _backoff.Attempt, delay, null);

            try
            {
                await Task.Delay(delay, _disposeCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }

            await _gate.WaitAsync(_disposeCts.Token).ConfigureAwait(false);
            try
            {
                IWebSocketConnection? newSocket = null;
                StreamConnectionInfo info;
                try
                {
                    info = await _protocol.ResolveConnectionAsync(_disposeCts.Token).ConfigureAwait(false);
                    newSocket = _connectionFactory();
                    await newSocket.ConnectAsync(info.Endpoint, _disposeCts.Token).ConfigureAwait(false);
                }
#pragma warning disable CA1031 // intentional: connect failure → retry next backoff iteration
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    s_logReconnectFailed(_logger, _backoff.Attempt, ex);
                    if (newSocket is not null)
                    {
                        try { await newSocket.DisposeAsync().ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: socket dispose on failed connect
                        catch (Exception) { /* ignored */ }
#pragma warning restore CA1031
                    }
                    continue;
                }

                _socket = newSocket;
                ApplyConnectionPacing(info);

                // Connected — restart pump and heartbeat.
                StartPump(info.Heartbeat);

                // K2: replay the stored subscribe set. Chunk into ≤100-entry groups and try a single
                // batched frame per chunk (BuildSubscribeBatch); fall back to the per-frame loop when
                // the venue returns null. Either way every frame routes through SendControlAsync, so a
                // large reconnect stays throttled (e.g. 300 symbols → 3 frames × interval, not 300).
                await ReplaySubscribeSetAsync().ConfigureAwait(false);

                // Transition all subscriptions back to Live.
                foreach (var entry in _subscriptions.Values)
                {
                    entry.Handle.SetState(StreamConnectionState.Live);
                    await InvokeLifecycleAsync(entry.Handle.ReconnectedCallback).ConfigureAwait(false);
                }

                return; // Success.
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    // K2 replay (chunked + batched). Snapshots the stored subscribe set, chunks it into ≤MaxBatchSize
    // groups, and prefers a single BuildSubscribeBatch frame per chunk; on a null (batching
    // unsupported / heterogeneous) chunk it replays that chunk frame-by-frame. Every send routes
    // through SendControlAsync so batched and per-frame replay alike stay throttled. A broad catch
    // around each frame preserves the prior per-item semantics: one replay failure must not abort the
    // rest of the set (K2).
    private async Task ReplaySubscribeSetAsync()
    {
        var requests = _subscribeSet.Values.ToList();
        if (requests.Count == 0)
            return;

        var frames = 0;
        for (var offset = 0; offset < requests.Count; offset += MaxBatchSize)
        {
            var chunk = requests.GetRange(offset, Math.Min(MaxBatchSize, requests.Count - offset));
            var batchText = _protocol.BuildSubscribeBatch(chunk);

            if (batchText is not null)
            {
                try
                {
                    await SendControlAsync(batchText, _disposeCts.Token).ConfigureAwait(false);
                    frames++;
                }
#pragma warning disable CA1031 // intentional: a batched replay failure must not block remaining chunks
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    s_logReplayFailed(_logger, _protocol.RoutingKeyFor(chunk[0]), ex);
                }
                continue;
            }

            // Batching unsupported for this chunk — fall back to the per-frame throttled loop.
            foreach (var request in chunk)
            {
                try
                {
                    var subscribeText = _protocol.BuildSubscribe(request);
                    await SendControlAsync(subscribeText, _disposeCts.Token).ConfigureAwait(false);
                    frames++;
                }
#pragma warning disable CA1031 // intentional: replay failure for one subscription must not block others
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    s_logReplayFailed(_logger, _protocol.RoutingKeyFor(request), ex);
                }
            }
        }

        s_logBatchedReplay(_logger, requests.Count, frames, null);
    }

    private async Task CloseAllSubscriptionsAsync()
    {
        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            foreach (var entry in _subscriptions.Values)
            {
                entry.Handle.SetState(StreamConnectionState.Closed);
                await entry.Channel.DisposeAsync().ConfigureAwait(false);
            }
            _subscriptions.Clear();
            _subscribeSet.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    // ── Pump + heartbeat wiring (shared by OpenSocketAsync and ReconnectCoreAsync) ─

    private void StartPump(HeartbeatPolicy heartbeat)
    {
        _pumpCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token);
        _pumpTask = Task.Run(() => PumpLoopAsync(_pumpCts.Token), _pumpCts.Token);
        StartHeartbeat(heartbeat, _pumpCts.Token);
        _backoff.Reset();
    }

    // ── Heartbeat (C1 — timers/watchdog in the engine, not in the protocol) ───

    private void StartHeartbeat(HeartbeatPolicy policy, CancellationToken ct)
    {
        _heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Interlocked.Exchange(ref _livenessFlag, 1); // treat connect as liveness event
        _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(policy, _heartbeatCts.Token), _heartbeatCts.Token);
    }

    private void StopHeartbeat()
    {
        if (_heartbeatCts is not null)
        {
            _heartbeatCts.Cancel();
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
        }
    }

    private Task HeartbeatLoopAsync(HeartbeatPolicy policy, CancellationToken ct)
    {
        return policy.Direction == HeartbeatDirection.ClientPing
            ? ClientPingLoopAsync(policy, ct)
            : ServerPingWatchdogAsync(policy, ct);
    }

    private async Task ClientPingLoopAsync(HeartbeatPolicy policy, CancellationToken ct)
    {
        // Decode once per connection; payload is immutable for the lifetime of the connection.
        var pingText = policy.PingFormat is PingFormat.Text or PingFormat.Json
            ? Encoding.UTF8.GetString(policy.ClientPingPayload.Span)
            : null;

        using var watchdogCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var watchdogTask = WatchdogAsync(policy.Timeout, watchdogCts.Token);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(policy.Interval, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }

                if (ct.IsCancellationRequested) break;

                try
                {
                    if (_socket is not null && _socket.IsOpen)
                    {
                        switch (policy.PingFormat)
                        {
                            case PingFormat.ControlFrame:
                                // RFC 6455 §5.5.2 Ping control frames (opcode 0x09) cannot be
                                // emitted by ClientWebSocket.SendAsync — it only sends data frames.
                                // Framework keep-alive (ClientWebSocketOptions.KeepAliveInterval set
                                // on ClientWebSocketConnection) handles the control-frame Ping/Pong
                                // handshake automatically; the engine must not attempt a manual send.
                                // Liveness is maintained by the existing liveness-on-any-frame reset
                                // in PumpLoopAsync (every received frame resets the watchdog).
                                break;
                            case PingFormat.Text:
                            case PingFormat.Json:
                                // Route through SendControlAsync so the ping is serialised against
                                // subscribe/replay sends (previously it ran outside _gate and could
                                // race ClientWebSocket.SendAsync on KuCoin) and is itself paced.
                                await SendControlAsync(pingText!, ct).ConfigureAwait(false);
                                break;
                        }
                        Interlocked.Exchange(ref _livenessFlag, 1);
                    }
                }
#pragma warning disable CA1031 // intentional: ping send failure is non-fatal; logged and loop continues
                catch (Exception ex) when (!ct.IsCancellationRequested)
#pragma warning restore CA1031
                {
                    s_logPingSendFailed(_logger, ex);
                }
            }
        }
        finally
        {
            await watchdogCts.CancelAsync().ConfigureAwait(false);
            try { await watchdogTask.ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: watchdog task cleanup
            catch (Exception) { /* swallow — watchdog exits on cancel */ }
#pragma warning restore CA1031
        }
    }

    private Task ServerPingWatchdogAsync(HeartbeatPolicy policy, CancellationToken ct)
        => WatchdogAsync(policy.Timeout, ct);

    private async Task WatchdogAsync(TimeSpan timeout, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(timeout, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }

            var was = Interlocked.Exchange(ref _livenessFlag, 0);
            if (was == 0)
            {
                s_logWatchdogTriggered(_logger, timeout, null);
                _ = Task.Run(() => ReconnectAsync(), _disposeCts.Token);
                return;
            }
        }
    }

    // ── Idle-close ────────────────────────────────────────────────────────────

    private void ScheduleIdleClose()
    {
        CancelIdleClose();
        var idleCloseCts = new CancellationTokenSource();
        _idleCloseCts = idleCloseCts;
        var token = idleCloseCts.Token;
        _idleCloseTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_options.IdleCloseDelay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }

            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_subscriptions.IsEmpty)
                {
                    s_logIdleClose(_logger, null);
                    await CloseSocketAsync().ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }
        }, token);
    }

    private void CancelIdleClose()
    {
        if (_idleCloseCts is not null)
        {
            _idleCloseCts.Cancel();
            _idleCloseCts.Dispose();
            _idleCloseCts = null;
        }
    }

    // ── Lifecycle callback isolation ──────────────────────────────────────────

    private async ValueTask InvokeLifecycleAsync(Func<ValueTask>? callback)
    {
        if (callback is null) return;
        try { await callback().ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: lifecycle callback failure must not propagate to engine
        catch (Exception ex) { s_logLifecycleException(_logger, ex); }
#pragma warning restore CA1031
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        var idleCloseTask = _idleCloseTask;
        CancelIdleClose();
        if (idleCloseTask is not null)
        {
            try { await idleCloseTask.ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: idle-close task exits on cancellation
            catch (Exception) { /* swallow — exits via OperationCanceledException */ }
#pragma warning restore CA1031
        }
        await _disposeCts.CancelAsync().ConfigureAwait(false);

        // Dispose all subscription channels.
        foreach (var entry in _subscriptions.Values)
        {
            entry.Handle.SetState(StreamConnectionState.Closed);
            await entry.Channel.DisposeAsync().ConfigureAwait(false);
        }
        _subscriptions.Clear();
        _subscribeSet.Clear();

        StopHeartbeat();
        if (_heartbeatTask is not null)
        {
            try { await _heartbeatTask.ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: heartbeat task cleanup
            catch (Exception) { /* swallow */ }
#pragma warning restore CA1031
        }
        if (_pumpTask is not null)
        {
            try { await _pumpTask.ConfigureAwait(false); }
#pragma warning disable CA1031 // intentional: pump task cleanup
            catch (Exception) { /* swallow */ }
#pragma warning restore CA1031
        }
        if (_pumpCts is not null)
        {
            _pumpCts.Dispose();
            _pumpCts = null;
        }

        if (_socket is not null)
        {
            try
            {
                if (_socket.IsOpen)
                    await _socket.CloseAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "engine-dispose", CancellationToken.None).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // intentional: close may fail if already closed
            catch (Exception) { /* swallow */ }
#pragma warning restore CA1031
            finally
            {
                await _socket.DisposeAsync().ConfigureAwait(false);
            }
        }

        _gate.Dispose();
        _sendSemaphore.Dispose();
        _disposeCts.Dispose();
    }

    // ── Routing key ───────────────────────────────────────────────────────────

    /// <summary>
    /// Canonical routing-key helper for test fakes and contract-level assertions.
    /// <para>
    /// <strong>Note</strong>: the engine no longer uses this method on the subscribe/route hot
    /// path — that path now delegates to <see cref="IStreamProtocol.RoutingKeyFor"/> so that
    /// the keyspace is single-sourced in the protocol. This method is retained for
    /// test doubles and test assertions that verify the canonical form.
    /// Convention (uppercase): <c>&lt;WIRESYMBOL&gt;@&lt;KIND&gt;[/&lt;DEPTH&gt;][/&lt;INTERVAL&gt;]</c>.
    /// </para>
    /// </summary>
    internal static string BuildRoutingKey(StreamRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kind = request.Kind.ToString().ToUpperInvariant();
        return request.Kind switch
        {
            StreamKind.OrderBook when request.Depth.HasValue
                => $"{request.WireSymbol}@{kind}/{request.Depth}",
            StreamKind.Kline when request.Interval is not null
                => $"{request.WireSymbol}@{kind}/{request.Interval}",
            _ => $"{request.WireSymbol}@{kind}",
        };
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    /// <summary>Holds the per-subscription channel, the decoded-write delegate, and the handle.</summary>
    private sealed class SubscriptionEntry
    {
        public SubscriptionEntry(IAsyncDisposable channel, Action<ReadOnlyMemory<byte>> writeFrame, IEngineHandle handle)
        {
            Channel = channel;
            WriteFrame = writeFrame;
            Handle = handle;
        }

        /// <summary>The typed bounded channel wrapper.</summary>
        public IAsyncDisposable Channel { get; }

        /// <summary>Writes a raw frame bytes into the subscription channel after decode.</summary>
        public Action<ReadOnlyMemory<byte>> WriteFrame { get; }

        /// <summary>The lifecycle handle that surfaces <see cref="StreamConnectionState"/>.</summary>
        public IEngineHandle Handle { get; }
    }

    /// <summary>Minimal internal interface the engine uses to drive handle state.</summary>
    internal interface IEngineHandle
    {
        /// <summary>Sets the connection state on the subscription handle.</summary>
        void SetState(StreamConnectionState state);

        /// <summary>Optional callback invoked when the engine starts reconnecting.</summary>
        Func<ValueTask>? ReconnectingCallback { get; }

        /// <summary>Optional callback invoked when the engine finishes reconnecting.</summary>
        Func<ValueTask>? ReconnectedCallback { get; }
    }
}
