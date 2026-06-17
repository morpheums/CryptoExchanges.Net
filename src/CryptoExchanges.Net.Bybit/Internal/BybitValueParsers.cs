namespace CryptoExchanges.Net.Bybit.Internal;

/// <summary>
/// Shared response-side value parsers for Bybit V5 API payloads.
/// All methods convert raw JSON field values (strings, unix-ms longs) into
/// domain types with identical semantics to the original per-service helpers.
/// </summary>
internal static class BybitValueParsers
{
    /// <summary>
    /// Parses a decimal string from a Bybit response field.
    /// Returns <c>0</c> for null or empty input.
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an optional decimal string from a Bybit response field.
    /// Returns <see langword="null"/> for null/empty input or any zero-valued
    /// amount (e.g. <c>"0"</c>, <c>"0.00000000"</c>), which Bybit uses for unset
    /// optional fields such as triggerPrice/avgPrice.
    /// </summary>
    public static decimal? ParseOptionalDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        var parsed = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        return parsed == 0m ? null : parsed;
    }

    /// <summary>
    /// Parses an asset ticker into a typed <see cref="Asset"/>, returning <see cref="Asset.None"/>
    /// for null/empty or otherwise unrepresentable tickers rather than throwing. Used for balance
    /// mapping where long-tail assets appear and must never abort the projection.
    /// </summary>
    public static Asset ParseAssetOrNone(string? ticker)
        => Asset.TryOf(ticker, out var asset) ? asset : Asset.None;

    /// <summary>
    /// Parses the Bybit wire string for an order side into <see cref="OrderSide"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized side string.</exception>
    public static OrderSide ParseOrderSide(string s) => s switch
    {
        "Buy" => OrderSide.Buy,
        "Sell" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order side: {s}")
    };

    /// <summary>
    /// Parses the Bybit wire string for an order type into <see cref="OrderType"/>.
    /// Bybit V5 spot exposes <c>Limit</c> and <c>Market</c> base types; stop/take-profit
    /// behaviour is conveyed by a separate trigger field rather than a distinct order type.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized type string.</exception>
    public static OrderType ParseOrderType(string s) => s switch
    {
        "Limit" => OrderType.Limit,
        "Market" => OrderType.Market,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order type: {s}")
    };

    /// <summary>
    /// Parses the Bybit wire string for an order status into <see cref="OrderStatus"/>.
    /// Unknown statuses map to <see cref="OrderStatus.Unknown"/> rather than throwing.
    /// </summary>
    public static OrderStatus ParseOrderStatus(string s) => s switch
    {
        "New" or "Created" or "Untriggered" => OrderStatus.New,
        "PartiallyFilled" => OrderStatus.PartiallyFilled,
        "Filled" => OrderStatus.Filled,
        "Cancelled" or "PartiallyFilledCanceled" or "Deactivated" => OrderStatus.Canceled,
        "Rejected" => OrderStatus.Rejected,
        "Triggered" => OrderStatus.PendingNew,
        _ => OrderStatus.Unknown
    };

    /// <summary>
    /// Parses the Bybit wire string for a time-in-force value into <see cref="TimeInForce"/>.
    /// Bybit's <c>PostOnly</c> maps to <see cref="TimeInForce.Gtc"/>, the closest domain
    /// equivalent for a resting maker-only order.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized TIF string.</exception>
    public static TimeInForce ParseTimeInForce(string s) => s switch
    {
        "GTC" or "PostOnly" => TimeInForce.Gtc,
        "IOC" => TimeInForce.Ioc,
        "FOK" => TimeInForce.Fok,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown TimeInForce: {s}")
    };
}
