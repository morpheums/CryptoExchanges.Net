using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;

namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// One exchange-agnostic, generic implementation of <see cref="IStreamClient"/> that wraps
/// a <see cref="StreamEngine"/> and an injected <see cref="ISymbolMapper"/>.
/// <para>
/// There is no per-exchange subclass — divergence across venues is data (a protocol +
/// a decode table), injected into the engine, not subclassed here. See the locked design:
/// §"Shared-generic client".
/// </para>
/// <para>
/// Binding constraint K1: this class uses <c>Core.Models</c> types only as transparent
/// generic type parameters carried through to the engine's <see cref="StreamHandlers{T}"/>
/// callback. No decode, no mapping, no DeltaMapper reference exists here.
/// </para>
/// </summary>
internal sealed class StreamClient : IStreamClient
{
    private readonly StreamEngine _engine;
    private readonly ISymbolMapper _symbolMapper;

    /// <summary>
    /// Initialises a <see cref="StreamClient"/> over an already-constructed engine.
    /// </summary>
    /// <param name="engine">The reconnecting byte-engine that owns the transport.</param>
    /// <param name="symbolMapper">Resolves canonical <see cref="Symbol"/> values to exchange wire strings.</param>
    /// <param name="exchangeId">The exchange this client is connected to.</param>
    public StreamClient(StreamEngine engine, ISymbolMapper symbolMapper, ExchangeId exchangeId)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(symbolMapper);

        _engine = engine;
        _symbolMapper = symbolMapper;
        ExchangeId = exchangeId;
    }

    /// <inheritdoc/>
    public ExchangeId ExchangeId { get; }

    // ── IStreamClient subscribe methods ───────────────────────────────────────

    /// <inheritdoc/>
    public Task<IStreamSubscription> SubscribeToTickerAsync(
        Symbol symbol,
        StreamHandlers<Ticker> handlers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        var wireSymbol = _symbolMapper.ToWire(symbol);
        var request = new StreamRequest(StreamKind.Ticker, wireSymbol);
        return _engine.SubscribeAsync(request, handlers, ct);
    }

    /// <summary>
    /// Subscribes to real-time ticker updates using a bare <see cref="Func{T, TResult}"/> callback.
    /// Convenience overload that wraps <paramref name="onUpdate"/> in a <see cref="StreamHandlers{T}"/>.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="onUpdate">Callback invoked on each ticker update.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>An <see cref="IStreamSubscription"/> that remains active until disposed.</returns>
    public Task<IStreamSubscription> SubscribeToTickerAsync(
        Symbol symbol,
        Func<Ticker, ValueTask> onUpdate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        return SubscribeToTickerAsync(symbol, new StreamHandlers<Ticker>(onUpdate), ct);
    }

    /// <inheritdoc/>
    public Task<IStreamSubscription> SubscribeToTradesAsync(
        Symbol symbol,
        StreamHandlers<Trade> handlers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        var wireSymbol = _symbolMapper.ToWire(symbol);
        var request = new StreamRequest(StreamKind.Trade, wireSymbol);
        return _engine.SubscribeAsync(request, handlers, ct);
    }

    /// <summary>
    /// Subscribes to real-time trade updates using a bare <see cref="Func{T, TResult}"/> callback.
    /// Convenience overload that wraps <paramref name="onUpdate"/> in a <see cref="StreamHandlers{T}"/>.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="onUpdate">Callback invoked on each trade update.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>An <see cref="IStreamSubscription"/> that remains active until disposed.</returns>
    public Task<IStreamSubscription> SubscribeToTradesAsync(
        Symbol symbol,
        Func<Trade, ValueTask> onUpdate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        return SubscribeToTradesAsync(symbol, new StreamHandlers<Trade>(onUpdate), ct);
    }

    /// <inheritdoc/>
    public Task<IStreamSubscription> SubscribeToOrderBookAsync(
        Symbol symbol,
        int depth,
        StreamHandlers<OrderBook> handlers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        var wireSymbol = _symbolMapper.ToWire(symbol);
        var request = new StreamRequest(StreamKind.OrderBook, wireSymbol, Depth: depth);
        return _engine.SubscribeAsync(request, handlers, ct);
    }

    /// <summary>
    /// Subscribes to real-time order-book updates using a bare <see cref="Func{T, TResult}"/> callback.
    /// Convenience overload that wraps <paramref name="onUpdate"/> in a <see cref="StreamHandlers{T}"/>.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="depth">The number of price levels requested on each side.</param>
    /// <param name="onUpdate">Callback invoked on each order-book update.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>An <see cref="IStreamSubscription"/> that remains active until disposed.</returns>
    public Task<IStreamSubscription> SubscribeToOrderBookAsync(
        Symbol symbol,
        int depth,
        Func<OrderBook, ValueTask> onUpdate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        return SubscribeToOrderBookAsync(symbol, depth, new StreamHandlers<OrderBook>(onUpdate), ct);
    }

    /// <inheritdoc/>
    public Task<IStreamSubscription> SubscribeToKlinesAsync(
        Symbol symbol,
        KlineInterval interval,
        StreamHandlers<Candlestick> handlers,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handlers);
        var wireSymbol = _symbolMapper.ToWire(symbol);
        // The canonical interval token (enum name, e.g. "OneMinute") is passed to the request.
        // IStreamProtocol.BuildSubscribe maps it to the exchange's native wire notation (e.g. "1m").
        var intervalToken = interval.ToString();
        var request = new StreamRequest(StreamKind.Kline, wireSymbol, Interval: intervalToken);
        return _engine.SubscribeAsync(request, handlers, ct);
    }

    /// <summary>
    /// Subscribes to real-time kline (candlestick) updates using a bare <see cref="Func{T, TResult}"/> callback.
    /// Convenience overload that wraps <paramref name="onUpdate"/> in a <see cref="StreamHandlers{T}"/>.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="interval">The candlestick time interval.</param>
    /// <param name="onUpdate">Callback invoked on each kline update.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>An <see cref="IStreamSubscription"/> that remains active until disposed.</returns>
    public Task<IStreamSubscription> SubscribeToKlinesAsync(
        Symbol symbol,
        KlineInterval interval,
        Func<Candlestick, ValueTask> onUpdate,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        return SubscribeToKlinesAsync(symbol, interval, new StreamHandlers<Candlestick>(onUpdate), ct);
    }

    // ── IAsyncDisposable ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _engine.DisposeAsync();
}
