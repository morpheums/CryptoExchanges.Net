namespace CryptoExchanges.Net.Bitget.Internal;

/// <summary>
/// Shared response-side value parsers for Bitget V2 spot API payloads.
/// All methods convert raw JSON field values (decimal strings, lower-case
/// enum tokens) into domain types using <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
/// </summary>
internal static class BitgetValueParsers
{
    /// <summary>
    /// Parses a decimal string from a Bitget response field.
    /// Returns <c>0</c> for null or empty input (Bitget emits <c>""</c> for unset numeric fields).
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an optional decimal string from a Bitget response field.
    /// Returns <see langword="null"/> for null/empty input or any zero-valued amount
    /// (e.g. <c>"0"</c>), which Bitget uses for unset optional fields such as priceAvg.
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
    /// Parses the Bitget wire <c>side</c> string into <see cref="OrderSide"/>.
    /// Bitget V2 uses lower-case <c>buy</c>/<c>sell</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized side string.</exception>
    public static OrderSide ParseOrderSide(string s) => s switch
    {
        "buy" => OrderSide.Buy,
        "sell" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order side: {s}")
    };

    /// <summary>
    /// Parses the Bitget wire <c>orderType</c> string into <see cref="OrderType"/>.
    /// Bitget V2 spot exposes lower-case <c>limit</c> and <c>market</c> base types; time-in-force
    /// is carried separately on the <c>force</c> field (see <see cref="ParseTimeInForce"/>).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized type string.</exception>
    public static OrderType ParseOrderType(string s) => s switch
    {
        "limit" => OrderType.Limit,
        "market" => OrderType.Market,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order type: {s}")
    };

    /// <summary>
    /// Parses the Bitget wire <c>status</c> string into <see cref="OrderStatus"/>.
    /// Bitget V2 spot uses British spelling (<c>cancelled</c>); <c>init</c>/<c>new</c> denote a freshly
    /// accepted resting order. Unknown statuses map to <see cref="OrderStatus.Unknown"/> rather than
    /// throwing, mirroring the Bybit/OKX posture.
    /// </summary>
    public static OrderStatus ParseOrderStatus(string s) => s switch
    {
        "init" or "new" or "live" => OrderStatus.New,
        "partially_filled" => OrderStatus.PartiallyFilled,
        "filled" => OrderStatus.Filled,
        "cancelled" => OrderStatus.Canceled,
        _ => OrderStatus.Unknown
    };

    /// <summary>
    /// Parses a Bitget string-encoded unix-ms timestamp, returning <c>0</c> for null/empty/malformed input.
    /// Bitget encodes timestamps as epoch-millisecond strings throughout the V2 API.
    /// </summary>
    public static long ParseMs(string value)
        => long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var ms) ? ms : 0L;

    /// <summary>
    /// Parses the Bitget wire <c>force</c> string into <see cref="TimeInForce"/>.
    /// Bitget V2 uses lower-case tokens; <c>post_only</c> is a resting maker-only order that maps to
    /// <see cref="TimeInForce.Gtc"/>, the closest domain equivalent.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized TIF string.</exception>
    public static TimeInForce ParseTimeInForce(string s) => s switch
    {
        "gtc" or "post_only" => TimeInForce.Gtc,
        "ioc" => TimeInForce.Ioc,
        "fok" => TimeInForce.Fok,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown TimeInForce: {s}")
    };
}
