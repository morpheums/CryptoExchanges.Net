namespace CryptoExchanges.Net.Coinbase.Internal;

/// <summary>
/// Shared response-side value parsers for Coinbase Advanced Trade REST payloads.
/// All methods convert raw JSON field values (decimal strings, upper-case enum tokens,
/// RFC3339 timestamps) into domain types using <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
/// </summary>
internal static class CoinbaseValueParsers
{
    /// <summary>
    /// Parses a decimal string from a Coinbase response field.
    /// Returns <c>0</c> for null or empty input.
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an optional decimal string from a Coinbase response field.
    /// Returns <see langword="null"/> for null/empty or zero-valued input.
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
    /// for null/empty or otherwise unrepresentable tickers rather than throwing.
    /// </summary>
    public static Asset ParseAssetOrNone(string? ticker)
        => Asset.TryOf(ticker, out var asset) ? asset : Asset.None;

    /// <summary>
    /// Parses the Coinbase wire string for an order side into <see cref="OrderSide"/>.
    /// Coinbase Advanced Trade uses upper-case <c>BUY</c>/<c>SELL</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized side string.</exception>
    public static OrderSide ParseOrderSide(string s) => s switch
    {
        "BUY" => OrderSide.Buy,
        "SELL" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order side: {s}")
    };

    /// <summary>
    /// Parses the Coinbase wire order status string into <see cref="OrderStatus"/>.
    /// Unknown statuses map to <see cref="OrderStatus.Unknown"/> rather than throwing.
    /// </summary>
    public static OrderStatus ParseOrderStatus(string s) => s switch
    {
        "OPEN" => OrderStatus.New,
        "FILLED" => OrderStatus.Filled,
        "CANCELLED" => OrderStatus.Canceled,
        "EXPIRED" => OrderStatus.Expired,
        "FAILED" => OrderStatus.Rejected,
        _ => OrderStatus.Unknown
    };

    /// <summary>
    /// Parses a Coinbase RFC3339 timestamp string into milliseconds since Unix epoch.
    /// Returns <c>0</c> for null/empty/malformed input.
    /// </summary>
    public static long ParseRfc3339ToMs(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0L;
        if (DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToUnixTimeMilliseconds();
        return 0L;
    }

    /// <summary>
    /// Parses a Coinbase unix-seconds string into a <see cref="DateTimeOffset"/>.
    /// Returns <see langword="null"/> for null/empty/zero input.
    /// </summary>
    public static DateTimeOffset? ParseUnixSecondsToTimestamp(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !long.TryParse(value, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var secs)
            || secs <= 0)
            return null;
        return DateTimeOffset.FromUnixTimeSeconds(secs);
    }

    /// <summary>
    /// Parses a Coinbase RFC3339 timestamp string into an optional <see cref="DateTimeOffset"/>.
    /// Returns <see langword="null"/> for null/empty/malformed input.
    /// </summary>
    public static DateTimeOffset? ParseRfc3339ToTimestamp(string value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        if (DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            return dt;
        return null;
    }

    /// <summary>
    /// Resolves the order type from the <c>order_configuration</c> shape.
    /// Coinbase encodes type implicitly via which nested configuration key is present.
    /// </summary>
    public static OrderType ParseOrderType(Dtos.OrderConfigurationDto? config) => config switch
    {
        { LimitGtc: not null } or { LimitGtd: not null } => OrderType.Limit,
        { MarketIoc: not null } => OrderType.Market,
        _ => OrderType.Limit
    };

    /// <summary>
    /// Resolves the time-in-force from the <c>order_configuration</c> shape.
    /// Limit GTC/GTD → <see cref="TimeInForce.Gtc"/>; market IOC → <see cref="TimeInForce.Ioc"/>.
    /// </summary>
    public static TimeInForce ParseTimeInForce(Dtos.OrderConfigurationDto? config) => config switch
    {
        { LimitGtc: not null } or { LimitGtd: not null } => TimeInForce.Gtc,
        { MarketIoc: not null } => TimeInForce.Ioc,
        _ => TimeInForce.Gtc
    };

    /// <summary>
    /// Extracts the limit price from <c>order_configuration</c>; returns <c>0</c> for market orders.
    /// </summary>
    public static decimal ParseLimitPrice(Dtos.OrderConfigurationDto? config) => config switch
    {
        { LimitGtc: { } gtc } => ParseDecimal(gtc.LimitPrice),
        { LimitGtd: { } gtd } => ParseDecimal(gtd.LimitPrice),
        _ => 0m
    };

    /// <summary>
    /// Extracts the original base size from <c>order_configuration</c>.
    /// For market IOC orders, uses <c>base_size</c> if present, else <c>0</c>.
    /// </summary>
    public static decimal ParseOriginalQuantity(Dtos.OrderConfigurationDto? config) => config switch
    {
        { LimitGtc: { } gtc } => ParseDecimal(gtc.BaseSize),
        { LimitGtd: { } gtd } => ParseDecimal(gtd.BaseSize),
        { MarketIoc: { } mkt } => ParseDecimal(mkt.BaseSize),
        _ => 0m
    };
}
