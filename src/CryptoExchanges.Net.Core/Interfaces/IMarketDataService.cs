using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>Provides access to public market data endpoints.</summary>
public interface IMarketDataService
{
    /// <summary>
    /// Retrieves 24-hour ticker statistics for one or all symbols.
    /// </summary>
    /// <param name="symbol">Optional symbol filter; if null, returns tickers for all symbols.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Ticker>> GetTickersAsync(Symbol? symbol = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current order book for a symbol.
    /// </summary>
    /// <param name="symbol">The trading pair symbol.</param>
    /// <param name="depth">Number of price levels to retrieve (e.g. 10, 100, 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<OrderBook> GetOrderBookAsync(Symbol symbol, int depth = 100, CancellationToken ct = default);

    /// <summary>
    /// Retrieves candlestick (kline) data for a symbol.
    /// </summary>
    /// <param name="symbol">The trading pair symbol.</param>
    /// <param name="interval">The kline interval.</param>
    /// <param name="startTime">Optional start time filter.</param>
    /// <param name="endTime">Optional end time filter.</param>
    /// <param name="limit">Maximum number of candles to retrieve (default 500).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Candlestick>> GetCandlesticksAsync(
        Symbol symbol,
        KlineInterval interval,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int limit = 500,
        CancellationToken ct = default);

    /// <summary>Retrieves the latest price for a symbol.</summary>
    Task<decimal> GetPriceAsync(Symbol symbol, CancellationToken ct = default);

    /// <summary>Retrieves the most recent public trades for a symbol.</summary>
    /// <param name="symbol">The trading pair symbol.</param>
    /// <param name="limit">Maximum number of trades (default 500).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Trade>> GetRecentTradesAsync(Symbol symbol, int limit = 500, CancellationToken ct = default);

    /// <summary>Retrieves exchange-wide trading rules and symbol information.</summary>
    Task<ExchangeInfo> GetExchangeInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Determines whether a symbol is supported by the exchange. Opt-in validation:
    /// other methods do not call this implicitly. Backed by a lazily-fetched, cached
    /// snapshot of the exchange's supported symbol set.
    /// </summary>
    /// <param name="symbol">The trading pair to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the symbol is in the exchange's supported set.</returns>
    Task<bool> IsSupportedAsync(Symbol symbol, CancellationToken ct = default);

    /// <summary>
    /// Resolves a symbol to its canonical supported form. Opt-in validation: other methods
    /// do not call this implicitly. Backed by a lazily-fetched, cached snapshot of the
    /// exchange's supported symbol set.
    /// </summary>
    /// <param name="symbol">The trading pair to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical supported <see cref="Symbol"/>, or <see langword="null"/> if unsupported.</returns>
    Task<Symbol?> ResolveSymbolAsync(Symbol symbol, CancellationToken ct = default);
}
