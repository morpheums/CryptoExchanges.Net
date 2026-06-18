namespace CryptoExchanges.Net.Okx.Internal;

/// <summary>
/// Shared response-side value parsers for OKX V5 API payloads.
/// All methods convert raw JSON field values (decimal strings, lower-case
/// enum tokens) into domain types using <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
/// </summary>
internal static class OkxValueParsers
{
    /// <summary>
    /// Parses a decimal string from an OKX response field.
    /// Returns <c>0</c> for null or empty input (OKX emits <c>""</c> for unset numeric fields).
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an optional decimal string from an OKX response field.
    /// Returns <see langword="null"/> for null/empty input or any zero-valued amount
    /// (e.g. <c>"0"</c>), which OKX uses for unset optional fields such as <c>avgPx</c>.
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
    /// Parses the OKX wire string for an order side into <see cref="OrderSide"/>.
    /// OKX V5 uses lower-case <c>buy</c>/<c>sell</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized side string.</exception>
    public static OrderSide ParseOrderSide(string s) => s switch
    {
        "buy" => OrderSide.Buy,
        "sell" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order side: {s}")
    };

    /// <summary>
    /// Parses the OKX wire <c>ordType</c> string into <see cref="OrderType"/>.
    /// OKX folds time-in-force semantics into <c>ordType</c>: <c>post_only</c>, <c>fok</c> and
    /// <c>ioc</c> are all resting/limit-priced order types, so each maps to <see cref="OrderType.Limit"/>
    /// (the maker-vs-taker / fill nuance is carried by <see cref="ParseTimeInForce"/>). OKX V5 spot does
    /// not expose distinct stop/take-profit order types on this field (algo orders use a separate API),
    /// so only the base spot types are mapped here.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized type string.</exception>
    public static OrderType ParseOrderType(string s) => s switch
    {
        "limit" or "post_only" or "fok" or "ioc" => OrderType.Limit,
        "market" => OrderType.Market,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order type: {s}")
    };

    /// <summary>
    /// Parses the OKX wire <c>state</c> string into <see cref="OrderStatus"/>.
    /// OKX uses American spelling (<c>canceled</c>). Unknown statuses map to
    /// <see cref="OrderStatus.Unknown"/> rather than throwing, mirroring the Bybit posture.
    /// </summary>
    public static OrderStatus ParseOrderStatus(string s) => s switch
    {
        "live" => OrderStatus.New,
        "partially_filled" => OrderStatus.PartiallyFilled,
        "filled" => OrderStatus.Filled,
        "canceled" or "mmp_canceled" => OrderStatus.Canceled,
        _ => OrderStatus.Unknown
    };

    /// <summary>
    /// Parses the OKX wire <c>ordType</c> string into <see cref="TimeInForce"/>.
    /// OKX expresses time-in-force through <c>ordType</c>: <c>fok</c> and <c>ioc</c> map directly,
    /// while <c>limit</c> and <c>post_only</c> are resting orders that map to
    /// <see cref="TimeInForce.Gtc"/> (the closest domain equivalent; <c>post_only</c> is a maker-only GTC order).
    /// A <c>market</c> order is non-resting (fills immediately against available liquidity, never rests on
    /// the book), so it maps to <see cref="TimeInForce.Ioc"/> — the closest domain semantics. This keys off
    /// the same <c>ordType</c> field as <see cref="ParseOrderType"/>, so both must accept every value the other does.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized TIF string.</exception>
    public static TimeInForce ParseTimeInForce(string s) => s switch
    {
        "limit" or "post_only" => TimeInForce.Gtc,
        "ioc" => TimeInForce.Ioc,
        "fok" => TimeInForce.Fok,
        "market" => TimeInForce.Ioc,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown TimeInForce: {s}")
    };
}
