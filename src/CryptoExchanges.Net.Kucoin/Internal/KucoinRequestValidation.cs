namespace CryptoExchanges.Net.Kucoin.Internal;

/// <summary>
/// Client-side validation for KuCoin request parameters, surfacing constraint violations as local
/// argument exceptions instead of opaque signed-request failures.
/// </summary>
internal static class KucoinRequestValidation
{
    /// <summary>
    /// Maximum number of records KuCoin returns per page for V1 list/history endpoints.
    /// KuCoin V1 caps <c>pageSize</c> at 500 for most history endpoints (e.g. <c>/api/v1/orders</c>,
    /// <c>/api/v1/fills</c>).
    /// </summary>
    public const int MaxHistoryLimit = 500;

    /// <summary>
    /// Validates the shared constraints for KuCoin V1 history endpoints: <paramref name="limit"/>
    /// must be in <c>1..500</c>, and any supplied <paramref name="startTime"/>/<paramref name="endTime"/>
    /// window must be ordered (start no later than end).
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
