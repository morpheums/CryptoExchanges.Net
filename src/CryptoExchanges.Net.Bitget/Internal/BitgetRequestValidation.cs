namespace CryptoExchanges.Net.Bitget.Internal;

/// <summary>
/// Client-side validation for Bitget request parameters, surfacing constraint
/// violations as local argument exceptions instead of opaque signed-request failures.
/// </summary>
internal static class BitgetRequestValidation
{
    /// <summary>
    /// Maximum number of records Bitget returns per page for V2 spot list/history endpoints.
    /// Bitget V2 spot caps <c>limit</c> at 100 (e.g. <c>/api/v2/spot/trade/history-orders</c>,
    /// <c>/api/v2/spot/trade/fills</c>).
    /// </summary>
    public const int MaxHistoryLimit = 100;

    /// <summary>
    /// Validates the shared constraints for Bitget V2 spot history endpoints: <paramref name="limit"/>
    /// must be in <c>1..100</c>, and any supplied <paramref name="startTime"/>/<paramref name="endTime"/>
    /// window must be ordered (start no later than end). Bitget paginates via <c>idLessThan</c> cursors
    /// rather than enforcing a fixed maximum window span, so only ordering is checked here.
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
