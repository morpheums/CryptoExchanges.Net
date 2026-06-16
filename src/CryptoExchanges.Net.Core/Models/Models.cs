using System.Diagnostics.CodeAnalysis;

namespace CryptoExchanges.Net.Core.Models;

/// <summary>
/// Represents a trading pair symbol, e.g. "BTCUSDT".
/// Stored as a readonly record struct for zero-allocation usage in hot paths.
/// </summary>
public readonly record struct Symbol : ISpanParsable<Symbol>
{
    /// <summary>The base asset (e.g. "BTC").</summary>
    public string BaseAsset { get; init; }

    /// <summary>The quote asset (e.g. "USDT").</summary>
    public string QuoteAsset { get; init; }

    /// <summary>
    /// Initializes a new <see cref="Symbol"/> with the specified asset pair.
    /// </summary>
    public Symbol(string baseAsset, string quoteAsset)
    {
        BaseAsset = baseAsset;
        QuoteAsset = quoteAsset;
    }

    /// <inheritdoc />
    public override string ToString() => $"{BaseAsset}{QuoteAsset}";

    /// <summary>
    /// Parses a symbol from <see cref="ReadOnlySpan{Char}"/>, e.g. "BTCUSDT" → Base="BTC", Quote="USDT".
    /// </summary>
    public static Symbol Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, provider, out var result))
            throw new FormatException($"Cannot parse '{s}' as a Symbol.");
        return result;
    }

    /// <inheritdoc />
    public static Symbol Parse(string s, IFormatProvider? provider = null)
        => Parse(s.AsSpan(), provider);

    /// <inheritdoc />
    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [MaybeNullWhen(false)] out Symbol result)
    {
        result = default;
        if (s.Length < 2)
            return false;

        int splitIndex = FindSplitPoint(s);
        if (splitIndex < 0)
            return false;

        var baseSpan = s[..splitIndex];
        var quoteSpan = s[splitIndex..];

        // Trim surrounding whitespace if any
        baseSpan = baseSpan.Trim();
        quoteSpan = quoteSpan.Trim();

        if (baseSpan.IsEmpty || quoteSpan.IsEmpty)
            return false;

        result = new Symbol(baseSpan.ToString(), quoteSpan.ToString());
        return true;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Symbol result)
        => TryParse(s.AsSpan(), provider, out result);

    private static readonly string[] KnownQuoteAssets =
    [
        "USDT", "USDC", "BUSD", "TUSD", "USDP", "DAI",
        "USD", "EUR", "GBP", "JPY", "TRY", "AUD", "BRL",
        "RUB", "UAH", "BIDR", "BTC", "ETH", "BNB", "XRP",
        "TRX", "DOGE", "SOL", "FDUSD", "PAXG", "PAX"
    ];

    private static int FindSplitPoint(ReadOnlySpan<char> s)
    {
        // Look for known quote assets starting at each position after the first char.
        for (int i = 1; i < s.Length - 1; i++)
        {
            var candidate = s[i..];
            foreach (var qs in KnownQuoteAssets)
            {
                if (candidate.Length >= qs.Length &&
                    candidate[..qs.Length].Equals(qs, StringComparison.OrdinalIgnoreCase) &&
                    candidate.Length == qs.Length)
                {
                    return i;
                }
            }
        }

        // Fallback: assume the quote is the last 3-5 characters.
        for (int quoteLen = 5; quoteLen >= 3; quoteLen--)
        {
            if (s.Length > quoteLen)
                return s.Length - quoteLen;
        }

        // Could not determine a split point.
        return -1;
    }
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
    Symbol? Symbol = null);

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
    string RateLimitType,
    string Interval,
    int Limit);
