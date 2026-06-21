namespace CryptoExchanges.Net.Kucoin.Internal;

/// <summary>
/// Shared response-side value parsers for KuCoin V1/V2 API payloads.
/// All methods convert raw JSON field values (decimal strings, lower-case
/// enum tokens) into domain types using <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
/// </summary>
internal static class KucoinValueParsers
{
    /// <summary>
    /// Parses a decimal string from a KuCoin response field.
    /// Returns <c>0</c> for null or empty input (KuCoin emits <c>""</c> for unset numeric fields).
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an optional decimal string from a KuCoin response field.
    /// Returns <see langword="null"/> for null/empty input or any zero-valued amount.
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
    /// Parses the KuCoin wire string for an order side into <see cref="OrderSide"/>.
    /// KuCoin uses lower-case <c>buy</c>/<c>sell</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized side string.</exception>
    public static OrderSide ParseOrderSide(string s) => s switch
    {
        "buy" => OrderSide.Buy,
        "sell" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order side: {s}")
    };

    /// <summary>
    /// Parses the KuCoin wire <c>type</c> string into <see cref="OrderType"/>.
    /// KuCoin spot order types: <c>limit</c> and <c>market</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized type string.</exception>
    public static OrderType ParseOrderType(string s) => s switch
    {
        "limit" => OrderType.Limit,
        "market" => OrderType.Market,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order type: {s}")
    };

    /// <summary>
    /// Derives the <see cref="OrderStatus"/> from the KuCoin order fields <c>isActive</c> and
    /// <c>cancelExist</c>. KuCoin does not return a single status string for resting orders;
    /// instead <c>isActive=true</c> means the order is live (open or partially filled) and
    /// <c>cancelExist=true</c> means it was cancelled at some point.
    /// </summary>
    public static OrderStatus ParseOrderStatus(bool isActive, bool cancelExist) =>
        (isActive, cancelExist) switch
        {
            (true, _) => OrderStatus.New,
            (false, true) => OrderStatus.Canceled,
            _ => OrderStatus.Filled
        };

    /// <summary>
    /// Parses the KuCoin wire <c>timeInForce</c> string into <see cref="TimeInForce"/>.
    /// KuCoin uses upper-case abbreviations: <c>GTC</c>, <c>GTT</c>, <c>IOC</c>, <c>FOK</c>.
    /// <c>GTT</c> (good-till-time) is mapped to <see cref="TimeInForce.Gtc"/> as the closest
    /// domain equivalent (the SDK does not yet model a cancel-after-time field).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized TIF string.</exception>
    public static TimeInForce ParseTimeInForce(string s) => s switch
    {
        "GTC" or "GTT" => TimeInForce.Gtc,
        "IOC" => TimeInForce.Ioc,
        "FOK" => TimeInForce.Fok,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown TimeInForce: {s}")
    };

    /// <summary>
    /// Parses a KuCoin string-encoded unix-ms timestamp, returning <c>0</c> for null/empty/malformed input.
    /// KuCoin encodes most timestamps as epoch-millisecond strings throughout the V1/V2 API.
    /// </summary>
    public static long ParseMs(string value)
        => long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ms) ? ms : 0L;

    /// <summary>
    /// Parses a KuCoin string-encoded unix-nanosecond timestamp into milliseconds.
    /// KuCoin's recent-trades endpoint returns nanosecond timestamps.
    /// Returns <c>0</c> for null/empty/malformed input.
    /// </summary>
    public static long ParseNsToMs(string value)
    {
        if (!long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ns))
            return 0L;
        return ns / 1_000_000L;
    }
}
