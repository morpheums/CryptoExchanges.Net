namespace CryptoExchanges.Net.Core.Models;

/// <summary>
/// A trading pair, e.g. BTC/USDT. Holds typed base and quote <see cref="Asset"/>s.
/// Has no wire format — converting to/from an exchange string is the job of an
/// <see cref="Interfaces.ISymbolMapper"/>.
/// </summary>
public readonly record struct Symbol
{
    /// <summary>The base asset (what you are buying/selling), e.g. BTC.</summary>
    public Asset Base { get; }

    /// <summary>The quote asset (what it is priced in), e.g. USDT.</summary>
    public Asset Quote { get; }

    /// <summary>Creates a trading pair from two distinct, valid assets.</summary>
    /// <exception cref="ArgumentException">Either leg is <see cref="Asset.None"/>, or both legs are equal.</exception>
    public Symbol(Asset @base, Asset quote)
    {
        if (@base.IsNone || quote.IsNone)
            throw new ArgumentException("Symbol legs must be valid assets (not Asset.None).");
        if (@base == quote)
            throw new ArgumentException($"Base and quote must differ (both were '{@base}').");
        Base = @base;
        Quote = quote;
    }

    /// <summary>Human-readable form, e.g. "BTC/USDT". NOT a wire format — do not send to an exchange.</summary>
    public override string ToString() => $"{Base}/{Quote}";
}

/// <summary>24-hour price change statistics for a trading pair.</summary>
public sealed record Ticker(
    Symbol Symbol,
    decimal LastPrice,
    decimal? OpenPrice = null,
    decimal? HighPrice = null,
    decimal? LowPrice = null,
    decimal? Volume = null,
    decimal? QuoteVolume = null,
    decimal? PriceChange = null,
    decimal? PriceChangePercent = null,
    DateTimeOffset? Timestamp = null);

/// <summary>A single price level in an order book (bid or ask).</summary>
public readonly record struct OrderBookEntry(
    decimal Price,
    decimal Quantity);

/// <summary>Snapshot of the current order book for a trading pair.</summary>
public sealed record OrderBook(
    Symbol Symbol,
    IReadOnlyList<OrderBookEntry> Bids,
    IReadOnlyList<OrderBookEntry> Asks,
    DateTimeOffset? Timestamp = null,
    long? LastUpdateId = null);

/// <summary>OHLCV candlestick / kline data for a given interval.</summary>
public sealed record Candlestick(
    DateTimeOffset OpenTime,
    DateTimeOffset? CloseTime = null,
    decimal Open = 0,
    decimal High = 0,
    decimal Low = 0,
    decimal Close = 0,
    decimal Volume = 0,
    decimal? QuoteVolume = null,
    int? TradeCount = null,
    Enums.KlineInterval? Interval = null,
    Symbol? TradingSymbol = null);

/// <summary>A public trade executed on the exchange.</summary>
public sealed record Trade(
    Symbol Symbol,
    string? Id = null,
    decimal Price = 0,
    decimal Quantity = 0,
    DateTimeOffset? Timestamp = null,
    bool IsBuyerMaker = false,
    string? OrderId = null);

/// <summary>An order placed or tracked on the exchange.</summary>
public sealed record Order(
    Symbol Symbol,
    string OrderId,
    string? ClientOrderId = null,
    decimal Price = 0,
    decimal OriginalQuantity = 0,
    decimal ExecutedQuantity = 0,
    Enums.OrderSide Side = Enums.OrderSide.Buy,
    Enums.OrderType Type = Enums.OrderType.Limit,
    Enums.OrderStatus Status = Enums.OrderStatus.New,
    Enums.TimeInForce TimeInForce = Enums.TimeInForce.Gtc,
    decimal? StopPrice = null,
    decimal? IcebergQuantity = null,
    DateTimeOffset? CreatedAt = null,
    DateTimeOffset? UpdatedAt = null)
{
    /// <summary>Cumulative quote asset quantity filled as of the last update.</summary>
    public decimal CumulativeQuoteQuantity { get; init; }
}

/// <summary>Balance of a single asset in the account.</summary>
public readonly record struct AssetBalance(
    string Asset,
    decimal Free,
    decimal Locked)
{
    /// <summary>Total balance (free + locked).</summary>
    public decimal Total => Free + Locked;
}

/// <summary>Exchange-wide trading rules and symbol information.</summary>
public sealed record ExchangeInfo(
    string ExchangeName,
    IReadOnlyList<SymbolInfo> Symbols,
    IReadOnlyList<RateLimit> RateLimits);

/// <summary>Detailed trading rules for a single symbol.</summary>
public sealed record SymbolInfo(
    Symbol Symbol,
    IReadOnlyList<Enums.OrderType> AllowedOrderTypes,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? TickSize = null,
    decimal? MinQuantity = null,
    decimal? MaxQuantity = null,
    decimal? StepSize = null,
    decimal? MinNotional = null);

/// <summary>Rate limit configuration for exchange API endpoints.</summary>
public sealed record RateLimit(
    Enums.RateLimitType RateLimitType,
    Enums.RateLimitInterval Interval,
    int Limit);
