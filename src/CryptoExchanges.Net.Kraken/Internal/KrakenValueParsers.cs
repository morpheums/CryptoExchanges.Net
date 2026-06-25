using CryptoExchanges.Net.Core.Models;

namespace CryptoExchanges.Net.Kraken.Internal;

/// <summary>
/// Shared response-side value parsers for Kraken REST API payloads.
/// All methods convert raw JSON field values (decimal strings, single-char enum tokens,
/// positional arrays) into domain types using <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.
/// </summary>
internal static class KrakenValueParsers
{
    /// <summary>
    /// Parses a decimal string from a Kraken response field.
    /// Returns <c>0</c> for null or empty input.
    /// </summary>
    public static decimal ParseDecimal(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;
        return decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses an optional decimal string from a Kraken response field.
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
    /// for null/empty or unrepresentable tickers rather than throwing.
    /// </summary>
    public static Asset ParseAssetOrNone(string? ticker)
        => Asset.TryOf(ticker, out var asset) ? asset : Asset.None;

    /// <summary>
    /// Parses the Kraken wire string for an order side into <see cref="OrderSide"/>.
    /// Kraken uses lower-case <c>buy</c>/<c>sell</c>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized side string.</exception>
    public static OrderSide ParseOrderSide(string s) => s switch
    {
        "buy"  => OrderSide.Buy,
        "sell" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order side: {s}")
    };

    /// <summary>
    /// Parses the Kraken wire <c>ordertype</c> string into <see cref="OrderType"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized type string.</exception>
    public static OrderType ParseOrderType(string s) => s switch
    {
        "limit"             => OrderType.Limit,
        "market"            => OrderType.Market,
        "stop-loss"         => OrderType.StopLoss,
        "stop-loss-limit"   => OrderType.StopLossLimit,
        "take-profit"       => OrderType.TakeProfit,
        "take-profit-limit" => OrderType.TakeProfitLimit,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown order type: {s}")
    };

    /// <summary>
    /// Parses the Kraken wire <c>status</c> string into <see cref="OrderStatus"/>.
    /// Unknown statuses map to <see cref="OrderStatus.Unknown"/> rather than throwing.
    /// </summary>
    public static OrderStatus ParseOrderStatus(string s) => s switch
    {
        "pending" or "open"  => OrderStatus.New,
        "closed"             => OrderStatus.Filled,
        "canceled"           => OrderStatus.Canceled,
        "expired"            => OrderStatus.Canceled,
        _ => OrderStatus.Unknown
    };

    /// <summary>
    /// Parses a unix fractional-seconds timestamp (decimal) to milliseconds as a long.
    /// Kraken uses fractional unix seconds (e.g. 1234567890.1234) throughout the REST API.
    /// </summary>
    public static long ParseFractionalSecondsToMs(decimal value)
        => (long)(value * 1000m);

    /// <summary>
    /// Parses a unix integer-seconds timestamp to milliseconds as a long.
    /// </summary>
    public static long ParseSecondsToMs(long seconds)
        => seconds * 1000L;

    /// <summary>
    /// Parses a string-encoded unix-ms timestamp, returning <c>0</c> for null/empty/malformed input.
    /// </summary>
    public static long ParseMs(string value)
        => long.TryParse(value, System.Globalization.NumberStyles.Integer,
               System.Globalization.CultureInfo.InvariantCulture, out var ms) ? ms : 0L;

    /// <summary>
    /// Extracts a string value from a positional <see cref="JsonElement"/> array entry.
    /// Kraken order-book and trade rows are heterogeneous arrays (mix of string and number elements).
    /// </summary>
    public static string GetArrayString(List<JsonElement> row, int index)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (index >= row.Count)
            return string.Empty;
        var el = row[index];
        return el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : el.GetRawText();
    }

    /// <summary>
    /// Extracts a long value from a positional <see cref="JsonElement"/> array entry.
    /// </summary>
    public static long GetArrayLong(List<JsonElement> row, int index)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (index >= row.Count)
            return 0L;
        var el = row[index];
        return el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var v) ? v : 0L;
    }

    /// <summary>
    /// Parses the Kraken trade taker-side single-char token into <see cref="OrderSide"/>.
    /// Kraken trades API uses <c>b</c> (buy) and <c>s</c> (sell).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any unrecognized token.</exception>
    public static OrderSide ParseTradeSide(string s) => s switch
    {
        "b" => OrderSide.Buy,
        "s" => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, $"Unknown trade side: {s}")
    };
}
