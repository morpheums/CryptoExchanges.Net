namespace CryptoExchanges.Net.Coinbase.Internal;

/// <summary>
/// Client-side validation for Coinbase Advanced Trade request parameters, surfacing constraint
/// violations as local argument exceptions instead of opaque signed-request failures.
/// </summary>
internal static class CoinbaseRequestValidation
{
    /// <summary>
    /// Maximum number of candles Coinbase returns per call for the
    /// <c>/api/v3/brokerage/products/{product_id}/candles</c> endpoint.
    /// Coinbase caps <c>limit</c> at 350 per call.
    /// </summary>
    public const int MaxCandleLimit = 350;

    /// <summary>
    /// Maximum number of records returned per call for the
    /// <c>/api/v3/brokerage/market/market-trades</c> endpoint.
    /// </summary>
    public const int MaxTradesLimit = 1000;

    /// <summary>
    /// Validates the shared constraints for Coinbase candle requests: <paramref name="limit"/>
    /// must be in <c>1..350</c>, and any supplied <paramref name="startTime"/>/<paramref name="endTime"/>
    /// window must be ordered (start no later than end).
    /// </summary>
    public static void ValidateCandleWindow(
        int limit,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime)
    {
        if (limit is < 1 or > MaxCandleLimit)
            throw new ArgumentOutOfRangeException(
                nameof(limit), limit, $"limit must be between 1 and {MaxCandleLimit}.");

        if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            throw new ArgumentException("startTime must not be later than endTime.", nameof(startTime));
    }
}
