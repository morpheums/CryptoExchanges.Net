using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Core.Interfaces;

// ──────────────────────────────────────────────
//  Market Data
// ──────────────────────────────────────────────

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
}

// ──────────────────────────────────────────────
//  Trading
// ──────────────────────────────────────────────

/// <summary>Provides access to order placement and management endpoints.</summary>
public interface ITradingService
{
    /// <summary>Places a new order.</summary>
    Task<Order> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct = default);

    /// <summary>Cancels an order by exchange-assigned order ID.</summary>
    Task<Order> CancelOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default);

    /// <summary>Cancels an order by client-assigned order ID.</summary>
    Task<Order> CancelOrderByClientIdAsync(Symbol symbol, string clientOrderId, CancellationToken ct = default);

    /// <summary>Cancels all open orders for the specified symbol.</summary>
    Task<IReadOnlyList<Order>> CancelAllOrdersAsync(Symbol symbol, CancellationToken ct = default);

    /// <summary>Retrieves a specific order by exchange-assigned order ID.</summary>
    Task<Order> GetOrderAsync(Symbol symbol, string orderId, CancellationToken ct = default);

    /// <summary>Retrieves all currently open orders, optionally filtered by symbol.</summary>
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(Symbol? symbol = null, CancellationToken ct = default);

    /// <summary>Retrieves order history for the specified symbol.</summary>
    Task<IReadOnlyList<Order>> GetOrderHistoryAsync(Symbol symbol, int limit = 500, CancellationToken ct = default);
}

// ──────────────────────────────────────────────
//  Account
// ──────────────────────────────────────────────

/// <summary>Provides access to account information and trade history.</summary>
public interface IAccountService
{
    /// <summary>Retrieves all non-zero asset balances.</summary>
    Task<IReadOnlyList<AssetBalance>> GetBalancesAsync(CancellationToken ct = default);

    /// <summary>Retrieves the balance for a specific asset.</summary>
    Task<AssetBalance> GetBalanceAsync(string asset, CancellationToken ct = default);

    /// <summary>Retrieves trade history for a specific symbol.</summary>
    Task<IReadOnlyList<Trade>> GetTradeHistoryAsync(Symbol symbol, int limit = 500, CancellationToken ct = default);
}

// ──────────────────────────────────────────────
//  Exchange Client
// ──────────────────────────────────────────────

/// <summary>
/// Unified entry point for interacting with a cryptocurrency exchange.
/// Provides access to market data, trading, and account services.
/// </summary>
public interface IExchangeClient : IAsyncDisposable
{
    /// <summary>The exchange identifier.</summary>
    ExchangeId ExchangeId { get; }

    /// <summary>Market data service for this exchange.</summary>
    IMarketDataService MarketData { get; }

    /// <summary>Trading service for this exchange.</summary>
    ITradingService Trading { get; }

    /// <summary>Account service for this exchange.</summary>
    IAccountService Account { get; }

    /// <summary>Pings the exchange REST API to verify connectivity.</summary>
    Task<bool> PingAsync(CancellationToken ct = default);
}

// ──────────────────────────────────────────────
//  PlaceOrderRequest
// ──────────────────────────────────────────────

/// <summary>
/// Represents a request to place a new order.
/// Supports market, limit, and stop orders with optional iceberg quantities.
/// </summary>
public sealed record PlaceOrderRequest
{
    /// <summary>The trading pair symbol. Required.</summary>
    public required Symbol Symbol { get; init; }

    /// <summary>Buy or Sell. Required.</summary>
    public required OrderSide Side { get; init; }

    /// <summary>Order execution type. Required.</summary>
    public required OrderType Type { get; init; }

    /// <summary>Base asset quantity.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>Quote asset order quantity (used for market orders).</summary>
    public decimal? QuoteOrderQuantity { get; init; }

    /// <summary>Limit price. Required for limit orders.</summary>
    public decimal? Price { get; init; }

    /// <summary>Stop price. Required for stop-limit and stop-loss orders.</summary>
    public decimal? StopPrice { get; init; }

    /// <summary>How long the order remains active.</summary>
    public TimeInForce? TimeInForce { get; init; }

    /// <summary>Client-supplied order ID for idempotency.</summary>
    public string? ClientOrderId { get; init; }

    /// <summary>Visible quantity for iceberg orders.</summary>
    public decimal? IcebergQuantity { get; init; }

    /// <summary>Validates that the request is well-formed.</summary>
    /// <exception cref="ArgumentException">Thrown when required fields are missing or inconsistent.</exception>
    public void Validate()
    {
        // Guard: symbol components
        ArgumentException.ThrowIfNullOrWhiteSpace(Symbol.BaseAsset);
        ArgumentException.ThrowIfNullOrWhiteSpace(Symbol.QuoteAsset);

        var errors = new List<string>();

        if (Side != OrderSide.Buy && Side != OrderSide.Sell)
            errors.Add("Side must be Buy or Sell.");

        // Quantity validation
        if (Type != OrderType.Market || QuoteOrderQuantity is null or 0)
        {
            if (Quantity is null or <= 0)
                errors.Add("Quantity is required and must be positive.");
        }

        if (Type == OrderType.Market && QuoteOrderQuantity is not null && QuoteOrderQuantity <= 0)
            errors.Add("QuoteOrderQuantity must be positive.");

        // Price validation
        if (Type is OrderType.Limit or OrderType.StopLossLimit or OrderType.TakeProfitLimit or OrderType.LimitMaker)
        {
            if (Price is null or <= 0)
                errors.Add("Price is required for limit orders and must be positive.");
        }

        // Stop price validation
        if (Type is OrderType.StopLoss or OrderType.StopLossLimit or OrderType.TakeProfit or OrderType.TakeProfitLimit)
        {
            if (StopPrice is null or <= 0)
                errors.Add("StopPrice is required for stop orders and must be positive.");
        }

        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors));
    }
}
