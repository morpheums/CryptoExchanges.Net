namespace CryptoExchanges.Net.Okx.Internal;

/// <summary>
/// Client-side validation for OKX request parameters, surfacing constraint
/// violations as local argument exceptions instead of opaque signed-request failures.
/// </summary>
internal static class OkxRequestValidation
{
    /// <summary>
    /// The OKX V5 <c>instType</c> value for spot instruments.
    /// Used as a query parameter on market data and trading endpoints (e.g. <c>/api/v5/market/tickers</c>,
    /// <c>/api/v5/trade/orders-pending</c>).
    /// </summary>
    public const string SpotInstType = "SPOT";

    /// <summary>
    /// Maximum number of records OKX returns per page for V5 list/history endpoints.
    /// OKX V5 caps <c>limit</c> at 100 for market and trade history endpoints
    /// (e.g. <c>/api/v5/trade/orders-history</c>, <c>/api/v5/market/trades</c>,
    /// <c>/api/v5/market/candles</c>).
    /// </summary>
    public const int MaxHistoryLimit = 100;

    /// <summary>
    /// Validates the shared constraints for OKX V5 history endpoints: <paramref name="limit"/>
    /// must be in <c>1..100</c>, and any supplied <paramref name="startTime"/>/<paramref name="endTime"/>
    /// window must be ordered (start no later than end). OKX does not enforce a fixed maximum window
    /// span on these endpoints (it paginates via <c>before</c>/<c>after</c> cursors), so only ordering
    /// is checked here.
    /// </summary>
    public static void ValidateHistoryWindow(
        int limit,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime)
    {
        if (limit is < 1 or > MaxHistoryLimit)
            throw new ArgumentOutOfRangeException(
                nameof(limit), limit, $"limit must be between 1 and {MaxHistoryLimit}.");

        if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            throw new ArgumentException("startTime must not be later than endTime.", nameof(startTime));
    }
}
