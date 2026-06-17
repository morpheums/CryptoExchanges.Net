namespace CryptoExchanges.Net.Bybit.Internal;

/// <summary>
/// Client-side validation for Bybit request parameters, surfacing constraint
/// violations as local argument exceptions instead of opaque signed-request failures.
/// </summary>
internal static class BybitRequestValidation
{
    /// <summary>Maximum number of records Bybit returns per page for history endpoints.</summary>
    public const int MaxHistoryLimit = 50;

    private static readonly TimeSpan MaxHistorySpan = TimeSpan.FromDays(7);

    /// <summary>
    /// Validates the shared constraints for the V5 <c>/order/history</c> and
    /// <c>/execution/list</c> history endpoints: <paramref name="limit"/> must be in
    /// <c>1..50</c>, and any supplied <paramref name="startTime"/>/<paramref name="endTime"/>
    /// window must be ordered and span at most 7 days.
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
                throw new ArgumentException("The startTime/endTime window must not exceed 7 days.", nameof(endTime));
        }
    }
}
