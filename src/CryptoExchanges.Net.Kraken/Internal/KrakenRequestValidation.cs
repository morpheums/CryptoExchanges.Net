namespace CryptoExchanges.Net.Kraken.Internal;

/// <summary>
/// Client-side validation for Kraken request parameters, surfacing constraint violations as
/// local argument exceptions instead of opaque signed-request failures.
/// </summary>
internal static class KrakenRequestValidation
{
    /// <summary>
    /// Maximum number of records Kraken returns per call for trades/history endpoints.
    /// Kraken caps <c>count</c> at 1000 on <c>/0/public/Trades</c>.
    /// </summary>
    public const int MaxTradesLimit = 1000;

    /// <summary>
    /// Validates the shared constraints for history endpoints: <paramref name="limit"/> must be
    /// at least 1, and any supplied <paramref name="startTime"/>/<paramref name="endTime"/>
    /// window must be ordered (start no later than end).
    /// </summary>
    public static void ValidateHistoryWindow(
        int limit,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime)
    {
        if (limit < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be at least 1.");

        if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            throw new ArgumentException("startTime must not be later than endTime.", nameof(startTime));
    }
}
