namespace CryptoExchanges.Net.Binance.Internal;

/// <summary>
/// Client-side validation for Binance request parameters, surfacing constraint
/// violations as local argument exceptions instead of opaque signed-request failures.
/// </summary>
internal static class BinanceRequestValidation
{
    /// <summary>Maximum number of records Binance returns for history endpoints.</summary>
    public const int MaxHistoryLimit = 1000;

    private static readonly TimeSpan MaxHistorySpan = TimeSpan.FromHours(24);

    /// <summary>
    /// Validates the shared constraints for the <c>/api/v3/allOrders</c> and
    /// <c>/api/v3/myTrades</c> history endpoints: <paramref name="limit"/> must be in
    /// <c>1..1000</c>, and any supplied <paramref name="startTime"/>/<paramref name="endTime"/>
    /// window must be ordered and span at most 24 hours.
    /// </summary>
    public static void ValidateHistoryWindow(
        int limit,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime)
    {
        if (limit is < 1 or > MaxHistoryLimit)
            throw new ArgumentOutOfRangeException(
                nameof(limit), limit, $"limit must be between 1 and {MaxHistoryLimit}.");

        if (startTime.HasValue && endTime.HasValue)
        {
            if (startTime.Value > endTime.Value)
                throw new ArgumentException("startTime must not be later than endTime.", nameof(startTime));

            if (endTime.Value - startTime.Value > MaxHistorySpan)
                throw new ArgumentException("The startTime/endTime window must not exceed 24 hours.", nameof(endTime));
        }
    }
}
