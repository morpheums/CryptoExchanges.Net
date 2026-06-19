using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;
using CryptoExchanges.Net.Core.Streaming;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>
/// Real-time market-data streaming client for a single exchange. Provides subscribe
/// methods for the four standard public streams, each delivering canonical
/// <c>Core.Models</c> via awaitable callbacks. Disposing the client closes its
/// underlying connection and disposes all active subscriptions.
/// </summary>
public interface IStreamClient : IAsyncDisposable
{
    /// <summary>The exchange this client is connected to.</summary>
    ExchangeId ExchangeId { get; }

    /// <summary>
    /// Subscribes to real-time ticker (24-hour price statistics) updates for
    /// <paramref name="symbol"/>.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="handlers">Callbacks for updates and lifecycle events.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>
    /// An <see cref="IStreamSubscription"/> that remains active until disposed.
    /// </returns>
    Task<IStreamSubscription> SubscribeToTickerAsync(
        Symbol symbol,
        StreamHandlers<Ticker> handlers,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribes to real-time trade updates for <paramref name="symbol"/>.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="handlers">Callbacks for updates and lifecycle events.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>
    /// An <see cref="IStreamSubscription"/> that remains active until disposed.
    /// </returns>
    Task<IStreamSubscription> SubscribeToTradesAsync(
        Symbol symbol,
        StreamHandlers<Trade> handlers,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribes to real-time order-book updates for <paramref name="symbol"/> at
    /// the specified <paramref name="depth"/>. Updates are delivered as per-frame
    /// snapshots; <see cref="OrderBook.LastUpdateId"/> exposes the sequence identifier
    /// for consumer-side gap detection.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="depth">The number of price levels requested on each side.</param>
    /// <param name="handlers">Callbacks for updates and lifecycle events.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>
    /// An <see cref="IStreamSubscription"/> that remains active until disposed.
    /// </returns>
    Task<IStreamSubscription> SubscribeToOrderBookAsync(
        Symbol symbol,
        int depth,
        StreamHandlers<OrderBook> handlers,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribes to real-time kline (candlestick) updates for <paramref name="symbol"/>
    /// at the given <paramref name="interval"/>.
    /// </summary>
    /// <param name="symbol">The trading pair to subscribe to.</param>
    /// <param name="interval">The candlestick time interval.</param>
    /// <param name="handlers">Callbacks for updates and lifecycle events.</param>
    /// <param name="ct">Token to cancel the subscribe handshake.</param>
    /// <returns>
    /// An <see cref="IStreamSubscription"/> that remains active until disposed.
    /// </returns>
    Task<IStreamSubscription> SubscribeToKlinesAsync(
        Symbol symbol,
        KlineInterval interval,
        StreamHandlers<Candlestick> handlers,
        CancellationToken ct = default);
}
