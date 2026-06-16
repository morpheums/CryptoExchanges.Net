namespace CryptoExchanges.Net.Binance.Internal;

/// <summary>
/// Shared response-side value parsers for Binance API payloads.
/// All methods convert raw JSON field values (strings, unix-ms longs) into
/// domain types with identical semantics to the original per-service helpers.
/// </summary>
internal static class BinanceValueParsers
{
    /// <summary>
    /// Parses a decimal string from a Binance response field.
    /// Returns <c>0</c> for null or empty input.
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an optional decimal string from a Binance response field.
    /// Returns <see langword="null"/> for null, empty, or <c>"0"</c> input.
    /// </summary>
    public static decimal? ParseOptionalDecimal(string value)
    {
        if (string.IsNullOrEmpty(value) || value == "0")
            return null;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses the Binance wire string for an order side into <see cref="OrderSide"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized side string.</exception>
    public static OrderSide ParseOrderSide(string s) => s switch
    {
        "BUY" => OrderSide.Buy,
        "SELL" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order side: {s}")
    };

    /// <summary>
    /// Parses the Binance wire string for an order type into <see cref="OrderType"/>.
    /// Used for both response mapping (e.g. <c>BinanceTradingService</c>) and
    /// exchange-info order-type lists (e.g. <c>BinanceMarketDataService</c>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized type string.</exception>
    public static OrderType ParseOrderType(string s) => s switch
    {
        "LIMIT" => OrderType.Limit,
        "MARKET" => OrderType.Market,
        "STOP_LOSS" => OrderType.StopLoss,
        "STOP_LOSS_LIMIT" => OrderType.StopLossLimit,
        "TAKE_PROFIT" => OrderType.TakeProfit,
        "TAKE_PROFIT_LIMIT" => OrderType.TakeProfitLimit,
        "LIMIT_MAKER" => OrderType.LimitMaker,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order type: {s}")
    };

    /// <summary>
    /// Parses the Binance wire string for an order status into <see cref="OrderStatus"/>.
    /// Unknown statuses map to <see cref="OrderStatus.Unknown"/> rather than throwing.
    /// </summary>
    public static OrderStatus ParseOrderStatus(string s) => s switch
    {
        "NEW" => OrderStatus.New,
        "PARTIALLY_FILLED" => OrderStatus.PartiallyFilled,
        "FILLED" => OrderStatus.Filled,
        "CANCELED" => OrderStatus.Canceled,
        "PENDING_CANCEL" => OrderStatus.PendingCancel,
        "REJECTED" => OrderStatus.Rejected,
        "EXPIRED" or "EXPIRED_IN_MATCH" => OrderStatus.Expired,
        "PENDING_NEW" => OrderStatus.PendingNew,
        _ => OrderStatus.Unknown
    };

    /// <summary>
    /// Parses the Binance wire string for a time-in-force value into <see cref="TimeInForce"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized TIF string.</exception>
    public static TimeInForce ParseTimeInForce(string s) => s switch
    {
        "GTC" => TimeInForce.Gtc,
        "IOC" => TimeInForce.Ioc,
        "FOK" => TimeInForce.Fok,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown TimeInForce: {s}")
    };
}
