namespace CryptoExchanges.Net.Core.Resilience;

/// <summary>
/// Venue-neutral clock-skew offset primitive shared by every exchange client. Computes the
/// offset (<c>server - local</c>, in milliseconds) applied to signed-request timestamps so a
/// skewed local clock does not trip an exchange's timestamp/recv-window check (e.g. Binance
/// -1021, Bybit retCode 10002, OKX timestamp-expiry). The offset is read from the venue's
/// server-time endpoint and written into a shared single-element holder that the signing
/// handler reads on each request.
/// </summary>
/// <remarks>
/// This carries zero exchange-specific logic, so it lives once in Core and is consumed
/// cross-assembly by all exchange clients. The offset holder works in milliseconds; each
/// signing handler converts it to whatever wire format its venue expects (epoch-ms string,
/// ISO-8601, etc.).
/// </remarks>
public static class ExchangeTimeSync
{
    /// <summary>server - local, in milliseconds.</summary>
    /// <param name="serverTimeMs">The exchange server time, in epoch milliseconds.</param>
    /// <param name="localNowMs">The local clock reading, in epoch milliseconds.</param>
    /// <returns>The clock-skew offset (may be negative if the local clock is ahead).</returns>
    public static long ComputeOffset(long serverTimeMs, long localNowMs) => serverTimeMs - localNowMs;

    /// <summary>
    /// Computes the offset and writes it atomically into the shared single-element holder that
    /// the signing handler reads on each request. Returns the offset that was written.
    /// </summary>
    /// <param name="serverTimeMs">The exchange server time, in epoch milliseconds.</param>
    /// <param name="localNowMs">The local clock reading, in epoch milliseconds.</param>
    /// <param name="offsetHolder">The shared single-element holder to write into.</param>
    /// <returns>The clock-skew offset that was written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="offsetHolder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="offsetHolder"/> has no elements.</exception>
    public static long ApplyOffset(long serverTimeMs, long localNowMs, long[] offsetHolder)
    {
        ArgumentNullException.ThrowIfNull(offsetHolder);
        if (offsetHolder.Length < 1)
            throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));
        var offset = ComputeOffset(serverTimeMs, localNowMs);
        Interlocked.Exchange(ref offsetHolder[0], offset);
        return offset;
    }
}
